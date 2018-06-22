using Castle.Core.Logging;
using DynamicCampaignsFeedGenerator.Utils;
using FeedGenerators.Core.Execution;
using FeedGenerators.Core.Services.Abstract;
using FeedGenerators.Core.Services.Concrete;
using FeedGenerators.Core.Types;
using FeedGenerators.Core.Utils;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Types;
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

namespace DynamicCampaignsFeedGenerator.Execution
{
    public class Runner : IRunner
    {
        private IFeedGeneratorIndigoCategoryService _feedGeneratorCategoryService;
        private IGooglePlaFeedRuleHelper _runnerFeed;
        private IExecutionLogLogger _executionLogLogger;
        private bool _hasError;
        private int _dynamicMerchLabelProductCount;
        private readonly object _syncRoot = new Object();

        private readonly IFeedRuleService _feedRuleService;
        private readonly IFeedCmsProductArchiveEntryService _feedCmsProductArciveEntryService;
        private readonly IIndigoCategoryService _indigoCategoryService;
        private readonly IGoogleCategoryService _googleCategoryService;
        
        private static readonly bool LimitTo100Products = ParameterUtils.GetParameter<bool>("DynamicCampaignsFeedGenerator.LimitTo100Products");
        private static readonly bool AllowItemErrorsInFiles = ParameterUtils.GetParameter<bool>("AllowItemErrorsInFiles");
        private static readonly bool AllowRuleOptimizations = ParameterUtils.GetParameter<bool>("AllowRuleOptimizations");
        private static readonly bool AllowRuleEntryRemovals = ParameterUtils.GetParameter<bool>("AllowRuleEntryRemovals");
        private static readonly bool AllowIEnumerableRuleEvaluations = ParameterUtils.GetParameter<bool>("AllowIEnumerableRuleEvaluations");
        private static readonly bool GzipFiles = ParameterUtils.GetParameter<bool>("DynamicCampaignsFeedGenerator.GzipFiles");
        private static readonly string OdysseyCommerceConnectionString = ConfigurationManager.ConnectionStrings["OdysseyCommerceDB"].ConnectionString;
        private static readonly XNamespace FeedXmlns = ParameterUtils.GetParameter<string>("FeedXmlns");
        private static readonly string RedirectUrlSuffix = ParameterUtils.GetParameter<string>("RedirectUrlSuffix");
        private static readonly Dictionary<string, Dictionary<string, string>> FeedGenerationInstructionsDictionary = ConfigurationManager.GetSection("feedgenerationinstructiondict") as Dictionary<string, Dictionary<string, string>>;
        private static readonly int SearchDataCommandTimeout = ParameterUtils.GetParameter<int>("SearchDataCommandTimeout");
        private static readonly int MaximumThreadsToUse = ParameterUtils.GetParameter<int>("MaximumThreadsToUse");
        private static readonly int GooglePlaFeedId = ParameterUtils.GetParameter<int>("GooglePlaFeedId");
        private static readonly bool SkipHasImageCheck = ParameterUtils.GetParameter<bool>("DynamicCampaignsFeedGenerator.SkipHasImageCheck");

        private const string CustomLabelCountMessageFormat = "{0} products got assigned a dynamic merch label value.";

        public ILogger Log { get; set; }

        public Runner(IFeedCmsProductArchiveEntryService feedCmsProductArciveEntryService, IFeedRuleService feedRuleService, IIndigoCategoryService indigoCategoryService, IGoogleCategoryService googleCategoryService)
        {
            _feedCmsProductArciveEntryService = feedCmsProductArciveEntryService;
            _feedRuleService = feedRuleService;
            _indigoCategoryService = indigoCategoryService;
            _googleCategoryService = googleCategoryService;
        }

