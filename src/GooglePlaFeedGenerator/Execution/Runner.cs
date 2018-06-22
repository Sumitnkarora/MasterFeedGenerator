using Castle.Core.Logging;
using FeedGenerators.Core.Execution;
using FeedGenerators.Core.Services.Abstract;
using FeedGenerators.Core.Services.Concrete;
using FeedGenerators.Core.Types;
using FeedGenerators.Core.Utils;
using GooglePlaFeedGenerator.Types;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Types;
using Indigo.Feeds.Utils;
using System;
using System.Collections.Concurrent;
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

namespace GooglePlaFeedGenerator.Execution
{
    public class Runner : IRunner
    {
        private IFeedGeneratorIndigoCategoryService _feedGeneratorCategoryService;
        private IExecutionLogLogger _executionLogLogger;
        private IExecutionLogLogger _executionLogLoggerSecondary;
        private RunMode _runMode = RunMode.Primary;
        private bool _hasError;
        private IGooglePlaFeedRuleHelper _runnerFeed;
        private IGooglePlaFeedRuleHelper _runnerFeedSecondary;
        private int _feedId;
        private readonly ConcurrentBag<DefaultCpcProductInfo> _defaultCpcProductInfos = new ConcurrentBag<DefaultCpcProductInfo>();

        private readonly IFeedRuleService _feedRuleService;
        private readonly IFeedCmsProductArchiveEntryService _feedCmsProductArciveEntryService;
        private readonly IIndigoCategoryService _indigoCategoryService;
        private readonly IGoogleCategoryService _googleCategoryService;

        private static readonly string OutputFolderPath = ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.OutputFolderPath");
        private static readonly bool LimitTo100Products = ParameterUtils.GetParameter<bool>("GooglePlaFeedGenerator.LimitTo100Products");
        private static readonly bool AllowItemErrorsInFiles = ParameterUtils.GetParameter<bool>("AllowItemErrorsInFiles");
        private static readonly bool AllowRuleOptimizations = ParameterUtils.GetParameter<bool>("AllowRuleOptimizations");
        private static readonly bool AllowRuleEntryRemovals = ParameterUtils.GetParameter<bool>("AllowRuleEntryRemovals");
        private static readonly bool AllowIEnumerableRuleEvaluations = ParameterUtils.GetParameter<bool>("AllowIEnumerableRuleEvaluations");
        private static readonly bool GzipFiles = ParameterUtils.GetParameter<bool>("GooglePlaFeedGenerator.GzipFiles");
        private static readonly string OdysseyCommerceConnectionString = ConfigurationManager.ConnectionStrings["OdysseyCommerceDB"].ConnectionString;
        private static readonly XNamespace FeedXmlsnsG = ParameterUtils.GetParameter<string>("FeedXmlsnsG");
        private static readonly XNamespace FeedXmlns = ParameterUtils.GetParameter<string>("FeedXmlns");
        private static readonly bool SkipHasImageCheck = ParameterUtils.GetParameter<bool>("GooglePlaFeedGenerator.SkipHasImageCheck");
        private static readonly string GoogleAdwordsRedirectUrlSuffix = ParameterUtils.GetParameter<string>("GoogleAdwordsRedirectUrlSuffix");
        private static readonly string YahooItemPageUrlSuffix = ParameterUtils.GetParameter<string>("YahooItemPageUrlSuffix");
        private static readonly bool DisplaySalePriceInfo = ParameterUtils.GetParameter<bool>("DisplaySalePriceInfo");
        private static readonly int SalePriceInfoTimeSpanBegin = ParameterUtils.GetParameter<int>("SalePriceInfoTimeSpanBegin");
        private static readonly int SalePriceInfoTimeSpanEnd = ParameterUtils.GetParameter<int>("SalePriceInfoTimeSpanEnd");
        private static readonly bool DisplaySalePriceTimeSpanInfo = ParameterUtils.GetParameter<bool>("DisplaySalePriceTimeSpanInfo");
        private static readonly Dictionary<string, Dictionary<string, string>> FeedGenerationInstructionsDictionary = ConfigurationManager.GetSection("feedgenerationinstructiondict") as Dictionary<string, Dictionary<string, string>>;
        private static readonly int SearchDataCommandTimeout = ParameterUtils.GetParameter<int>("SearchDataCommandTimeout");
        private static readonly int MaxTitleLength = ParameterUtils.GetParameter<int>("MaxTitleLength");
        private static readonly bool DisplayDefaultShoppingInfo = ParameterUtils.GetParameter<bool>("DisplayDefaultShoppingInfo");
        private static readonly string AncillaryOutputFolderPath = ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.AncillaryOutputFolderPath");
        private static readonly string DefaultCpcsFileName = ParameterUtils.GetParameter<string>("DefaultCpcsFileName");
        private static readonly int MaximumThreadsToUse = ParameterUtils.GetParameter<int>("MaximumThreadsToUse");
        private static readonly int MaximumBreadcrumbsToSend = ParameterUtils.GetParameter<int>("MaximumBreadcrumbsToSend");

