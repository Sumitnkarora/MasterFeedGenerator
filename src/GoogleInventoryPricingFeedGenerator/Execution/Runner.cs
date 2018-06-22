using Castle.Core.Logging;
using FeedGenerators.Core.Execution;
using FeedGenerators.Core.Services.Abstract;
using FeedGenerators.Core.Services.Concrete;
using FeedGenerators.Core.Types;
using FeedGenerators.Core.Utils;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace GoogleInventoryPricingFeedGenerator.Execution
{
    public class Runner : IRunner
    {
        private const string DeletedBooksStoredProcedureName = "uspGoogleInventoryAndPricingFeedBooksDeletedProducts";
        private const string DeletedGeneralMerchandiseStoredProcedureName = "uspGoogleInventoryAndPricingFeedGeneralMerchandiseDeletedProducts";
        private const string ExcludedProductGoogleAvailabilityText = "out of stock";

        private IExecutionLogLogger _executionLogLogger;
        private bool _isIncrementalRun;
        private DateTime? _effectiveFromTime;

        private IFeedGeneratorIndigoCategoryService _feedGeneratorCategoryService;
        private IGooglePlaFeedRuleHelper _runnerFeedRulesHelper;
        private bool _hasError;

        private readonly IFeedRuleService _feedRuleService;
        private readonly IFeedCmsProductArchiveEntryService _feedCmsProductArchiveEntryService;
        private readonly IIndigoCategoryService _indigoCategoryService;
        private readonly IGoogleCategoryService _googleCategoryService;

        private static readonly bool LimitTo100Products = ParameterUtils.GetParameter<bool>("GoogleInventoryPricingFeedGenerator.LimitTo100Products");
        private static readonly bool AllowItemErrorsInFiles = ParameterUtils.GetParameter<bool>("AllowItemErrorsInFiles");
        private static readonly bool AllowRuleOptimizations = ParameterUtils.GetParameter<bool>("AllowRuleOptimizations");
        private static readonly bool AllowRuleEntryRemovals = ParameterUtils.GetParameter<bool>("AllowRuleEntryRemovals");
        private static readonly bool AllowIEnumerableRuleEvaluations = ParameterUtils.GetParameter<bool>("AllowIEnumerableRuleEvaluations");
        private static readonly bool SendExcludedProductData = ParameterUtils.GetParameter<bool>("SendExcludedProductData");
        private static readonly bool GzipFiles = ParameterUtils.GetParameter<bool>("GoogleInventoryPricingFeedGenerator.GzipFiles");
        private static readonly string OutputFolderPath = ParameterUtils.GetParameter<string>("GoogleInventoryPricingFeedGenerator.OutputFolderPath");
        private static readonly string OdysseyCommerceConnectionString = ConfigurationManager.ConnectionStrings["OdysseyCommerceDB"].ConnectionString;
        private static readonly XNamespace FeedXmlns = ParameterUtils.GetParameter<string>("FeedXmlns");
        private static readonly XNamespace FeedXmlsnsG = ParameterUtils.GetParameter<string>("FeedXmlsnsG");
        private static readonly Dictionary<string, Dictionary<string, string>> FeedGenerationInstructionsDictionary = ConfigurationManager.GetSection("feedgenerationinstructiondict") as Dictionary<string, Dictionary<string, string>>;
        private static readonly int SearchDataCommandTimeout = ParameterUtils.GetParameter<int>("SearchDataCommandTimeout");
        private static readonly int MaximumThreadsToUse = ParameterUtils.GetParameter<int>("MaximumThreadsToUse");
        private static readonly int GooglePlaFeedId = ParameterUtils.GetParameter<int>("GooglePlaFeedId");
        private static readonly bool SkipHasImageCheck = ParameterUtils.GetParameter<bool>("GoogleInventoryPricingFeedGenerator.SkipHasImageCheck");
        private static readonly bool DisplaySalePriceInfo = ParameterUtils.GetParameter<bool>("DisplaySalePriceInfo");
        private static readonly int SalePriceInfoTimeSpanBegin = ParameterUtils.GetParameter<int>("SalePriceInfoTimeSpanBegin");
        private static readonly int SalePriceInfoTimeSpanEnd = ParameterUtils.GetParameter<int>("SalePriceInfoTimeSpanEnd");
        private static readonly bool DisplaySalePriceTimeSpanInfo = ParameterUtils.GetParameter<bool>("DisplaySalePriceTimeSpanInfo");

        public ILogger Log { get; set; }

        public Runner(IFeedCmsProductArchiveEntryService feedCmsProductArchiveEntryService, IFeedRuleService feedRuleService, IIndigoCategoryService indigoCategoryService, IGoogleCategoryService googleCategoryService)
        {
            _feedCmsProductArchiveEntryService = feedCmsProductArchiveEntryService;
            _feedRuleService = feedRuleService;
            _indigoCategoryService = indigoCategoryService;
            _googleCategoryService = googleCategoryService;
        }

        public void Initialize(IExecutionLogLogger executionLogLogger, int plaFeedId, bool isIncremental, DateTime? fromTime, DateTime? effectiveFromTime, DateTime executionTime)
        {
            _executionLogLogger = executionLogLogger;
            _isIncrementalRun = isIncremental;
            _effectiveFromTime = effectiveFromTime;

            // Instantiate the IFeedGeneratorIndigoCategoryService 
            _feedGeneratorCategoryService = new FeedGeneratorIndigoCategoryService(_indigoCategoryService, _googleCategoryService, Log);

            // First get rules associated with this feed
            _runnerFeedRulesHelper = new GooglePlaFeedRuleHelper(_feedRuleService, _feedGeneratorCategoryService, _feedCmsProductArchiveEntryService, Log, AllowRuleOptimizations, AllowRuleEntryRemovals, AllowIEnumerableRuleEvaluations);
            _runnerFeedRulesHelper.Initialize(plaFeedId, isIncremental, fromTime, executionTime, GoogleRunFeedType.Google); 
        }

        public IExecutionLogLogger Execute()
        {
            if (_isIncrementalRun)
                Log.InfoFormat("Executing an incremental run with an effective from time of {0}.", _effectiveFromTime);

            // Build the range collection that will be used to generate the files dynamically
            var ranges = GetRanges();
            if (ranges.Any())
                Parallel.ForEach(ranges, new ParallelOptions { MaxDegreeOfParallelism = MaximumThreadsToUse }, ProcessRange);

            _executionLogLogger.HasError = _hasError;
            _executionLogLogger.SetExecutionEndTime(DateTime.Now);
                
            return _executionLogLogger;
        }

        private List<ProcessingInstruction> GetRanges()
        {
            var ranges = new List<ProcessingInstruction>(); 
            foreach (var instructionEntry in FeedGenerationInstructionsDictionary)
            {
                var isIncluded = bool.Parse(instructionEntry.Value["isincluded"]);
                var catalog = instructionEntry.Value["catalog"];
                if (!isIncluded)
                {
                    Log.InfoFormat("Catalog [{0}] excluded from feed generation", catalog);
                    continue;
                }

                var dbcmd = instructionEntry.Value["dbcmd"];
                var catalogattributesection = instructionEntry.Value["catalogattributesection"];
                var feedSplitter = instructionEntry.Value["splitter"];

                var baseProcessingInstruction = new ProcessingInstruction
                {
                    Catalog = catalog,
                    CatalogAttributesSection = catalogattributesection,
                    Dbcmd = dbcmd
                };

                foreach (var range in feedSplitter.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                {
                    baseProcessingInstruction.Range = range;
                    ranges.Add(baseProcessingInstruction);
                }
            }

            return ranges;
        }

        private void ProcessRange(ProcessingInstruction processingInstruction)
        {
            var sqlParameters = new SqlParameter[2];
            var pair = processingInstruction.Range.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            sqlParameters[0] = new SqlParameter("@PIDRangeStart", Convert.ToInt32(pair[0]));
            sqlParameters[1] = new SqlParameter("@PIDRangeEnd", Convert.ToInt32(pair[1]));
            var identifier = string.Format("{0}_{1}_{2}", processingInstruction.Catalog, sqlParameters[0].Value, sqlParameters[1].Value);
            try
            {
                var startDt = DateTime.Now;
                Log.DebugFormat("[{0}] start", identifier);
                WriteFeed(processingInstruction.Catalog, processingInstruction.Dbcmd, sqlParameters, identifier, processingInstruction.CatalogAttributesSection);
                var endDt = DateTime.Now;
                var execTime = endDt - startDt;
                Log.InfoFormat("[{0}] completed. Execution time in seconds: {1}", identifier, execTime.TotalSeconds);
                _executionLogLogger.AddFileGenerationUpdate(PlaRelatedFeedUtils.GetFeedFileName(identifier), true);
            }
            catch (Exception ex)
            {
                Log.InfoFormat("[Feed] {0}; error {1}", processingInstruction.Catalog + "-" + pair[0] + pair[1], ex);
                _executionLogLogger.AddFileGenerationUpdate(PlaRelatedFeedUtils.GetFeedFileName(identifier), false);
                _hasError = true;
            }
        }

        /// <summary>
        /// Generates feed file
        /// </summary>
        /// <param name="catalog">catalog: data source</param>
        /// <param name="commandText">stored procedure name</param>
        /// <param name="sqlParameters">parameter for stored procedure (null if none)</param>
        /// <param name="identifier">feed identifier (used for feed files names)</param>
        /// <param name="configSection">name of the configuration section containing catalog attributes</param>
        private void WriteFeed(string catalog, string commandText, SqlParameter[] sqlParameters, string identifier, string configSection)
        {
            using (var sqlConnection = new SqlConnection(OdysseyCommerceConnectionString))
            {
                sqlConnection.Open();
                using (var sqlCommand = new SqlCommand(commandText, sqlConnection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = SearchDataCommandTimeout
                })
                {
                    if (sqlParameters != null && sqlParameters.Length == 2 && !(sqlParameters[0].Value.ToString() == "0" && sqlParameters[1].Value.ToString() == "99"))
                        sqlCommand.Parameters.AddRange(sqlParameters);

                    if (LimitTo100Products)
                        sqlCommand.Parameters.AddWithValue("@GetTop100", 1);

                    if (_isIncrementalRun)
                    {
                        sqlCommand.Parameters.AddWithValue("@IsIncremental", 1);
                        sqlCommand.Parameters.AddWithValue("@DateChanged", _effectiveFromTime);
                    }

                    using (var sqlDataReader = sqlCommand.ExecuteReader())
                    {
                        if (sqlDataReader.HasRows)
                        {
                            WriteFeedFiles(catalog, sqlDataReader, identifier, configSection);
                        }
                    }//using sqldatareader
                } //using sqlCommand
            }
        }

        /// <summary>
        /// generates xml file(s) and archives it using gzip library
        /// </summary>
        /// <param name="catalog">catalog from which data is retrieved</param>
        /// <param name="reader">datareader</param>
        /// <param name="identifier">feed identifier used for file name</param>
        /// <param name="configSection">name of the configuration section containing catalog attributes</param>
        private void WriteFeedFiles(string catalog, IDataReader reader, string identifier, string configSection)
        {
            var dict = ConfigurationManager.GetSection(configSection) as StringDictionary;
            var feedFilePath = PlaRelatedFeedUtils.GetFeedFilePath(identifier, false, OutputFolderPath);

            Log.DebugFormat("[WriteFeedFile] {0} start", feedFilePath);
            //create gzip archive stream
            if (GzipFiles)
            {
                var gZipStream = new GZipStream(File.Create(feedFilePath + ".gz"), CompressionMode.Compress);
                var xmlWriter = XmlWriter.Create(gZipStream, new XmlWriterSettings {Indent = true});

                FeedXmlElement(xmlWriter, reader, dict, catalog, identifier, feedFilePath);

                xmlWriter.Close();
                gZipStream.Close();
            }
            else
            {
                var xmlWriter = XmlWriter.Create(feedFilePath, new XmlWriterSettings {Indent = true});
                FeedXmlElement(xmlWriter, reader, dict, catalog, identifier, feedFilePath);

                xmlWriter.Close();
            }
        }

        private void FeedXmlElement(XmlWriter xmlWriter, IDataReader reader, StringDictionary dict, string catalog, string identifier, string feedFilePath)
        {
            var counter = new ProcessingCounters();
            var time = _effectiveFromTime.HasValue ? _effectiveFromTime.Value : DateTime.Now;
            PlaRelatedFeedUtils.StartXmlDocument(xmlWriter, GoogleRunFeedType.Google, time); 

            //<entry>
            while (reader.Read())
            {
                Log.DebugFormat("{0}::Processing record [{1}]: {2}", identifier, (counter.GetTotalProcessed()), reader["PID"]);

                var id = reader[dict["gId"]].ToString();
                var title = reader[dict["title"]].ToString();
                try
                {
                    var haveExclusionRulesChanged = _runnerFeedRulesHelper.HaveExclusionRulesChanged();
                    var sku = reader[dict["sku"]].ToString();
                    var brandName = !dict.ContainsKey("gBrand") ? string.Empty : reader[dict["gBrand"]].ToString();
                    var cContributors = PlaRelatedFeedUtils.ContributorAttributes(dict, reader, id);
                    var contributor = cContributors ?? null;
                    var defaultCategory = FeedUtils.GetFeedGeneratorIndigoCategory(_feedGeneratorCategoryService, reader, dict, catalog, Log);
                    var productData = FeedUtils.GetProductData(dict, reader, sku, catalog, brandName, contributor, defaultCategory);
                    var sanitizedTitle = (string.IsNullOrWhiteSpace(title)) ? string.Empty : FeedUtils.SanitizeString(title);
                    var gAvailability = !dict.ContainsKey("gAvailability") ? FeedUtils.GetGoogleAvailability(1)
                            : FeedUtils.GetGoogleAvailability(int.Parse(reader[dict["gAvailability"]].ToString()));
                    var availability = !dict.ContainsKey("gAvailability") ? 1 : int.Parse(reader[dict["gAvailability"]].ToString());
                    var recordType = !dict.ContainsKey("recordType") ? string.Empty : reader[dict["recordType"]].ToString();
                    var hasImage = true;
                    if (!SkipHasImageCheck)
                        hasImage = (!dict.ContainsKey("hasImage")) || int.Parse(reader[dict["hasImage"]].ToString()) > 0;
                    
                    string message;
                    var isExcluded = false;
                    if (_isIncrementalRun)
                    {
                        var statusId = int.Parse(reader["StatusId"].ToString());
                        switch (statusId) 
                        {
                            // New product
                            case 1:
                                counter.NumberOfNew++;
                                continue;
                            // Unchanged product
                            case 3:
                                if (!haveExclusionRulesChanged)
                                {
                                    Log.DebugFormat("Product with id {0} is skipped in incremental mode as it wasn't modified and the rules haven't changed.", id);
                                    counter.NumberOfUnchanged++;
                                    continue;
                                }

                                var oldExclusionResult = _runnerFeedRulesHelper.IsExcludedFromFeed(productData, true);
                                var currentExclusionResult = _runnerFeedRulesHelper.IsExcludedFromFeed(productData, false);
                                if (oldExclusionResult == currentExclusionResult)
                                {
                                    Log.DebugFormat("Product with id {0} is skipped in incremental mode as it wasn't modified, rules had changed but exclusion rule evaluation's result remained the same.", id);
                                    counter.NumberOfUnchanged++;
                                    continue;
                                }

                                // If the product is excluded at the moment, then perform the "exclusion logic" per business requirements, 
                                // otherwise (i.e. product is included at the moment, treat it as "new")
                                if (!currentExclusionResult)
                                {
                                    Log.DebugFormat("Product with id {0} is marked as new and skipped in incremental mode as it wasn't modified, rules had changed but exclusion rule evaluation's result changed and currently product isn't excluded.", id);
                                    counter.NumberOfNew++;
                                    continue;
                                }

                                Log.DebugFormat("Product with id {0} is marked as excluded in incremental mode as it wasn't modified, rules had changed but exclusion rule evaluation's result changed and currently product is being excluded.", id);
                                isExcluded = true;
                                break;
                            // Modified product
                            case 2:
                                var isEntryExcluded = IndigoBreadcrumbRepositoryUtils.IsExcludedDueToData(GooglePlaFeedId, sanitizedTitle, hasImage, availability, recordType, false, out message);
                                if (isEntryExcluded)
                                {
                                    Log.DebugFormat("Product with id {0} is marked as excluded in incremental mode as it was modified, and it failed the data requirements for inclusion.", id);
                                    isExcluded = true;
                                    break;
                                }

                                // If product was excluded from the feed due to rules, then mark it as excluded
                                if (_runnerFeedRulesHelper.IsExcludedFromFeed(productData, false))
                                {
                                    Log.DebugFormat("Product with id {0} is marked as excluded in incremental mode as it was modified, and it's matching one of the exclusion rules.", id);
                                    isExcluded = true;
                                }
                                break;
                            default:
                                throw new ApplicationException("Invalid StatusId during an incremental run.");
                        }
                    }
                    else
                    {
                        isExcluded = IndigoBreadcrumbRepositoryUtils.IsExcludedDueToData(GooglePlaFeedId, sanitizedTitle, hasImage, availability, recordType, false, out message);
                        if (isExcluded)
                            Log.DebugFormat("Product with id {0} is marked as excluded in full mode as it failed the data requirements for inclusion.", id);
                        
                        if (!isExcluded)
                            isExcluded = _runnerFeedRulesHelper.IsExcludedFromFeed(productData, false);

                        if (isExcluded)
                            Log.DebugFormat("Product with id {0} is marked as excluded in full mode as it's matching one of the exclusion rules.", id);
                    }

                    // At this point, we know if the product is excluded or not, regardless of which type of run is being executed.
                    // If we aren't supposed to be sending excluded product data, then update the skipped counter and exit
                    if (isExcluded)
                    {
                        counter.NumberOfExcluded++;
                        if (!SendExcludedProductData)
                        {
                            Log.Debug("Skipped the product because it was excluded.");
                            continue;
                        }

                        gAvailability = ExcludedProductGoogleAvailabilityText;
                    }

                    var regularPrice = (decimal)reader[dict["price"]];
                    var adjustedPrice = string.IsNullOrEmpty(dict["adjustedPrice"]) ? "" : reader[dict["adjustedPrice"]].ToString();
                    decimal? salePrice = null;
                    if (!string.IsNullOrWhiteSpace(adjustedPrice))
                    {
                        var salePriceFromDatabase = Decimal.Parse(adjustedPrice);
                        if (salePriceFromDatabase != regularPrice)
                        {
                            if (salePriceFromDatabase > regularPrice)
                            {
                                regularPrice = salePriceFromDatabase;
                                salePrice = null;
                            }
                            else
                                salePrice = salePriceFromDatabase;
                        }
                    }
                    
                    var entry = EntryAttribute(id, regularPrice, salePrice, gAvailability);
                    entry.WriteTo(xmlWriter);
                    counter.NumberOfProcessed++;
                }
                catch (Exception e)
                {
                    counter.NumberOfErrored++;
                    var errorMessage = string.Format("Can't process the item. Id:{0};title:{1},catalog:{2},Message:{3}", id, title, catalog, e.Message);

                    Log.Error(errorMessage);

                    Log.DebugFormat("Error stack trace: {0}", e);
                    _executionLogLogger.AddCustomMessage(string.Format("Can't process the item. Id: {0};title: {1}, file identifier: {2}", id, title, identifier));
                    if (_isIncrementalRun && !AllowItemErrorsInFiles)
                        _hasError = true;
                }
            }

            // If the setting for sending deleted products is set to true in an incremental run, then get the deleted products since the last run
            // and send them as the "special" deleted products, i.e. pid + availability of "out of stock"
            if (_isIncrementalRun && SendExcludedProductData)
                AddDeletedProducts(xmlWriter, identifier, ref counter);

            PlaRelatedFeedUtils.EndXmlDocument(xmlWriter);
            var infoLogMessage = string.Format("[WriteFeedFile] {0} completed processed record count: {1}, error record count: {2}, unchanged record count: {3}, " +
                           "new record count: {4}, excluded record count: {5}, deleted record count: {6}. ",
                feedFilePath, counter.NumberOfProcessed, counter.NumberOfErrored, counter.NumberOfUnchanged, counter.NumberOfNew, counter.NumberOfExcluded, counter.NumberOfDeleted);

            if (SendExcludedProductData)
                infoLogMessage += "Excluded produces were included in processed count.";

            Log.Info(infoLogMessage);
        }

        private static XElement EntryAttribute(string gId, decimal price, decimal? salePrice, string gAvailability)
        {
            //entry
            var entry = new XElement(FeedXmlns + "entry");
            //g:id
            var gaId = new XElement(FeedXmlsnsG + "id", gId);
            entry.Add(gaId);

            //google search availability
            var gaAvailability = new XElement(FeedXmlsnsG + "availability", gAvailability);
            entry.Add(gaAvailability);

            // Price-related fields
            var gaPrice = new XElement(FeedXmlsnsG + "price", price.ToString("F", CultureInfo.InvariantCulture) + " CAD");
            entry.Add(gaPrice);
            if (DisplaySalePriceInfo)
            {
                if (salePrice.HasValue)
                {
                    var gaSalePrice = new XElement(FeedXmlsnsG + "sale_price", salePrice.Value.ToString("F", CultureInfo.InvariantCulture) + " CAD");
                    entry.Add(gaSalePrice);
                    if (DisplaySalePriceTimeSpanInfo)
                    {
                        var time = DateTime.Now.AddDays(SalePriceInfoTimeSpanBegin);
                        const string timeToStringFormat = "yyyy-MM-ddTHH:mmK";
                        entry.Add(new XElement(FeedXmlsnsG + "sale_price_effective_date",
                            string.Format("{0}/{1}", time.ToString(timeToStringFormat),
                                time.AddDays(SalePriceInfoTimeSpanEnd).ToString(timeToStringFormat))));
                    }
                }
                else
                {
                    var gaSalePrice = new XElement(FeedXmlsnsG + "sale_price", string.Empty);
                    entry.Add(gaSalePrice);
                    if (DisplaySalePriceTimeSpanInfo)
                    {
                        entry.Add(new XElement(FeedXmlsnsG + "sale_price_effective_date", string.Empty));
                    }
                }
            }

            return entry;
        }

        private struct ProcessingInstruction
        {
            public string Range { get; set; }
            public string Catalog { get; set; }
            public string Dbcmd { get; set; }
            public string CatalogAttributesSection { get; set; }
        }

        private struct ProcessingCounters
        {
            public int NumberOfProcessed { get; set; }
            public int NumberOfNew { get; set; }
            public int NumberOfDeleted { get; set; }
            public int NumberOfExcluded { get; set; }
            public int NumberOfUnchanged { get; set; }
            public int NumberOfErrored { get; set; }

            public int GetTotalProcessed()
            {
                return NumberOfProcessed + NumberOfNew + NumberOfDeleted + NumberOfUnchanged + NumberOfErrored;
            }
        }

        #region Deleted Product Processing
        private void AddDeletedProducts(XmlWriter xmlWriter, string identifier, ref ProcessingCounters counter)
        {
            var parts = identifier.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
                throw new ArgumentException("Invalid identifier - length", identifier);

            var catalogName = parts[0];
            var rangeStart = int.Parse(parts[1]);
            var rangeEnd = int.Parse(parts[2]);
            string deletedStoredProcedureName;
            switch (catalogName)
            {
                case "Books":
                    deletedStoredProcedureName = DeletedBooksStoredProcedureName;
                    break;
                case "GeneralMerchandise":
                    deletedStoredProcedureName = DeletedGeneralMerchandiseStoredProcedureName;
                    break;
                default:
                    throw new ArgumentException("Invalid identifier - catalog name", identifier);
            }

            WriteDeletedProductNodes(xmlWriter, deletedStoredProcedureName, rangeStart, rangeEnd, ref counter);
        }

        private void WriteDeletedProductNodes(XmlWriter xmlWriter, string storedProcedureName, int rangeStart, int rangeEnd, ref ProcessingCounters counter)
        {
            Log.InfoFormat("Starting to write deleted product nodes for SP of {0}, rangeStart of {1}, rangeEnd of {2}.", storedProcedureName, rangeStart, rangeEnd);
            using (var sqlConnection = new SqlConnection(OdysseyCommerceConnectionString))
            {
                sqlConnection.Open();
                using (var sqlCommand = new SqlCommand(storedProcedureName, sqlConnection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = SearchDataCommandTimeout
                })
                {
                    sqlCommand.Parameters.AddWithValue("@PIDRangeStart", rangeStart);
                    sqlCommand.Parameters.AddWithValue("@PIDRangeEnd", rangeEnd);
                    sqlCommand.Parameters.AddWithValue("@DateChanged", _effectiveFromTime);

                    using (var sqlDataReader = sqlCommand.ExecuteReader())
                    {
                        if (sqlDataReader.HasRows)
                        {
                            while (sqlDataReader.Read())
                            {
                                WriteDeletedProductNode(xmlWriter, sqlDataReader["Pid"].ToString());
                                counter.NumberOfDeleted++;
                            }
                        }
                    }//using sqldatareader
                } //using sqlCommand
            }
            Log.InfoFormat("Completed writing deleted product nodes for SP of {0}, rangeStart of {1}, rangeEnd of {2}.", storedProcedureName, rangeStart, rangeEnd);
        }

        private static void WriteDeletedProductNode(XmlWriter xmlWriter, string id)
        {
            //entry
            var entry = new XElement(FeedXmlsnsG + "entry");
            //g:id
            var gaId = new XElement(FeedXmlsnsG + "Id", id);
            entry.Add(gaId);

            var gaAvailability = new XElement(FeedXmlsnsG + "Availability", ExcludedProductGoogleAvailabilityText);
            entry.Add(gaAvailability);

            entry.WriteTo(xmlWriter);
        }
        #endregion
    }
}
