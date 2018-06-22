using Castle.Core.Logging;
using FeedGenerators.Core.Services.Abstract;
using FeedGenerators.Core.Services.Concrete;
using GoogleApiPlaFeedGenerator.Json;
using Indigo.Feeds.Entities.Concrete;
using Indigo.Feeds.Models.Concrete;
using Indigo.Feeds.Services.Concrete;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Types;
using Indigo.Feeds.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace GoogleApiPlaFeedGenerator.Execution
{
    public class Runner : IRunner
    {
        private IExecutionLogLogger _executionLogLogger;
        private bool _isIncrementalRun;
        private bool _isPseudoFullRun;
        private DateTime? _effectiveFromTime;

        private IFeedGeneratorIndigoCategoryService _feedGeneratorCategoryService;
        private IFeedCmsProductArchiveEntryService _feedCmsProductArchiveEntryService;
        private IFeedCmsProductArchiveEntryService _pastFeedCmsProductArchiveEntryService;
        private bool _hasError;

        private readonly IFeedRuleService _feedRuleService;
        private readonly IFeedCmsProductArchiveEntryService _feedCmsProductArchiveEntryServiceShared;
        private readonly IIndigoCategoryService _indigoCategoryService;
        private readonly IGoogleCategoryService _googleCategoryService;
        private IProductDataService _productDataService;
        private IOutputInstructionProcessor _outputInstructionProcessor;

        private static readonly bool LimitTo100Products = ParameterUtils.GetParameter<bool>("GoogleApiPlaFeedGenerator.LimitTo100Products");
        private static readonly bool AllowItemErrorsInFiles = ParameterUtils.GetParameter<bool>("AllowItemErrorsInFiles");
        private static readonly bool AllowRuleOptimizations = ParameterUtils.GetParameter<bool>("AllowRuleOptimizations");
        private static readonly bool AllowRuleEntryRemovals = ParameterUtils.GetParameter<bool>("AllowRuleEntryRemovals");
        private static readonly bool AllowIEnumerableRuleEvaluations = ParameterUtils.GetParameter<bool>("AllowIEnumerableRuleEvaluations");
        //private static readonly bool SendExcludedProductData = ParameterUtils.GetParameter<bool>("SendExcludedProductData");
        private static readonly string OdysseyCommerceConnectionString = ConfigurationManager.ConnectionStrings["OdysseyCommerceDB"].ConnectionString;
        private static readonly Dictionary<string, Dictionary<string, string>> FeedGenerationInstructionsDictionary = ConfigurationManager.GetSection("feedgenerationinstructiondict") as Dictionary<string, Dictionary<string, string>>;
        private static readonly int SearchDataCommandTimeout = ParameterUtils.GetParameter<int>("SearchDataCommandTimeout");
        private static readonly int MaximumThreadsToUse = ParameterUtils.GetParameter<int>("MaximumThreadsToUse");
        private static readonly int GooglePlaFeedId = ParameterUtils.GetParameter<int>("GooglePlaFeedId");
        private static readonly bool SkipHasImageCheck = ParameterUtils.GetParameter<bool>("GoogleApiPlaFeedGenerator.SkipHasImageCheck");
        private static readonly string DeletedProductsStoredProcedureName = ParameterUtils.GetParameter<string>("DeletedProductsStoredProcedureName");
        private static readonly int NumberOfProductsPerApiCall = ParameterUtils.GetParameter<int>("NumberOfProductsPerApiCall");

        public ILogger Log { get; set; }

        public Runner(IFeedCmsProductArchiveEntryService feedCmsProductArchiveEntryService, IFeedRuleService feedRuleService, IIndigoCategoryService indigoCategoryService, IGoogleCategoryService googleCategoryService, IOutputInstructionProcessor outputInstructionProcessor)
        {
            _feedCmsProductArchiveEntryServiceShared = feedCmsProductArchiveEntryService;
            _feedRuleService = feedRuleService;
            _indigoCategoryService = indigoCategoryService;
            _googleCategoryService = googleCategoryService;
            _outputInstructionProcessor = outputInstructionProcessor;
        }

        public void Initialize(IExecutionLogLogger executionLogLogger, int plaFeedId, bool isIncremental, DateTime? fromTime, DateTime? effectiveFromTime, DateTime executionTime, bool isPseudoFullRun)
        {
            _executionLogLogger = executionLogLogger;
            _isIncrementalRun = isIncremental;
            _isPseudoFullRun = isPseudoFullRun;
            _effectiveFromTime = effectiveFromTime;

            // Instantiate the IFeedGeneratorIndigoCategoryService 
            _feedGeneratorCategoryService = new FeedGeneratorIndigoCategoryService(_indigoCategoryService, _googleCategoryService, Log);

            // Instantiate the "caching" cms data services
            _feedCmsProductArchiveEntryService = new FeedGeneratorCmsDataService(_feedCmsProductArchiveEntryServiceShared, null);
            _pastFeedCmsProductArchiveEntryService = new FeedGeneratorCmsDataService(_feedCmsProductArchiveEntryServiceShared, fromTime);

            // First get rules associated with this feed
            _productDataService = new ProductDataService(null, _feedGeneratorCategoryService, _feedRuleService, _feedCmsProductArchiveEntryService, _feedGeneratorCategoryService, _pastFeedCmsProductArchiveEntryService);
        }

        public IExecutionLogLogger Execute()
        {
            if (IsEffectiveIncrementalRun())
                Log.InfoFormat("Executing an incremental run with an effective from time of {0}.", _effectiveFromTime);

            if (_isPseudoFullRun)
                Log.InfoFormat("Note that this is a pseudo full run.");

            // If we're in an incremental run, first process the deleted products as deletions are probably more important than updates
            // i.e. don't pay advertising money on products that don't exist on the site
            if (IsEffectiveIncrementalRun() && _effectiveFromTime.HasValue)
                ProcessDeletedProducts(_effectiveFromTime.Value);

            // Build the range collection that will be used to generate the api calls dynamically
            var ranges = GetRanges();
            if (ranges.Any())
                Parallel.ForEach(ranges, new ParallelOptions { MaxDegreeOfParallelism = MaximumThreadsToUse }, ProcessRange);

            _executionLogLogger.HasError = _hasError;
            _executionLogLogger.SetExecutionEndTime(DateTime.Now);

            return _executionLogLogger;
        }

        private bool IsEffectiveIncrementalRun()
        {
            return _isPseudoFullRun || _isIncrementalRun;
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
                ProcessFeed(processingInstruction.Catalog, processingInstruction.Dbcmd, sqlParameters, identifier, processingInstruction.CatalogAttributesSection);
                var endDt = DateTime.Now;
                var execTime = endDt - startDt;
                Log.InfoFormat("[{0}] completed. Execution time in seconds: {1}", identifier, execTime.TotalSeconds);
                _executionLogLogger.AddFileGenerationUpdate(identifier, true);
            }
            catch (Exception ex)
            {
                Log.InfoFormat("[Feed] {0}; error {1}", processingInstruction.Catalog + "-" + pair[0] + pair[1], ex);
                _executionLogLogger.AddFileGenerationUpdate(identifier, false);
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
        private void ProcessFeed(string catalog, string commandText, SqlParameter[] sqlParameters, string identifier, string configSection)
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

                    if (IsEffectiveIncrementalRun())
                    {
                        sqlCommand.Parameters.AddWithValue("@IsIncremental", 1);
                        sqlCommand.Parameters.AddWithValue("@DateChanged", _effectiveFromTime);
                    }

                    using (var sqlDataReader = sqlCommand.ExecuteReader())
                    {
                        if (sqlDataReader.HasRows)
                        {
                            ExecuteFeedUpdates(catalog, sqlDataReader, identifier, configSection);
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
        private void ExecuteFeedUpdates(string catalog, IDataReader reader, string identifier, string configSection)
        {
            var dict = ConfigurationManager.GetSection(configSection) as StringDictionary;

            Log.DebugFormat("[ExecuteFeedUpdates] {0} start", identifier);
            ExecuteFeedUpdate(reader, dict, catalog, identifier);
            Log.DebugFormat("[ExecuteFeedUpdates] {0} completed", identifier);
        }

        private void ExecuteFeedUpdate(IDataReader reader, StringDictionary dict, string catalog, string identifier)
        {
            var counter = new ProcessingCounters();

            //<entry>
            var batch = new List<GooglePlaProductData>();
            var deletedBatch = new List<string>();
            var fileCount = 1;
            while (reader.Read())
            {
                Log.DebugFormat("{0}::Processing record [{1}]: {2}", identifier, (counter.GetTotalProcessed()), reader["PID"]);

                var id = reader[dict["gId"]].ToString();
                var title = reader[dict["title"]].ToString();
                try
                {
                    // First get the Bronte data processing result
                    var processingResult = _productDataService.GetGooglePlaProductData(GetProductDataProcessingRequest(dict, reader, catalog));

                    // Process the result and add to batch if the product update is to be sent to Google while
                    // ensuring that the proper counter gets incremented
                    switch (processingResult.Status)
                    {
                        case GooglePlaProductDataStatus.ExcludedDueToProductData:
                        case GooglePlaProductDataStatus.ExcludedDueToExclusionRule:
                            Log.DebugFormat("Product {0} was excluded.", id);
                            counter.NumberOfExcluded++;
                            continue;
                        case GooglePlaProductDataStatus.Unmodified:
                            if (_isPseudoFullRun)
                            {
                                Log.DebugFormat("Product {0} was unmodified but was treated as if it was an update due to pseudo full run that's in progress.", id);
                                batch.Add(processingResult.ProductData);
                                counter.NumberOfProcessed++;
                                break;
                            }
                            else
                            {
                                Log.DebugFormat("Product {0} was skipped as its data and/or rules haven't resulted in a change for the product.", id);
                                counter.NumberOfUnchanged++;
                                continue;
                            }
                        case GooglePlaProductDataStatus.Removed:
                            Log.DebugFormat("Product {0} was removed, so adding its identifier to the removals batch.", id);
                            counter.NumberOfDeleted++;
                            deletedBatch.Add(id);
                            break;
                        case GooglePlaProductDataStatus.FoundOrModified:
                            Log.DebugFormat("Product {0} was found/modified, so adding its data to the insert/update batch.", id);
                            batch.Add(processingResult.ProductData);
                            counter.NumberOfProcessed++;
                            break;
                        case GooglePlaProductDataStatus.NotFound:
                        default:
                            var message = string.Format("[{2}]Product {0} - {1} wasn't found, so is being treated as an erroneous product.", id, title, identifier);
                            Log.Debug(message);
                            counter.NumberOfErrored++;
                            _executionLogLogger.AddCustomMessage(message);
                            if (!AllowItemErrorsInFiles)
                                _hasError = true;
                            continue;
                    }
                }
                catch (Exception exception)
                {
                    counter.NumberOfErrored++;
                    var errorMessage = string.Format("Can't process the item. Id:{0};title:{1},catalog:{2},Message:{3}", id, title, catalog, exception.Message);
                    Log.Error(errorMessage);
                    Log.DebugFormat("Error stack trace: {0}", exception);
                    _executionLogLogger.AddCustomMessage(string.Format("Can't process the item. Id: {0};title: {1}, file identifier: {2}", id, title, identifier));
                    if (!AllowItemErrorsInFiles)
                        _hasError = true;
                }

                if (batch.Count >= NumberOfProductsPerApiCall)
                {
                    SendUpdateBatch(batch, fileCount, identifier);
                    fileCount++;
                    batch = new List<GooglePlaProductData>();
                }

                if (deletedBatch.Count >= NumberOfProductsPerApiCall)
                {
                    SendDeletionBatch(deletedBatch, fileCount, identifier);
                    fileCount++;
                    deletedBatch = new List<string>();
                }
            }

            if (batch.Any())
            {
                SendUpdateBatch(batch, fileCount, identifier);
                fileCount++;
                batch = new List<GooglePlaProductData>();
            }

            if (deletedBatch.Any())
            {
                SendDeletionBatch(deletedBatch, fileCount, identifier);
                fileCount++;
                deletedBatch = new List<string>();
            }

            var infoLogMessage = string.Format("[ExecuteFeedUpdate] {0} completed processed record count: {1}, error record count: {2}, unchanged record count: {3}, " +
                           "excluded (ignored) record count: {4}, removed record count {5}.",
                identifier, counter.NumberOfProcessed, counter.NumberOfErrored, counter.NumberOfUnchanged, counter.NumberOfExcluded, counter.NumberOfDeleted);

            //if (SendExcludedProductData)
            //    infoLogMessage += "Excluded produces were included in processed count.";

            Log.Info(infoLogMessage);
        }

        private GooglePlaProductDataProcessingRequest GetProductDataProcessingRequest(StringDictionary dict, IDataReader reader, string catalog)
        {
            var feedSectionType = GetCatalog(catalog);
            var result = new GooglePlaProductDataProcessingRequest
            {
                AllowIenumerableRuleEvaluations = AllowIEnumerableRuleEvaluations, 
                AllowRuleOptimizations = AllowRuleOptimizations, 
                AllowRuleEntryRemovals = AllowRuleEntryRemovals,
                Catalog = feedSectionType,
                DefaultGoogleBreadcrumb = dict["gGoogleProductCategory"],
                ExecutionTime = _executionLogLogger.GetExecutionStartTime(),
                FromTime = _effectiveFromTime,
                IsIncremental = IsEffectiveIncrementalRun(),
                RequireProductDataResultForUnmodifiedProducts = _isPseudoFullRun,
                ProductData = GetBronteProductData(dict, reader, feedSectionType)
            };

            return result;
        }

        private static FeedSectionType GetCatalog(string catalog)
        {
            switch (catalog.ToUpperInvariant())
            {
                case "BOOKS":
                    return FeedSectionType.Books;
                case "GENERALMERCHANDISE":
                    return FeedSectionType.Gifts;
                default:
                    throw new ArgumentException("Invalid catalog value was passed in.", "catalog");
            }
        }

        private BrontePlaProductData GetBronteProductData(StringDictionary dict, IDataReader reader, FeedSectionType catalog)
        {
            var hasImage = true;
            if (!SkipHasImageCheck)
                hasImage = (!dict.ContainsKey("hasImage")) || int.Parse(reader[dict["hasImage"]].ToString()) > 0;

            var isCanonical = false;
            bool.TryParse(reader[dict["isCanonical"]].ToString(), out isCanonical);

            var isSensitiveProduct = dict.ContainsKey("isSensitiveProduct") && int.Parse(reader[dict["isSensitiveProduct"]].ToString()) > 0;
            decimal? adjustedPrice = null;
            decimal parsedSalePrice;
            if (!string.IsNullOrEmpty(dict["adjustedPrice"]) && decimal.TryParse(reader[dict["adjustedPrice"]].ToString(), out parsedSalePrice))
                adjustedPrice = parsedSalePrice;

            var result = new BrontePlaProductData
            {
                AvailabilityID = dict.ContainsKey("gAvailability") ? (int)reader[dict["gAvailability"]] : 1, 
                BISACBindingTypeID = dict.ContainsKey("bisacbindingtypeid") ? reader[dict["bisacbindingtypeid"]].ToString() : string.Empty, 
                BrandName = dict.ContainsKey("gBrand") ? reader[dict["gBrand"]].ToString() : null,
                BrowseCategories = dict.ContainsKey("gProductType") ? reader[dict["gProductType"]].ToString() : null,
                Contributors = dict.ContainsKey("contributors") ? reader[dict["contributors"]].ToString() : null,
                Description = reader[dict["description"]].ToString(),
                HasImage = hasImage,
                ISBN = dict.ContainsKey("secondarySku") ? reader[dict["secondarySku"]].ToString() : null,
                ISBN13 = catalog == FeedSectionType.Books ? reader[dict["linkSku"]].ToString() : null,
                IsCanonical = isCanonical,
                IsSensitiveProduct = isSensitiveProduct,
                PID = reader[dict["gId"]].ToString(),
                RecordType = dict.ContainsKey("recordType") ? reader[dict["recordType"]].ToString() : string.Empty, 
                Title = reader[dict["title"]].ToString(), 
                UPC = catalog != FeedSectionType.Books ? reader[dict["linkSku"]].ToString() : null,
                ListPrice = decimal.Parse(reader[dict["price"]].ToString()),
                AdjustedPrice = adjustedPrice,
                BronteProductDataStatus = GetProductStatus(dict, reader)
            };

            return result;
        }

        private BronteProductDataStatus GetProductStatus(StringDictionary dict, IDataReader reader)
        {
            var productStatus = BronteProductDataStatus.New;
            if (IsEffectiveIncrementalRun())
                productStatus = (BronteProductDataStatus)int.Parse(reader["StatusId"].ToString());

            return productStatus;
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
            public int NumberOfDeleted { get; set; }
            public int NumberOfExcluded { get; set; }
            public int NumberOfUnchanged { get; set; }
            public int NumberOfErrored { get; set; }

            public int GetTotalProcessed()
            {
                return NumberOfProcessed + NumberOfDeleted + NumberOfUnchanged + NumberOfErrored;
            }
        }

        #region Update-related API code
        private void SendUpdateBatch(IList<GooglePlaProductData> productDatas, int fileCount, string identifier)
        {
            if (fileCount == 1)
                Log.InfoFormat("Starting to write batch update file {0} for identifier {1} containing {2} products.", fileCount, identifier, productDatas.Count);
            else
                Log.DebugFormat("Starting to write batch update file {0} for identifier {1} containing {2} products.", fileCount, identifier, productDatas.Count);

            var fileContent = new OutputInstruction
            {
                Format = OutputFormat.Update,
                Updates = productDatas, 
                FileCount = fileCount
            };
            _outputInstructionProcessor.RecordOutputInstruction(fileContent, identifier, "update" + fileCount);
            Log.Debug("Completed writing batch update file.");
        }
        #endregion

        #region Processing of deleted products
        private void ProcessDeletedProducts(DateTime fromTime)
        {
            var deletedProductIds = new List<string>();
            using (var sqlConnection = new SqlConnection(OdysseyCommerceConnectionString))
            {
                sqlConnection.Open();
                using (var sqlCommand = new SqlCommand(DeletedProductsStoredProcedureName, sqlConnection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = SearchDataCommandTimeout
                })
                {
                    sqlCommand.Parameters.AddWithValue("@DateChanged", fromTime);
                    using (var sqlDataReader = sqlCommand.ExecuteReader())
                    {
                        if (sqlDataReader.HasRows)
                        {
                            while (sqlDataReader.Read())
                            {
                                deletedProductIds.Add(sqlDataReader["PID"].ToString());
                            }
                        }
                    }//using sqldatareader
                } //using sqlCommand    
            }

            Log.InfoFormat("There were {0} deleted products retrieved from the database", deletedProductIds.Count);
            SendDeletedProductInformation(deletedProductIds);
        }

        private void SendDeletedProductInformation(IList<string> deletedProductIds)
        {
            Log.DebugFormat("Entered void SendDeletedProductInformation(IList<string> deletedProductIds) with {0} products.", deletedProductIds.Count);
            var fileCount = 1;
            var batch = new List<string>();

            foreach (var id in deletedProductIds)
            {
                batch.Add(id);

                if (batch.Count >= NumberOfProductsPerApiCall)
                {
                    SendDeletionBatch(batch, fileCount, "deletions");
                    fileCount++;
                    batch = new List<string>();
                }
            }

            if (batch.Count > 0)
            {
                SendDeletionBatch(batch, fileCount, "deletions");
                fileCount++;
                batch = new List<string>();
            }

            Log.DebugFormat("Exiting void SendDeletedProductInformation(IList<string> deletedProductIds).", deletedProductIds.Count);
        }

        private void SendDeletionBatch(IList<string> deletedProductIds, int fileCount, string identifier)
        {
            if (fileCount == 1)
                Log.InfoFormat("Starting to write batch delete file {0} for identifier {1} containing {2} products.", fileCount, identifier, deletedProductIds.Count);
            else
                Log.DebugFormat("Starting to write batch delete file {0} for identifier {1} containing {2} products.", fileCount, identifier, deletedProductIds.Count);

            var fileContent = new OutputInstruction
            {
                Format = OutputFormat.Delete,
                Deletions = deletedProductIds, 
                FileCount = fileCount
            };
            _outputInstructionProcessor.RecordOutputInstruction(fileContent, identifier, "delete" + fileCount);
            Log.Debug("Completed writing batch delete file.");
        }
        #endregion
    }
}