        public ILogger Log { get; set; }
        public Runner(IFeedCmsProductArchiveEntryService feedCmsProductArciveEntryService, IFeedRuleService feedRuleService, IIndigoCategoryService indigoCategoryService, IGoogleCategoryService googleCategoryService)
        {
            _feedCmsProductArciveEntryService = feedCmsProductArciveEntryService;
            _feedRuleService = feedRuleService;
            _indigoCategoryService = indigoCategoryService;
            _googleCategoryService = googleCategoryService;
        }

        public void Initialize(IExecutionLogLogger executionLogLogger, IExecutionLogLogger secondaryExecutionLogger, int feedId, int? secondaryRunId)
        {
            _executionLogLogger = executionLogLogger;
            _executionLogLoggerSecondary = secondaryExecutionLogger;
            _feedId = feedId;

            // Instantiate the IFeedGeneratorIndigoCategoryService 
            _feedGeneratorCategoryService = new FeedGeneratorIndigoCategoryService(_indigoCategoryService, _googleCategoryService, Log);

            // First get rules associated with this feed
            _runnerFeed = new GooglePlaFeedRuleHelper(_feedRuleService, _feedGeneratorCategoryService, _feedCmsProductArciveEntryService, Log, AllowRuleOptimizations, AllowRuleEntryRemovals, AllowIEnumerableRuleEvaluations);
            _runnerFeed.Initialize(feedId, false, null, executionLogLogger.GetExecutionStartTime(), GoogleRunFeedType.Google);
            if (secondaryRunId.HasValue)
            {
                _runMode = RunMode.PrimaryAndSecondary;
                _runnerFeedSecondary = new GooglePlaFeedRuleHelper(_feedRuleService, _feedGeneratorCategoryService, _feedCmsProductArciveEntryService, Log, AllowRuleOptimizations, AllowRuleEntryRemovals, AllowIEnumerableRuleEvaluations);
                _runnerFeedSecondary.Initialize(secondaryRunId.Value, false, null, executionLogLogger.GetExecutionStartTime(), GoogleRunFeedType.Yahoo);
            }
        }