        public void Initialize(IExecutionLogLogger executionLogLogger, int feedId)
        {
            _executionLogLogger = executionLogLogger;

            // Instantiate the IFeedGeneratorIndigoCategoryService 
            _feedGeneratorCategoryService = new FeedGeneratorIndigoCategoryService(_indigoCategoryService, _googleCategoryService, Log);

            // First get rules associated with this feed
            _runnerFeed = new GooglePlaFeedRuleHelper(_feedRuleService, _feedGeneratorCategoryService, _feedCmsProductArciveEntryService, Log, AllowRuleOptimizations, AllowRuleEntryRemovals, AllowIEnumerableRuleEvaluations);
            _runnerFeed.Initialize(GooglePlaFeedId, false, null, executionLogLogger.GetExecutionStartTime(), GoogleRunFeedType.Google);
        }

        public IExecutionLogLogger Execute()
        {
            // Build the range collection that will be used to generate the files dynamically
            var ranges = GetRanges();
            if (ranges.Any())
                Parallel.ForEach(ranges, new ParallelOptions { MaxDegreeOfParallelism = MaximumThreadsToUse }, ProcessRange);

            _executionLogLogger.HasError = _hasError;
            _executionLogLogger.SetExecutionEndTime(DateTime.Now);
            _executionLogLogger.AddCustomMessage(string.Format(CustomLabelCountMessageFormat, _dynamicMerchLabelProductCount));
                
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
                //var sqlParameters = new SqlParameter[2];

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
                _executionLogLogger.AddFileGenerationUpdate(XmlDataUtils.GetFeedFileName(identifier), true);
            }
            catch (Exception ex)
            {
                Log.InfoFormat("[Feed] {0}; error {1}", processingInstruction.Catalog + "-" + pair[0] + pair[1], ex);
                _executionLogLogger.AddFileGenerationUpdate(XmlDataUtils.GetFeedFileName(identifier), false);
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
                    {
                        sqlCommand.Parameters.AddRange(sqlParameters);
                    }

                    if (LimitTo100Products)
                    {
                        sqlCommand.Parameters.AddWithValue("@GetTop100", 1);
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
            var feedFilePath = XmlDataUtils.GetFeedFilePath(identifier, false);

            Log.DebugFormat("[WriteFeedFile] {0} start", feedFilePath);
            var countRec = new Tuple<int, int, int>(0, 0, 0);
            //create gzip archive stream
            if (GzipFiles)
            {
                var gZipStream = new GZipStream(File.Create(feedFilePath + ".gz"), CompressionMode.Compress);
                var xmlWriter = XmlWriter.Create(gZipStream, new XmlWriterSettings {Indent = true});

                FeedXmlElement(xmlWriter, reader, dict, catalog, identifier, feedFilePath, ref countRec);

                xmlWriter.Close();
                gZipStream.Close();
            }
            else
            {
                var xmlWriter = XmlWriter.Create(feedFilePath, new XmlWriterSettings {Indent = true});
                FeedXmlElement(xmlWriter, reader, dict, catalog, identifier, feedFilePath, ref countRec);

                xmlWriter.Close();
            }

            Log.DebugFormat("[WriteFeedFile] {0} complete.  Total written records: {1}; Error records: {2}, skipped records: {3}.", feedFilePath,
                                               countRec.Item1, countRec.Item2, countRec.Item3);
        }

        private void FeedXmlElement(XmlWriter xmlWriter, IDataReader reader, StringDictionary dict, string catalog, string identifier, string feedFilePath, ref Tuple<int, int, int> counter)
        {
            if (counter == null) throw new ArgumentNullException("counter");
            var countProcessed = 0;
            var countError = 0;
            var countSkipped = 0;
            var dynamicMerchLabelProductCount = 0;

            XmlDataUtils.StartXmlDocument(xmlWriter); 

            //<entry>
            while (reader.Read())
            {
                Log.DebugFormat("{0}::Processing record [{1}]: {2}", identifier, (countProcessed + countError), reader["PID"]);

                var id = reader[dict["gId"]].ToString();
                var title = reader[dict["title"]].ToString();
                try
                {
                    var sku = reader[dict["sku"]].ToString();
                    // Interesting business choice: for books, first author. For GM, real brand.
                    var marinBrand = reader[dict["gMarinBrand"]].ToString();
                    var brandName = !dict.ContainsKey("gBrand") ? string.Empty : reader[dict["gBrand"]].ToString();
                    var cContributors = PlaRelatedFeedUtils.ContributorAttributes(dict, reader, id);
                    var contributor = cContributors ?? null;
                    var defaultCategory = FeedUtils.GetFeedGeneratorIndigoCategory(_feedGeneratorCategoryService, reader, dict, catalog, Log);
                    var productData = FeedUtils.GetProductData(dict, reader, sku, catalog, brandName, contributor, defaultCategory);
                    marinBrand = XmlDataUtils.GetBrand(marinBrand, catalog);
                    var formatString = dict.ContainsKey("Format") ? FeedUtils.GetFormat(reader[dict["Format"]].ToString(), id) : string.Empty;
                    var sanitizedTitle = (string.IsNullOrWhiteSpace(title)) ? string.Empty : FeedUtils.SanitizeString(string.IsNullOrWhiteSpace(formatString) ? title : $"{title}-{formatString}");
                    var quantity = reader[dict["quantity"]].ToString();
                    var gAvailability = !dict.ContainsKey("gAvailability") ? FeedUtils.GetGoogleAvailability(1) : FeedUtils.GetGoogleAvailability((int)reader[dict["gAvailability"]]);
                    var availability = !dict.ContainsKey("gAvailability") ? 1 : (int)reader[dict["gAvailability"]];
                    var recordType = !dict.ContainsKey("recordType") ? string.Empty : reader[dict["recordType"]].ToString();
                    var hasImage = true;
                    if (!SkipHasImageCheck)
                        hasImage = (!dict.ContainsKey("hasImage")) || int.Parse(reader[dict["hasImage"]].ToString()) > 0;
                    var size = !dict.ContainsKey("size") ? string.Empty : reader[dict["size"]].ToString();
                    var colour = !dict.ContainsKey("colour") ? string.Empty : reader[dict["colour"]].ToString();
                    var style = !dict.ContainsKey("style") ? string.Empty : reader[dict["style"]].ToString();
                    var scent = !dict.ContainsKey("scent") ? string.Empty : reader[dict["scent"]].ToString();
                    var flavour = !dict.ContainsKey("flavour") ? string.Empty : reader[dict["flavour"]].ToString();
                    var bindingType = !dict.ContainsKey("Format") ? string.Empty : FeedUtils.GetFormat(reader[dict["Format"]].ToString(), "");
                    var familyId = !dict.ContainsKey("FamilyId") ? string.Empty : reader[dict["FamilyId"]].ToString();

                    var dynamicMerchLabel = string.Empty;
                    var hasLoadedDynamicMerchLabelData = false; 
                    string message;
                    var isEntryExcluded = IndigoBreadcrumbRepositoryUtils.IsExcludedDueToData(GooglePlaFeedId, sanitizedTitle, hasImage, availability, recordType, false, out message);

                    // If entry is excluded, then there is no need to process anything further
                    if (isEntryExcluded)
                    {
                        // Business requested that any product that has a dynamic merch label value will be included in the feed, regardless of its data
                        dynamicMerchLabel = PlaRelatedFeedUtils.GetDynamicMerchLabelValue(_runnerFeed, productData);
                        hasLoadedDynamicMerchLabelData = true;
                        if (string.IsNullOrWhiteSpace(dynamicMerchLabel))
                        {
                            countSkipped++;
                            continue;
                        }
                    }

                    // At this point, check if the product is excluded from the feed due to an exclusion rule, if so, skip
                    if (_runnerFeed.IsExcludedFromFeed(productData, false))
                    {
                        countSkipped++;
                        continue;
                    }

                    if (!hasLoadedDynamicMerchLabelData)
                    {
                        dynamicMerchLabel = PlaRelatedFeedUtils.GetDynamicMerchLabelValue(_runnerFeed, productData);
                    }

                    if (!string.IsNullOrWhiteSpace(dynamicMerchLabel))
                        dynamicMerchLabelProductCount++; 

                    var googleCategoryBreadcrumb = (string.IsNullOrWhiteSpace(defaultCategory.GoogleCategoryBreadcrumb)) ? dict["gGoogleProductCategory"] : defaultCategory.GoogleCategoryBreadcrumb;
                    var customLabel0 = PlaRelatedFeedUtils.GetCustomLabelValue(_runnerFeed, productData, FeedRuleType.Custom_Label_0);
                    var customLabel1 = PlaRelatedFeedUtils.GetCustomLabelValue(_runnerFeed, productData, FeedRuleType.Custom_Label_1);
                    var customLabel2 = PlaRelatedFeedUtils.GetCustomLabelValue(_runnerFeed, productData, FeedRuleType.Custom_Label_2);
                    var customLabel3 = PlaRelatedFeedUtils.GetCustomLabelValue(_runnerFeed, productData, FeedRuleType.Custom_Label_3);
                    var customLabel4 = PlaRelatedFeedUtils.GetCustomLabel4Value(_runnerFeed, productData, dict, reader);

                    var regularPrice = (decimal)reader[dict["price"]];
                    var effectivePrice = regularPrice;
                    decimal? gSavingDollars = null;
                    decimal? gSavingsPercentage = null;
                    var adjustedPrice = string.IsNullOrEmpty(dict["adjustedPrice"]) ? "" : reader[dict["adjustedPrice"]].ToString();
                    var isOnSale = false;
                    if (!string.IsNullOrWhiteSpace(adjustedPrice))
                    {
                        decimal salePrice = Decimal.Parse(adjustedPrice);
                        if (salePrice != regularPrice)
                        {
                            if (salePrice > regularPrice)
                            {
                                regularPrice = salePrice;
                            }
                            else
                            {
                                isOnSale = true;
                            }

                            effectivePrice = salePrice;
                        }
                    }

                    // finally calculate the sale-related values
                    if (isOnSale)
                    {
                        gSavingDollars = regularPrice - effectivePrice;
                        gSavingsPercentage = 100 - effectivePrice * 100 / regularPrice;
                    }

                    var linkCatalog = dict["linkCatalog"];

                    var entry = EntryAttribute(reader
                        , id
                        , sanitizedTitle
                        , sku
                        , effectivePrice
                        , gSavingDollars
                        , gSavingsPercentage
                        , gAvailability
                        , marinBrand
                        , quantity
                        , linkCatalog, googleCategoryBreadcrumb, defaultCategory.Breadcrumb, customLabel0, customLabel1, customLabel2, customLabel3, customLabel4, dynamicMerchLabel
                        , size, colour, style, scent, flavour, bindingType, familyId
                        );

                    entry.WriteTo(xmlWriter);

                    countProcessed++;
                }
                catch (Exception e)
                {
                    countError++;
                    var errorMessage = string.Format("Can't process the item. Id:{0};title:{1},catalog:{2},Message:{3}", id, title, catalog, e.Message);

                    Log.Error(errorMessage);

                    Log.DebugFormat("Error stack trace: {0}", e);
                    _executionLogLogger.AddCustomMessage(string.Format("Can't process the item. Id: {0};title: {1}, file identifier: {2}", id, title, identifier));
                    if (!AllowItemErrorsInFiles)
                        _hasError = true;
                }
            }

            XmlDataUtils.EndXmlDocument(xmlWriter);
            counter = new Tuple<int, int, int>(countProcessed, countError, countSkipped);
            AddToDynamicMerchProductCount(dynamicMerchLabelProductCount);

            Log.InfoFormat("[WriteFeedFile] {0} completed Processed record count: {1}, Error record count: {2}, skipped record count: {3}", feedFilePath, countProcessed, countError, countSkipped);
        }

        private void AddToDynamicMerchProductCount(int dynamicMerchLabelProductCount)
        {
            lock (_syncRoot)
            {
                _dynamicMerchLabelProductCount += dynamicMerchLabelProductCount; 
            }   
        }

        private static XElement EntryAttribute(IDataReader reader
            , string gId
            , string sanitizedTitle
            , string sku
            , decimal gCurrentPrice
            , decimal? gSavingDollars
            , decimal? gSavingsPercentage
            , string gAvailability
            , string gBrand
            , string quantity
            , string linkCatalog, string googleCategoryBreadcrumb, string indigoBreadcrumb, string customLabel0, 
            string customLabel1, 
            string customLabel2,
            string customLabel3,
            string customLabel4, string dynamicMerchLabel,
            string size , string colour, string style, string scent, string flavour, string bindingType, 
            string familyId
            )
        {
            //entry
            var entry = new XElement(FeedXmlns + "entry");
            //g:id
            var gaId = new XElement(FeedXmlns + "Id", gId);
            entry.Add(gaId);
            //sku
            var gSku = new XElement(FeedXmlns + "Sku", sku);
            entry.Add(gSku);
            //title
            var aTitle = new XElement(FeedXmlns + "Title", new XCData(sanitizedTitle));
            entry.Add(aTitle);

            //quantity
            var gQuantity = new XElement(FeedXmlns + "StockCount", quantity);
            entry.Add(gQuantity);

            //link
            var url = XmlDataUtils.GetFeedEntryLinkValue(reader, linkCatalog, sku);
            var aLink = XmlDataUtils.FeedEntryLink(url + RedirectUrlSuffix); 
            entry.Add(aLink);            

            //google search availability
            var gaAvailability = new XElement(FeedXmlns + "Availability", gAvailability);
            entry.Add(gaAvailability);

            // Price-related fields
            var gaPrice = new XElement(FeedXmlns + "CurrentPrice",
                gCurrentPrice.ToString("F", CultureInfo.InvariantCulture));
            entry.Add(gaPrice);

            var savingsDollarsValue = (gSavingDollars.HasValue) ? gSavingDollars.Value.ToString("F", CultureInfo.InvariantCulture): String.Empty;
            var gaSavingDollars = new XElement(FeedXmlns + "SavingDollars", savingsDollarsValue);
            entry.Add(gaSavingDollars);

            var savingPercentageValue = (gSavingsPercentage.HasValue) ? gSavingsPercentage.Value.ToString("#") + "%" : String.Empty;
            var gaSavingsPercentage = new XElement(FeedXmlns + "SavingsPercentage", savingPercentageValue);
            entry.Add(gaSavingsPercentage);

            entry.Add(new XElement(FeedXmlns + "Brand", gBrand.Trim()));

            // Adding XML nodes that are populated from Google PLA feed generator data 
            entry.Add(new XElement(FeedXmlns + "GoogleCategory", googleCategoryBreadcrumb));
            entry.Add(new XElement(FeedXmlns + "IndigoCategory", indigoBreadcrumb));
            entry.Add(new XElement(FeedXmlns + "CustomLabel0", customLabel0));
            entry.Add(new XElement(FeedXmlns + "CustomLabel1", customLabel1));
            entry.Add(new XElement(FeedXmlns + "CustomLabel2", customLabel2));
            entry.Add(new XElement(FeedXmlns + "CustomLabel3", customLabel3));
            entry.Add(new XElement(FeedXmlns + "CustomLabel4", customLabel4));
            entry.Add(new XElement(FeedXmlns + "DynamicMarinLabel", dynamicMerchLabel));
            entry.Add(new XElement(FeedXmlns + "Size", size));
            entry.Add(new XElement(FeedXmlns + "Colour", colour));
            entry.Add(new XElement(FeedXmlns + "Style", style));
            entry.Add(new XElement(FeedXmlns + "Scent", scent));
            entry.Add(new XElement(FeedXmlns + "Flavour", flavour));
            entry.Add(new XElement(FeedXmlns + "Format", bindingType));
            entry.Add(new XElement(FeedXmlns + "FamilyId", familyId));

            return entry;
        }

        private struct ProcessingInstruction
        {
            public string Range { get; set; }
            public string Catalog { get; set; }
            public string Dbcmd { get; set; }
            public string CatalogAttributesSection { get; set; }
        }
    }
}