        public IEnumerable<IExecutionLogLogger> Execute()
        {
            // Build the range collection that will be used to generate the files dynamically
            var ranges = GetRanges();
            if (ranges.Any())
                Parallel.ForEach(ranges, new ParallelOptions { MaxDegreeOfParallelism = MaximumThreadsToUse }, ProcessRange);

            _executionLogLogger.HasError = _hasError;
            _executionLogLogger.SetExecutionEndTime(DateTime.Now);

            var defaultCpcMessage = string.Empty;
            // Create a text file with the items that didn't have merchandise types assigned 
            if (_defaultCpcProductInfos.Any())
            {
                defaultCpcMessage = string.Format("There were {0} products that weren assigned to default CPC value. IT has the list.", _defaultCpcProductInfos.Count);
                _executionLogLogger.AddCustomMessage(defaultCpcMessage);
                Log.Info(defaultCpcMessage);
                ProcessDefaultCpcProductInfos();
            }

            var result = new List<IExecutionLogLogger> {_executionLogLogger};
            if (HasTwoGenerators())
            {
                if (!string.IsNullOrWhiteSpace(defaultCpcMessage))
                    _executionLogLoggerSecondary.AddCustomMessage(defaultCpcMessage);

                _executionLogLoggerSecondary.HasError = _hasError; 
                _executionLogLoggerSecondary.SetExecutionEndTime(_executionLogLogger.GetExecutionEndTime());
                result.Add(_executionLogLoggerSecondary);
            }
                
            return result;
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

        private bool HasTwoGenerators()
        {
            return _runMode == RunMode.PrimaryAndSecondary;
        }

        private void ProcessRange(ProcessingInstruction processingInstruction)
        {
            var sqlParameters = new SqlParameter[2];
            var pair = processingInstruction.Range.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            sqlParameters[0] = new SqlParameter("@PIDRangeStart", Convert.ToInt32(pair[0]));
            sqlParameters[1] = new SqlParameter("@PIDRangeEnd ", Convert.ToInt32(pair[1]));
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
                if (HasTwoGenerators())
                    _executionLogLoggerSecondary.AddFileGenerationUpdate(PlaRelatedFeedUtils.GetFeedFileName(identifier), false);
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
            var feedFilePath = PlaRelatedFeedUtils.GetFeedFilePath(identifier, false, OutputFolderPath);
            var feedFilePathSecondary = (HasTwoGenerators()) ? PlaRelatedFeedUtils.GetFeedFilePath(identifier, true, AncillaryOutputFolderPath) : string.Empty;

            Log.DebugFormat("[WriteFeedFile] {0} start", feedFilePath);
            var countRec = new Tuple<int, int, int>(0, 0, 0);
            //create gzip archive stream
            if (GzipFiles)
            {
                var gZipStream = new GZipStream(File.Create(feedFilePath + ".gz"), CompressionMode.Compress);
                var xmlWriter = XmlWriter.Create(gZipStream, new XmlWriterSettings {Indent = true});
                GZipStream gZipStreamSecondary = null;
                XmlWriter xmlWriterSecondary = null;
                if (HasTwoGenerators())
                {
                    gZipStreamSecondary = new GZipStream(File.Create(feedFilePathSecondary + ".gz"), CompressionMode.Compress);
                    xmlWriterSecondary = XmlWriter.Create(gZipStreamSecondary, new XmlWriterSettings { Indent = true });
                }

                FeedXmlElement(xmlWriter, xmlWriterSecondary, reader, dict, catalog, identifier, feedFilePath, ref countRec);

                xmlWriter.Close();
                if (xmlWriterSecondary != null)
                    xmlWriterSecondary.Close();

                gZipStream.Close();
                if (gZipStreamSecondary != null) 
                    gZipStreamSecondary.Close();
            }
            else
            {
                var xmlWriter = XmlWriter.Create(feedFilePath, new XmlWriterSettings {Indent = true});
                XmlWriter xmlWriterSecondary = null;
                if (HasTwoGenerators())
                    xmlWriterSecondary = XmlWriter.Create(feedFilePathSecondary, new XmlWriterSettings {Indent = true});

                FeedXmlElement(xmlWriter, xmlWriterSecondary, reader, dict, catalog, identifier, feedFilePath, ref countRec);

                xmlWriter.Close();
                if (xmlWriterSecondary != null)
                    xmlWriterSecondary.Close();
            }

            Log.DebugFormat("[WriteFeedFile] {0} complete.  Total written records: {1}; Error records: {2}, skipped records: {3}.", feedFilePath,
                                               countRec.Item1, countRec.Item2, countRec.Item3);
        }

        private void FeedXmlElement(XmlWriter xmlWriter, XmlWriter xmlWriterSecondary, IDataReader reader, StringDictionary dict, string catalog, string identifier, string feedFilePath, ref Tuple<int, int, int> counter)
        {
            if (counter == null) throw new ArgumentNullException("counter");
            var countProcessed = 0;
            var countError = 0;
            var countSkipped = 0;

            var hasTwoGenerators = HasTwoGenerators();
            var time = DateTime.Now;
            PlaRelatedFeedUtils.StartXmlDocument(xmlWriter, _runnerFeed.GetRunFeedType(), time); 
            if (hasTwoGenerators)
                PlaRelatedFeedUtils.StartXmlDocument(xmlWriterSecondary, _runnerFeedSecondary.GetRunFeedType(), time);

            //<entry>
            while (reader.Read())
            {
                Log.DebugFormat("{0}::Processing record [{1}]: {2}", identifier, (countProcessed + countError), reader["PID"]);

                var id = reader[dict["gId"]].ToString();
                var title = reader[dict["title"]].ToString();

                try
                {

                    // First check if the product is a sensitive product, and if so, exclude
                    if (dict.ContainsKey("isSensitiveProduct") && int.Parse(reader[dict["isSensitiveProduct"]].ToString()) > 0)
                    {
                        countSkipped++;
                        continue;
                    }

                    var linkSku = reader[dict["linkSku"]].ToString();
                    var brandName = !dict.ContainsKey("gBrand") ? string.Empty : reader[dict["gBrand"]].ToString();
                    var cContributors = PlaRelatedFeedUtils.ContributorAttributes(dict, reader, id);
                    var contributor = cContributors ?? null;
                    var defaultCategory = FeedUtils.GetFeedGeneratorIndigoCategory(_feedGeneratorCategoryService, reader, dict, catalog, Log);
                    var productData = FeedUtils.GetProductData(dict, reader, linkSku, catalog, brandName, contributor, defaultCategory);
                    var formatString = dict.ContainsKey("bisacbindingtypeid") ? FeedUtils.GetFormat(reader[dict["bisacbindingtypeid"]].ToString(), id) : string.Empty;
                    var sanitizedTitle = string.IsNullOrWhiteSpace(title) ? string.Empty : FeedUtils.SanitizeString(title);
                    var gAvailability = !dict.ContainsKey("gAvailability") ? FeedUtils.GetGoogleAvailability(1) : FeedUtils.GetGoogleAvailability((int)reader[dict["gAvailability"]]);
                    var availability = !dict.ContainsKey("gAvailability") ? 1 : (int)reader[dict["gAvailability"]];
                    var recordType = !dict.ContainsKey("recordType") ? string.Empty : reader[dict["recordType"]].ToString();
                    var hasImage = true;
                    if (!SkipHasImageCheck)
                        hasImage = (!dict.ContainsKey("hasImage")) || int.Parse(reader[dict["hasImage"]].ToString()) > 0;

                    string message;
                    var isExcludedDueToData = IndigoBreadcrumbRepositoryUtils.IsExcludedDueToData(_feedId, sanitizedTitle, hasImage, availability, recordType, false, out message);

                    // First determine if the entries are to be excluded or not
                    var isEntryExcluded = isExcludedDueToData || _runnerFeed.IsExcludedFromFeed(productData, false);
                    var isEntrySecondaryExcluded = isExcludedDueToData;
                    if (!isEntrySecondaryExcluded && hasTwoGenerators)
                        isEntrySecondaryExcluded = _runnerFeedSecondary.IsExcludedFromFeed(productData, false);

                    // If both entries are excluded, then there is no need to process anything further
                    if ((isEntryExcluded && isEntrySecondaryExcluded) || (isEntryExcluded && !hasTwoGenerators))
                    {
                        countSkipped++;
                        continue;
                    }

                    var linkCatalog = dict["linkCatalog"];

                    if(MaxTitleLength > 0)
                        sanitizedTitle = FeedUtils.GetTruncatedTitle(sanitizedTitle, formatString, MaxTitleLength, ParameterUtils.GetParameter<bool>("GooglePlaFeedGenerator.TruncateTitle"));

                    var description = reader[dict["description"]].ToString();
                    description = string.IsNullOrWhiteSpace(description) ? sanitizedTitle : FeedUtils.SanitizeString(FeedUtils.RemoveHtmlTags(description));
                    var sanitizedDescription = string.IsNullOrWhiteSpace(description) ? sanitizedTitle : description;

                    // Get the breadcrumb value
                    var googleCategoryBreadcrumb = (string.IsNullOrWhiteSpace(defaultCategory.GoogleCategoryBreadcrumb)) ? dict["gGoogleProductCategory"] : defaultCategory.GoogleCategoryBreadcrumb;
                    var allBreadcrumbs = (productData.BrowseCategoryIds.Any() && !productData.BrowseCategoryIds.Contains(-1)) ? new List<string>() : new List<string> {defaultCategory.Breadcrumb};
                    foreach (var browseCategoryId in productData.BrowseCategoryIds.Distinct())
                    {
                        var categories = _feedGeneratorCategoryService.GetIndigoBreadcrumbCategories(browseCategoryId);
                        if (categories != null)
                        {
                            foreach (var category in categories)
                            {
                                if (category.Crumbs[0].Equals(defaultCategory.Crumbs[0], StringComparison.OrdinalIgnoreCase))
                                    allBreadcrumbs.Add(category.Breadcrumb);
                            }
                        }
                    }

                    decimal? salePrice = null;
                    decimal parsedSalePrice;
                    var regularPrice = decimal.Parse(reader[dict["price"]].ToString());
                    if (!string.IsNullOrEmpty(dict["adjustedPrice"]) && decimal.TryParse(reader[dict["adjustedPrice"]].ToString(), out parsedSalePrice))
                    {
                        if (parsedSalePrice != regularPrice)
                        {
                            if (parsedSalePrice > regularPrice)
                            {
                                regularPrice = parsedSalePrice;
                            }
                            else
                            {
                                salePrice = parsedSalePrice;    
                            }    
                        }
                    }

                    var gtin = reader[dict["gGtin"]].ToString();

                    var isDefaultCpcValue = false;
                    if (!isEntryExcluded)
                    {
                        var customLabel0 = PlaRelatedFeedUtils.GetCustomLabelValue(_runnerFeed, productData, FeedRuleType.Custom_Label_0);
                        var customLabel1 = PlaRelatedFeedUtils.GetCustomLabelValue(_runnerFeed, productData, FeedRuleType.Custom_Label_1);
                        var customLabel2 = PlaRelatedFeedUtils.GetCustomLabelValue(_runnerFeed, productData, FeedRuleType.Custom_Label_2);
                        var customLabel3 = PlaRelatedFeedUtils.GetCustomLabelValue(_runnerFeed, productData, FeedRuleType.Custom_Label_3);
                        var customLabel4 = PlaRelatedFeedUtils.GetCustomLabel4Value(_runnerFeed, productData, dict, reader);

                        var cpcValue = PlaRelatedFeedUtils.GetCpcValue(_runnerFeed, productData, out isDefaultCpcValue);
                        var entry = EntryAttribute(reader
                                                   , id
                                                   , sanitizedTitle, sanitizedDescription, googleCategoryBreadcrumb
                                                   , linkCatalog, linkSku
                                                   , regularPrice
                                                   , salePrice
                                                   , gAvailability//, availability
                                                   , gtin
                                                   , brandName, dict.ContainsKey("gBrand"), defaultCategory.Breadcrumb, allBreadcrumbs, cpcValue
                                                   , customLabel0 , customLabel1, customLabel2, customLabel3, customLabel4
                                                   , _runnerFeed.GetRunFeedType());

                        entry.WriteTo(xmlWriter);
                    }

                    if (hasTwoGenerators && !isEntrySecondaryExcluded)
                    {
                        var cpcValue = PlaRelatedFeedUtils.GetCpcValue(_runnerFeedSecondary, productData, out isDefaultCpcValue);

                        var customLabel0 = PlaRelatedFeedUtils.GetCustomLabelValue(_runnerFeed, productData, FeedRuleType.Custom_Label_0);
                        var customLabel1 = PlaRelatedFeedUtils.GetCustomLabelValue(_runnerFeed, productData, FeedRuleType.Custom_Label_1);
                        var customLabel2 = PlaRelatedFeedUtils.GetCustomLabelValue(_runnerFeed, productData, FeedRuleType.Custom_Label_2);
                        var customLabel3 = PlaRelatedFeedUtils.GetCustomLabelValue(_runnerFeed, productData, FeedRuleType.Custom_Label_3);
                        var customLabel4 = PlaRelatedFeedUtils.GetCustomLabel4Value(_runnerFeed, productData, dict, reader);

                        var entry = EntryAttribute(reader
                                                   , id
                                                   , sanitizedTitle, sanitizedDescription, googleCategoryBreadcrumb
                                                   , linkCatalog, linkSku
                                                   , regularPrice
                                                   , salePrice
                                                   , gAvailability
                                                   , gtin
                                                   , brandName, dict.ContainsKey("gBrand"), defaultCategory.Breadcrumb, allBreadcrumbs, cpcValue
                                                   , customLabel0, customLabel1, customLabel2, customLabel3, customLabel4
                                                   , _runnerFeedSecondary.GetRunFeedType());

                        entry.WriteTo(xmlWriterSecondary);
                    }

                    if (isDefaultCpcValue)
                        _defaultCpcProductInfos.Add(new DefaultCpcProductInfo { Breadcrumb = defaultCategory.Breadcrumb, Sku = linkSku, Title = sanitizedTitle });

                    countProcessed++;
                }
                catch (Exception e)
                {
                    countError++;
                    Log.ErrorFormat("Can't process the item. Id:{0};title:{1}", id, title);
                    Log.DebugFormat("Error stack trace: {0}", e);
                    _executionLogLogger.AddCustomMessage(string.Format("Can't process the item. Id: {0};title: {1}, file identifier: {2}", id, title, identifier));
                    if (HasTwoGenerators()) 
                        _executionLogLoggerSecondary.AddCustomMessage(string.Format("Can't process the item. Id: {0};title: {1}, file identifier: {2}", id, title, identifier));
                    if (!AllowItemErrorsInFiles)
                        _hasError = true;
                }
            }

            PlaRelatedFeedUtils.EndXmlDocument(xmlWriter);
            if (hasTwoGenerators)
                PlaRelatedFeedUtils.EndXmlDocument(xmlWriterSecondary);

            counter = new Tuple<int, int, int>(countProcessed, countError, countSkipped);

            Log.InfoFormat("[WriteFeedFile] {0} completed Processed record count: {1}, Error record count: {2}, skipped record count: {3}", feedFilePath, countProcessed, countError, countSkipped);
        }

        private XElement EntryAttribute(IDataReader reader
            , string gId
            , string sanitizedTitle, string sanitizedDescription, string googleProductCategory
            , string linkCatalog
            , string linkSku
            , decimal regularPrice
            , decimal? salePrice
            , string gAvailability //, string availability
            , string gtin
            , string gBrandName,
            bool hasBrandName, 
            string defaultBreadcrumb,
            IEnumerable<string> allBreadcrumbs, 
            string cpcValue,
            string customLabel0, 
            string customLabel1, 
            string customLabel2,
            string customLabel3,
            string customLabel4,
            GoogleRunFeedType feedType)
        {
            var googleNs = (feedType == GoogleRunFeedType.Google) ? FeedXmlsnsG : FeedXmlns;
            //entry
            var entry = new XElement(FeedXmlns + "entry");
            //g:id
            var gaId = new XElement(googleNs + "id", gId);
            entry.Add(gaId);
            //title
            var aTitle = new XElement(FeedXmlns + "title", new XCData(sanitizedTitle));
            entry.Add(aTitle);
            //description
            var aDescription = new XElement(FeedXmlns + "description", new XCData(sanitizedDescription));
            entry.Add(aDescription);
            //<g:google_product_category>
            var gaGoogleProductCategory = new XElement(googleNs + "google_product_category", googleProductCategory);
            entry.Add(gaGoogleProductCategory);
            //write optional product type - NOT required by google
            foreach (var breadcrumb in allBreadcrumbs.Take(MaximumBreadcrumbsToSend))
            {
                var aProductType = new XElement(googleNs + "product_type", breadcrumb);
                entry.Add(aProductType);
            }

            //link
            var url = PlaRelatedFeedUtils.GetFeedEntryLinkValue(reader, linkCatalog, linkSku);
            if (feedType == GoogleRunFeedType.Yahoo) 
                url += YahooItemPageUrlSuffix;
            var aLink = PlaRelatedFeedUtils.FeedEntryLink(url); 
            entry.Add(aLink);            

            //g:image_link
            var aImgLink = PlaRelatedFeedUtils.EntryImageLink(reader, linkSku, googleNs, feedType);
            entry.Add(aImgLink);
            //g:condition
            var gaCondition = new XElement(googleNs + "condition", "new");
            entry.Add(gaCondition);
            //google search availability
            var gaAvailability = new XElement(googleNs + "availability", gAvailability);
            entry.Add(gaAvailability);

            //g:price, g:sale_price, sale_price_effective_date
            if (DisplaySalePriceInfo)
            {
                var gaPrice = new XElement(googleNs + "price", regularPrice.ToString("F", CultureInfo.InvariantCulture) + " CAD");
                entry.Add(gaPrice);

                if (salePrice.HasValue)
                {
                    var gaSalePrice = new XElement(googleNs + "sale_price", salePrice.Value.ToString("F", CultureInfo.InvariantCulture) + " CAD");
                    entry.Add(gaSalePrice);
                    if (DisplaySalePriceTimeSpanInfo)
                    {
                        var time = DateTime.Now.AddDays(SalePriceInfoTimeSpanBegin);
                        const string timeToStringFormat = "yyyy-MM-ddTHH:mmK";
                        entry.Add(new XElement(googleNs + "sale_price_effective_date", string.Format("{0}/{1}", time.ToString(timeToStringFormat), time.AddDays(SalePriceInfoTimeSpanEnd).ToString(timeToStringFormat))));
                    }
                }
            }
            else
            {
                var gaPrice = new XElement(googleNs + "price", regularPrice.ToString("F", CultureInfo.InvariantCulture) + " CAD");
                entry.Add(gaPrice);
            }

            // Items to be sent only to Google
            if (feedType == GoogleRunFeedType.Google)
            {
                if (DisplayDefaultShoppingInfo)
                {
                    //g:shipping
                    var gShipping = PlaRelatedFeedUtils.ShippingAttribute(null, null, null, googleNs);
                    entry.Add(gShipping);    
                }

                //write mandatory brand - required by google - only for items under Apparel & Accessories 
                // (if brand name isn't available, set identifier_exists to false --> Unique Product Identifiers section)
                // https://support.google.com/merchants/answer/188494?hl=en&ref_topic=3404778
                if (hasBrandName && !string.IsNullOrWhiteSpace(gBrandName))
                    entry.Add(new XElement(googleNs + "brand", gBrandName.Trim()));

                //google search unique product identifiers -> g:gtin
                var gaGtin = new XElement(googleNs + "gtin", gtin);
                entry.Add(gaGtin);

                // Add the adwords_redirect node
                if (!string.IsNullOrWhiteSpace(GoogleAdwordsRedirectUrlSuffix))
                    entry.Add(new XElement(googleNs + "adwords_redirect", url + GoogleAdwordsRedirectUrlSuffix)); 
                else
                    entry.Add(new XElement(googleNs + "adwords_redirect", url)); 

                entry.Add(new XElement(googleNs + "adwords_grouping", defaultBreadcrumb));
                if (!string.IsNullOrWhiteSpace(customLabel0))
                {
                    entry.Add(new XElement(googleNs + "custom_label_0", customLabel0));
                }
                if (!string.IsNullOrWhiteSpace(customLabel1))
                {
                    entry.Add(new XElement(googleNs + "custom_label_1", customLabel1));
                }
                if (!string.IsNullOrWhiteSpace(customLabel2))
                {
                    entry.Add(new XElement(googleNs + "custom_label_2", customLabel2));
                }
                if (!string.IsNullOrWhiteSpace(customLabel3))
                {
                    entry.Add(new XElement(googleNs + "custom_label_3", customLabel3));
                }
                if (!string.IsNullOrWhiteSpace(customLabel4))
                {
                    entry.Add(new XElement(googleNs + "custom_label_4", customLabel4));
                }
            }

            if (feedType == GoogleRunFeedType.Yahoo)
                entry.Add(new XElement(FeedXmlns + "cpc", cpcValue));

            return entry;
        }

        private void ProcessDefaultCpcProductInfos()
        {
            if (!Directory.Exists(AncillaryOutputFolderPath))
                Directory.CreateDirectory(AncillaryOutputFolderPath);

            var filePath = Path.Combine(AncillaryOutputFolderPath, DefaultCpcsFileName);
            if (File.Exists(filePath))
                File.Delete(filePath);

            var invalidEntriesStreamWriter = new StreamWriter(filePath);
            foreach (var productInfo in _defaultCpcProductInfos)
            {
                invalidEntriesStreamWriter.WriteLine(productInfo.Sku + ", " + productInfo.Title + ", " + productInfo.Breadcrumb);
            }
            invalidEntriesStreamWriter.Close();
        }

        private struct DefaultCpcProductInfo
        {
            public string Sku { get; set; }
            public string Breadcrumb { get; set; }
            public string Title { get; set; }
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
