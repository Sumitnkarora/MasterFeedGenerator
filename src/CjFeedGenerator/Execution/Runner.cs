using Castle.Core.Internal;
using Castle.Core.Logging;
using FeedGenerators.Core.SectionHanlderEntities;
using FeedGenerators.Core.Services.Abstract;
using FeedGenerators.Core.Services.Concrete;
using FeedGenerators.Core.Utils;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Services.Interfaces;
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

namespace CjFeedGenerator.Execution
{
    using System.Web;
    using XE = XElement;

    public class Runner : IRunner 
    {
        private IExecutionLogLogger _executionLogLogger; 
        private bool _isIncrementalRun;
        private DateTime? _fromTime;
        private DateTime? _effectiveFromTime;
        private int _feedId;
        private IFeedGeneratorIndigoCategoryService _feedGeneratorCategoryService;
        private readonly IFeedRuleService _feedRuleService;
        private readonly IFeedCmsProductArchiveEntryService _feedCmsProductArciveEntryService; 
        private readonly IIndigoCategoryService _indigoCategoryService;
        private readonly IGoogleCategoryService _googleCategoryService;
        private Dictionary<int, List<DeletedProductInfo>> _deletedProductInfos = new Dictionary<int, List<DeletedProductInfo>>();
        private readonly ConcurrentBag<MissingMerchandiseTypeProductInfo> _missingMerchandiseTypeProductInfos = new ConcurrentBag<MissingMerchandiseTypeProductInfo>();
        private readonly ConcurrentBag<decimal> _updatedMerchandiseTypeProductPids = new ConcurrentBag<decimal>();
        private RunnerHelper _runnerHelper; 

        private static readonly bool LimitTo100Products = ParameterUtils.GetParameter<bool>("CjFeedGenerator.LimitTo100Products");
        private static readonly bool GzipFiles = ParameterUtils.GetParameter<bool>("CjFeedGenerator.GzipFiles");
        private static readonly string OdysseyCommerceConnectionString = ConfigurationManager.ConnectionStrings["OdysseyCommerceDB"].ConnectionString;
        private static readonly string FileNameFormat = ParameterUtils.GetParameter<string>("FileNameFormat");
        private static readonly string OutputFolderPath = ParameterUtils.GetParameter<string>("CjFeedGenerator.OutputFolderPath");
        private static readonly string BaseUrl = ParameterUtils.GetParameter<string>("PhoenixOnlineBaseUrl");
        private static readonly string UrlSuffix = ParameterUtils.GetParameter<string>("UrlSuffix");
        private static readonly string ImagePathSuffix = ParameterUtils.GetParameter<string>("ImagePathSuffix");
        private static readonly string ImgBaseUrl = ParameterUtils.GetParameter<string>("DynamicImagesUrl");
        private static readonly bool DisplaySalePriceInfo = ParameterUtils.GetParameter<bool>("DisplaySalePriceInfo");
        private static readonly FeedGenerationFileInstructionsConfigurationSection FeedGenerationInstructions = ConfigurationManager.GetSection("feedGenerationFileInstructions") as FeedGenerationFileInstructionsConfigurationSection;
        private static readonly int SearchDataCommandTimeout = ParameterUtils.GetParameter<int>("SearchDataCommandTimeout");
        private static readonly string IncrementalRunDeletedProductsStoredProcedureName = ParameterUtils.GetParameter<string>("IncrementalRunDeletedProductsStoredProcedureName");
        private static readonly int MaxTitleLength = ParameterUtils.GetParameter<int>("MaxTitleLength");
        private static readonly bool AllowItemErrorsInFiles = ParameterUtils.GetParameter<bool>("AllowItemErrorsInFiles");
        private static readonly bool AllowRuleOptimizations = ParameterUtils.GetParameter<bool>("AllowRuleOptimizations");
        private static readonly bool AllowRuleEntryRemovals = ParameterUtils.GetParameter<bool>("AllowRuleEntryRemovals");
        private static readonly bool AllowIEnumerableRuleEvaluations = ParameterUtils.GetParameter<bool>("AllowIEnumerableRuleEvaluations");
        private static readonly string DocTypeSystemId = ParameterUtils.GetParameter<string>("DocTypeSystemId");
        private static readonly string Cid = ParameterUtils.GetParameter<string>("Cid");
        //private static readonly string DateFormatString = ParameterUtils.GetParameter<string>("DateFormatString");
        private static readonly string SubId = ParameterUtils.GetParameter<string>("SubId");
        private static readonly string ZeroCommissionListName = ParameterUtils.GetParameter<string>("ZeroCommissionListName");
        private static readonly string BreadcrumbTrailSplitter = ParameterUtils.GetParameter<string>("BreadcrumbTrailSplitter");
        private static readonly int MaxKeywordLength = ParameterUtils.GetParameter<int>("MaxKeywordLength");
        private static readonly int MaxPromotionalTextLength = ParameterUtils.GetParameter<int>("MaxPromotionalTextLength");
        private static readonly int MaxAdvertiserCategoryLength = ParameterUtils.GetParameter<int>("MaxAdvertiserCategoryLength");
        private static readonly int MaxManufacturerLength = ParameterUtils.GetParameter<int>("MaxManufacturerLength");
        private static readonly int MaxDescriptionLength = ParameterUtils.GetParameter<int>("MaxDescriptionLength");
        private static readonly string DefaultShippingCost = ParameterUtils.GetParameter<string>("DefaultShippingCost");
        private static readonly string AncillaryOutputFolderPath = ParameterUtils.GetParameter<string>("CjFeedGenerator.AncillaryOutputFolderPath");
        private static readonly string MissingMerchandiseTypeFileName = ParameterUtils.GetParameter<string>("MissingMerchandiseTypeFileName");
        private static readonly int MaximumThreadsToUse = ParameterUtils.GetParameter<int>("MaximumThreadsToUse");
        private static readonly string DefaultEGiftCardPrice = ParameterUtils.GetParameter<string>("DefaultEGiftCardPrice");

        public ILogger Log { get; set; }

        public Runner(IFeedCmsProductArchiveEntryService feedCmsProductArciveEntryService, IFeedRuleService feedRuleService, IIndigoCategoryService indigoCategoryService, IGoogleCategoryService googleCategoryService)
        {
            _feedCmsProductArciveEntryService = feedCmsProductArciveEntryService;
            _feedRuleService = feedRuleService;
            _indigoCategoryService = indigoCategoryService;
            _googleCategoryService = googleCategoryService;
        }

        public void Initialize(IExecutionLogLogger executionLogLogger, int feedId, bool isIncremental, DateTime? fromTime, DateTime? effectiveFromTime, DateTime executionTime)
        {
            _executionLogLogger = executionLogLogger;
            _isIncrementalRun = isIncremental;
            _fromTime = fromTime;
            _effectiveFromTime = effectiveFromTime;
            _feedId = feedId;            

            // Instantiate the IFeedGeneratorIndigoCategoryService 
            _feedGeneratorCategoryService = new FeedGeneratorIndigoCategoryService(_indigoCategoryService, _googleCategoryService, Log);
            _runnerHelper = new RunnerHelper(_feedRuleService, _feedGeneratorCategoryService, _feedCmsProductArciveEntryService, Log, AllowRuleOptimizations, AllowRuleEntryRemovals, AllowIEnumerableRuleEvaluations);
            _runnerHelper.Initialize(feedId, _isIncrementalRun, _fromTime, executionTime);
        }

        public IExecutionLogLogger Execute()
        {
            // If we're in an incremental run, first populate the deleted products dictionary
            if (_isIncrementalRun && _effectiveFromTime.HasValue)
                PopulateDeletedProductInfo(_effectiveFromTime.Value);

            Parallel.ForEach(FeedGenerationInstructions.FeedGenerationFileInstructions, new ParallelOptions {MaxDegreeOfParallelism = MaximumThreadsToUse}, GenerateFeedFile);

            // Create a text file with the items that didn't have merchandise types assigned 
            if (_missingMerchandiseTypeProductInfos.Any())
            {   
                var message = string.Format("There were {0} products that weren't assigned proper merchandise type values. They've been placed under zero commission list and IT has the list.", _missingMerchandiseTypeProductInfos.Count);
                _executionLogLogger.AddCustomMessage(message);
                Log.Info(message);
            }

            ProcessMissingMerchandiseTypeProductInfos();
            ProcessUpdatedMerchandiseTypeProductPids();

            _executionLogLogger.HasError = _hasError;
            _executionLogLogger.SetExecutionEndTime(DateTime.Now);
            return _executionLogLogger;
        }

        private bool _hasError;
        private void GenerateFeedFile(FeedGenerationFileInstruction fileInstruction)
        {
            var feedFilePath = GetFeedFileName(fileInstruction);

            try
            {
                Log.DebugFormat("[WriteFeedFile] {0} start", feedFilePath);
                //create gzip archive stream
                if (GzipFiles)
                {
                    using (var gzipOut = new GZipStream(File.Create(feedFilePath + ".gz"), CompressionMode.Compress))
                    {
                        using (var xmlWriter = XmlWriter.Create(gzipOut, new XmlWriterSettings { Indent = true }))
                        {
                            WriteFeedFile(xmlWriter, fileInstruction);
                        } //end using
                    }
                }
                else
                {
                    using (var xmlWriter = XmlWriter.Create(feedFilePath, new XmlWriterSettings { Indent = true }))
                    {
                        WriteFeedFile(xmlWriter, fileInstruction);
                    } //end using
                }
                _executionLogLogger.AddFileGenerationUpdate(feedFilePath, true);
            }
            catch (Exception ex)
            {
                Log.Error("Error generating a feed file.", ex);
                _executionLogLogger.AddFileGenerationUpdate(feedFilePath, false);
                _hasError = true;
            }
        }

        private void WriteFeedFile(XmlWriter xmlWriter, FeedGenerationFileInstruction fileInstruction)
        {
            var countRec = new Tuple<int, int, int>(0, 0, 0);
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteDocType("product_catalog_data", null, DocTypeSystemId, null);
            xmlWriter.WriteStartElement("product_catalog_data");
            var header = new XE("header");
            header.Add(new XE("cid", Cid));
            header.Add(new XE("subid", SubId));
            //header.Add(new XE("datefmt", DateFormatString));
            header.Add(new XE("processtype", (_isIncrementalRun) ? "UPDATE" : "OVERWRITE"));
            header.Add(new XE("aid", fileInstruction.Aid));
            
            header.WriteTo(xmlWriter);

            //<feedGenerationFileLineItem isIncluded="true" catalog="books" storedProcedureName="uspCJFeedBooks" catalogattributesection="booksattributes" ranges="00-04;05-09" />               
            foreach (var fileComponent in fileInstruction.LineItems)
            {
                WriteFileComponent(xmlWriter, fileComponent, ref countRec);   
            }

            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();
            
        }

        private void WriteFileComponent(XmlWriter xmlWriter, FeedGenerationFileLineItem fileComponent, ref Tuple<int, int, int> countRec)
        {
            using (var sqlConnection = new SqlConnection(OdysseyCommerceConnectionString))
            {
                sqlConnection.Open();

                var catalog = fileComponent.Catalog;
                var sqlParameters = new SqlParameter[2];
                if (!fileComponent.IsIncluded)
                {
                    Log.InfoFormat("FeedGenerationFileLineItem [{0}-{1}] was excluded from feed generation.", catalog, fileComponent.RangeDatas);
                    return;
                }

                var identifier = string.Format("{0}_{1}", catalog, fileComponent.RangeDatas);
                try
                {
                    var ranges = fileComponent.GetRanges();
                    foreach (var range in ranges)
                    {
                        sqlParameters[0] = new SqlParameter("@PIDRangeStart", range.Begin);
                        sqlParameters[1] = new SqlParameter("@PIDRangeEnd ", range.End);
                        identifier = string.Format("{0}_{1}_{2}", catalog, range.Begin, range.End);

                        var startDt = DateTime.Now;
                        Log.DebugFormat("[{0}] start", identifier);
                        using (var sqlCommand = new SqlCommand(fileComponent.StoredProcedureName, sqlConnection)
                        {
                            CommandType = CommandType.StoredProcedure,
                            CommandTimeout = SearchDataCommandTimeout
                        })
                        {
                            if (sqlParameters.Length == 2 && !(sqlParameters[0].Value.ToString() == "0" && sqlParameters[1].Value.ToString() == "99"))
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
                                    WriteFileComponentContent(xmlWriter, fileComponent, sqlDataReader, identifier, ref countRec);
                                }
                            }//using sqldatareader
                        } //using sqlCommand
                        var endDt = DateTime.Now;
                        var execTime = endDt - startDt;
                        Log.InfoFormat("[{0}] completed. Execution time in seconds: {1}", identifier, execTime.TotalSeconds);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("[Feed] {0} errored out.", identifier), ex);
                    _hasError = true;
                }
            }
        }

        private void WriteFileComponentContent(XmlWriter xmlWriter, FeedGenerationFileLineItem fileComponent, IDataReader reader, string identifier, ref Tuple<int, int, int> countRec)
        {
            var attributesDictionary = ConfigurationManager.GetSection(fileComponent.Catalogattributesection) as StringDictionary;
            if (countRec == null) throw new ArgumentNullException("countRec");
            var countProcessed = 0;
            var countDeleted = 0;
            var countError = 0;
            //<entry>
            while (reader.Read())
            {
                Log.DebugFormat("{0}::Processing record [{1}]: {2}", identifier, (countProcessed + countError + countDeleted), reader["PID"]);

                var id = reader[attributesDictionary["gId"]].ToString();
                var title = reader[attributesDictionary["title"]].ToString();
                try
                {
                    var haveRulesChanged = _runnerHelper.HaveRulesChanged;
                    var linkSku = reader[attributesDictionary["linkSku"]].ToString();
                    var brandName = !attributesDictionary.ContainsKey("gBrand") ? string.Empty : reader[attributesDictionary["gBrand"]].ToString();
                    var cContributors = GetContributors(attributesDictionary, reader);
                    // Get the breadcrumb value
                    var defaultCategory = FeedUtils.GetFeedGeneratorIndigoCategory(_feedGeneratorCategoryService, reader, attributesDictionary, fileComponent.Catalog, Log);
                    var productData = FeedUtils.GetProductData(attributesDictionary, reader, linkSku, fileComponent.Catalog, brandName, cContributors, defaultCategory);
                    var sanitizedTitle = (string.IsNullOrWhiteSpace(title)) ? string.Empty : FeedUtils.SanitizeString(title);
                    var gAvailability = !attributesDictionary.ContainsKey("gAvailability") ? FeedUtils.GetGoogleAvailability(1) : FeedUtils.GetGoogleAvailability((int)reader[attributesDictionary["gAvailability"]]);
                    var availability = !attributesDictionary.ContainsKey("gAvailability") ? 1 : (int)reader[attributesDictionary["gAvailability"]];
                    var recordType = !attributesDictionary.ContainsKey("recordType") ? string.Empty : reader[attributesDictionary["recordType"]].ToString();
                    var merchandiseType = !attributesDictionary.ContainsKey("merchandiseType") ? string.Empty : reader[attributesDictionary["merchandiseType"]].ToString();
                    if (string.IsNullOrWhiteSpace(merchandiseType))
                        _missingMerchandiseTypeProductInfos.Add(new MissingMerchandiseTypeProductInfo { Breadcrumb = defaultCategory.Breadcrumb, Sku = linkSku, Title = sanitizedTitle });

                    IRuleEvaluationResult newZeroCommissionResult = null;
                    IRuleEvaluationResult newPromotionalTextResult = null; 
                    if (_isIncrementalRun)
                    {
                        var isModifiedData = int.Parse(reader["IsModified"].ToString());
                        if (isModifiedData == 0)
                        {
                            if (!haveRulesChanged)
                            {
                                Log.DebugFormat(
                                    "Product with pid {0} is skipped in incremental mode as it wasn't modified and the rules haven't changed.",
                                    id);
                                continue;
                            }

                            var oldZeroCommissionResult = _runnerHelper.GetZeroCommissionRuleResult(productData, true);
                            newZeroCommissionResult = _runnerHelper.GetZeroCommissionRuleResult(productData, false);
                            var oldPromotionalTextResult = _runnerHelper.GetPromotionalTextRuleResult(productData, true);
                            newPromotionalTextResult = _runnerHelper.GetPromotionalTextRuleResult(productData, false);
                            if (oldZeroCommissionResult.HasMatch == newZeroCommissionResult.HasMatch
                                && oldPromotionalTextResult.HasMatch == newPromotionalTextResult.HasMatch
                                && oldPromotionalTextResult.MatchingRulePayLoads.First()
                                    .Equals(newPromotionalTextResult.MatchingRulePayLoads.First()))
                            {
                                countDeleted++;
                                Log.DebugFormat(
                                    "Product with pid {0} is skipped in incremental mode as it wasn't modified and rule evaluations yielded same results.",
                                    id);
                                continue;
                            }

                            // At this point, we know that rules have changed, which means we need to resend this product as modified
                        }
                        else
                        {
                            newZeroCommissionResult = _runnerHelper.GetZeroCommissionRuleResult(productData, false);
                            newPromotionalTextResult = _runnerHelper.GetPromotionalTextRuleResult(productData, false);
                        }
                    }
                    else
                    {
                        newZeroCommissionResult = _runnerHelper.GetZeroCommissionRuleResult(productData, false);
                        newPromotionalTextResult = _runnerHelper.GetPromotionalTextRuleResult(productData, false);
                    }

                    var isZeroCommissionElement = newZeroCommissionResult.HasMatch || IsZeroCommissionElement(sanitizedTitle, _feedId, true, availability, recordType);
                    if (isZeroCommissionElement)
                        Log.DebugFormat("Product with pid {0} is being placed in zero commission list due to either data issues or zero commission rules.)", id);

                    var linkCatalog = attributesDictionary["linkCatalog"];
                    if (string.IsNullOrWhiteSpace(sanitizedTitle))
                        sanitizedTitle = "(No Title)";
                    var description = reader[attributesDictionary["description"]].ToString();
                    description = string.IsNullOrWhiteSpace(description) ? sanitizedTitle : FeedUtils.SanitizeString(description);

                    // Get the breadcrumb value
                    var breadcrumb = defaultCategory.Breadcrumb;
                    var gPrice = string.IsNullOrEmpty(attributesDictionary["price"]) ? "" : reader[attributesDictionary["price"]].ToString();
                    var gAdjustedPrice = string.IsNullOrEmpty(attributesDictionary["adjustedPrice"]) ? string.Empty : reader[attributesDictionary["adjustedPrice"]].ToString();
                    var publisherName = !attributesDictionary.ContainsKey("publisherName") ? string.Empty : reader[attributesDictionary["publisherName"]].ToString();
                    if (!string.IsNullOrWhiteSpace(recordType) &&
                        recordType.Equals("GCard_Electronic", StringComparison.OrdinalIgnoreCase))
                    {
                        gPrice = DefaultEGiftCardPrice;
                        gAdjustedPrice = DefaultEGiftCardPrice;
                    }
                        

                    var entry = EntryAttribute(fileComponent.Catalog, reader, attributesDictionary
                                               , id
                                               , sanitizedTitle
                                               , linkCatalog, linkSku
                                               , gPrice
                                               , gAdjustedPrice
                                               , gAvailability
                                               , brandName, publisherName, cContributors, breadcrumb, isZeroCommissionElement, merchandiseType, newPromotionalTextResult, description);

                    countProcessed++;
                    //out of memory exception will be thrown if we keep the xml entries in memory 
                    entry.WriteTo(xmlWriter);
                }
                catch (Exception e)
                {
                    countError++;
                    Log.ErrorFormat("Can't process the item. Id:{0};title:{1}", id, title);
                    Log.DebugFormat("Error stack trace: {0}", e);
                    _executionLogLogger.AddCustomMessage(string.Format("Can't process the item. Id: {0};title: {1}, file identifier: {2}", id, title, identifier));
                    if (!AllowItemErrorsInFiles || _isIncrementalRun)
                        _hasError = true;
                }
            }

            // Now process the deleted items for the range & producttypeid and put them in the xml file as "deleted" items
            if (_isIncrementalRun)
            {
                // Don't do it for gift card file component 
                if (!fileComponent.StoredProcedureName.Equals("uspCJFeedGeneralMerchandiseGiftCard",
                        StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var deletedProductInfo in GetDeletedProductInfos(identifier))
                    {
                        GetDeletedElement(deletedProductInfo).WriteTo(xmlWriter);
                        countDeleted++;
                    }
                }
            }

            Log.InfoFormat("{0} completed. Processed record count: {1}, Error record count: {2}, deleted/skipped record count {3}", identifier, countProcessed, countError, countDeleted);
            countRec = new Tuple<int, int, int>(countRec.Item1 + countProcessed, countRec.Item2 + countError, countRec.Item3 + countDeleted);
        }

        private XE EntryAttribute(string catalog, IDataReader reader, StringDictionary dict
            , string gId
            , string sanitizedTitle
            , string linkCatalog
            , string linkSku
            , string gPrice
            , string gAdjustedPrice
            , string gAvailability
            , string gBrandName,
            string publisherName,
            XE contributors,
            string breadcrumb, bool isZeroCommissionProduct, string merchandiseType, IRuleEvaluationResult promotionalTextEvaluationResult, string description)
        {
            //entry
            var entry = new XE("product");
            sanitizedTitle = GetCjString(sanitizedTitle, MaxTitleLength); 
            var name = new XE("name", sanitizedTitle);
            entry.Add(name);

            var keywordsValue = string.Empty; 
            // First get the major contributor text
            if (!string.IsNullOrWhiteSpace(gBrandName))
                keywordsValue = gBrandName; 
            else if (contributors != null)
                keywordsValue = contributors.Value;

            // Now add the breadcrumb
            if (!string.IsNullOrWhiteSpace(keywordsValue))
                keywordsValue += ", ";

            keywordsValue += string.Join(", ", breadcrumb.Split(new[] {BreadcrumbTrailSplitter}, StringSplitOptions.RemoveEmptyEntries)) + ", ";
            keywordsValue += sanitizedTitle;
            var keywords = new XE("keywords", GetCjString(keywordsValue, MaxKeywordLength)); 
            entry.Add(keywords);

            var descriptionValue = GetCjString(FeedUtils.RemoveHtmlTags(description), MaxDescriptionLength);
            if (string.IsNullOrWhiteSpace(descriptionValue))
                descriptionValue = sanitizedTitle;
            var descriptionXe = new XE("description", descriptionValue);
            entry.Add(descriptionXe);

            var gaId = new XE("sku", gId);
            entry.Add(gaId);

            var aLink = FeedEntryLink(GetFeedEntryLinkValue(reader, linkCatalog, linkSku));
            entry.Add(aLink);

            var available = new XE("available", "Yes");
            entry.Add(available);

            var aImgLink = EntryImageLink(reader, linkSku);
            entry.Add(aImgLink);

            Decimal price;
            bool isSpecial = false; 
            if (DisplaySalePriceInfo)
            {
                if (Decimal.TryParse(gPrice, out price))
                {
                    var gaPrice = new XE("price", price.ToString("F", CultureInfo.InvariantCulture));
                    entry.Add(gaPrice);

                    Decimal salePrice;
                    if (!string.IsNullOrWhiteSpace(gAdjustedPrice) && Decimal.TryParse(gAdjustedPrice, out salePrice) && salePrice != price)
                    {
                        isSpecial = true;
                        var gaSalePrice = new XE("saleprice", salePrice.ToString("F", CultureInfo.InvariantCulture));
                        entry.Add(gaSalePrice);
                    }
                }
            }
            else
            {
                price = Decimal.Parse(gAdjustedPrice);
                var gaPrice = new XE("price", price.ToString("F", CultureInfo.InvariantCulture));
                entry.Add(gaPrice);
            }

            entry.Add(new XE("currency", "CAD"));

            // CJ expects the optional xml nodes in a certain order
            var isBook = catalog.Equals("books", StringComparison.OrdinalIgnoreCase);

            if (!isBook && (linkSku.Length == 12 || linkSku.Length == 13))
                entry.Add(new XE("upc", linkSku));

            if (promotionalTextEvaluationResult.HasMatch)
            {
                if (!promotionalTextEvaluationResult.IsDefaultMatch)
                    isSpecial = true;

                var promoTextValue = GetCjString(promotionalTextEvaluationResult.MatchingRulePayLoads.First(), MaxPromotionalTextLength);
                entry.Add(new XE("promotionaltext", promoTextValue));
            }

            var advertiserCategoryText = breadcrumb.Replace(BreadcrumbTrailSplitter, ">");
            entry.Add(new XE("advertisercategory", GetCjString(advertiserCategoryText, MaxAdvertiserCategoryLength)));

            if (!string.IsNullOrWhiteSpace(gBrandName))
            {
                var manufacturer = GetCjString(gBrandName, MaxManufacturerLength);
                entry.Add(new XE("manufacturer", manufacturer));
                entry.Add(new XE("manufacturerid", linkSku));
            }

            if (isBook)
            {
                entry.Add(new XE("isbn", linkSku));

                if (contributors != null)
                    entry.Add(new XE("author", GetCjString(contributors.Value, MaxManufacturerLength)));

                if (!string.IsNullOrWhiteSpace(publisherName))
                    entry.Add(new XE("publisher", GetCjString(publisherName, MaxManufacturerLength)));
            }
            else
            {
                if (contributors != null)
                    entry.Add(new XE("artist", GetCjString(contributors.Value, MaxManufacturerLength)));
            }

            entry.Add(new XE("title", sanitizedTitle));

            if (!isBook)
            {
                if (!string.IsNullOrWhiteSpace(publisherName))
                    entry.Add(new XE("label", GetCjString(publisherName, MaxManufacturerLength)));

                if (dict.ContainsKey("format"))
                {
                    var format = reader[dict["format"]].ToString();
                    if (!string.IsNullOrWhiteSpace(format))
                        entry.Add(new XE("format", format));
                }
            }

            entry.Add(new XE("special", (isSpecial) ? "Yes" : "No"));

            if (catalog.Equals("generalmerchandise", StringComparison.OrdinalIgnoreCase))
                entry.Add(new XE("gift", "Yes")); 

            entry.Add(new XE("instock", (string.IsNullOrWhiteSpace(gAvailability)) ? "No" : "Yes"));
            entry.Add(new XE("condition", "New"));

            if (!string.IsNullOrWhiteSpace(DefaultShippingCost))
                entry.Add(new XE("standardshippingcost", DefaultShippingCost));

            if (string.IsNullOrWhiteSpace(merchandiseType))
            {
                entry.Add(new XE("merchandisetype", ZeroCommissionListName));
            }
            else
            {
                if (isZeroCommissionProduct)
                {
                    entry.Add(new XE("merchandisetype", ZeroCommissionListName));
                    // If the merchandise type has a different value and it's not equal to zero commission list 
                    // and if we're putting the item on the zero commission list, add it to the list of items to be 
                    // updated inside the 
                    if (!merchandiseType.Equals(ZeroCommissionListName, StringComparison.OrdinalIgnoreCase))
                        _updatedMerchandiseTypeProductPids.Add(decimal.Parse(gId));

                    Log.DebugFormat("Product {0} was put in zero commission list.", gId);
                } 
                else
                    entry.Add(new XE("merchandisetype", merchandiseType));
            }

            return entry;
        }

        private static string GetCjString(string input, int maximumCharacterCount)
        {
            if (maximumCharacterCount == default(int))
                return input;

            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var result = EscapeCjSpecialCharacters(input);

            if (result.Length > maximumCharacterCount)
                result = result.Substring(0, maximumCharacterCount);

            var htmlVersion = HttpUtility.HtmlEncode(result);
            while (htmlVersion.Length > maximumCharacterCount)
            {
                if (!result.Contains(" "))
                {
                    result = result.Substring(0, maximumCharacterCount - (htmlVersion.Length - result.Length - 5));
                    break;
                }

                result = result.Substring(0, result.LastIndexOf(' '));
                htmlVersion = HttpUtility.HtmlEncode(result);
            }

            return result;
        }

        private static string EscapeCjSpecialCharacters(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            return input.Replace("'", "&apos;").Replace("\"", "&quot;");
        }

        private static string GetFeedEntryLinkValue(IDataReader reader, string catalog, string sku)
        {
            return catalog == "ebooks" ? reader["KoboItemPageURL"].ToString() : FeedUtils.GetProductUrl(BaseUrl, catalog, sku);
        }

        private static XE FeedEntryLink(string url) //, string title, int productTypeId, string browseSection)
        {
            return new XE("buyurl", url + UrlSuffix);
        }

        private static XE EntryImageLink(IDataRecord reader, string sku)
        {
            var imgSection = FeedUtils.GetImageFileSectionName(reader);
            var imgUrl = string.Format("{0}/{1}/{2}.jpg{3}", ImgBaseUrl, imgSection, sku, ImagePathSuffix);
            return new XE("imageurl", imgUrl);
        }

        private XE GetContributors(StringDictionary dict, IDataRecord reader)
        {
            if (!dict.ContainsKey("contributors")) return null;

            var contributors = FeedUtils.SanitizeString(reader[dict["contributors"]].ToString());
            var dictC = GetContributors(contributors);

            if (dictC.IsNullOrEmpty()) return null;

            // If there are multiple keys in the dictionary, log an info message and pick the first one and move on
            if (dictC.Count > 1) 
                Log.DebugFormat("Item {0} contained multiple contributor types.", reader[dict["gId"]].ToString());

            const string star = "star";
            bool dictContainsStar = dictC.ContainsKey(star);

            return new XE(dictC.First().Key, string.Join(", ", dictC.First(keyValuePair => 
                {
                    if (dictContainsStar && keyValuePair.Key.Equals(star))
                        return true;

                    if (!dictC.ContainsKey(star))
                        return true;

                    return false;

                }).Value));
        }

        private static Dictionary<string, List<string>> GetContributors(string contributors)
        {
            if (string.IsNullOrWhiteSpace(contributors))
            {
                return null;
            }

            var listC = new Dictionary<string, List<string>>();

            //get first word from the arg

            var words = contributors.Split(new[] { '^' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                var cType = word.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var name = cType[0];
                var value = word.TrimStart(name.ToCharArray()).Trim();
                //string trim;

                switch (name.ToLowerInvariant())
                {
                    //case "artist1":             //endeca    //~[tbl_001_ContributorRoles]
                    case "author":              //endeca
                    case "director":            //endeca    //~[tbl_001_ContributorRoles]
                    case "star":
                    //case "editor":              //endeca
                    //case "engineer":            //endeca    //~[tbl_001_ContributorRoles]
                    //case "illustrator":         //endeca
                    //case "other":               //endeca
                    case "performer":           //endeca    //~[tbl_001_ContributorRoles]
                    case "contributors": //by
                        //case "producer":            //endeca    //~[tbl_001_ContributorRoles]
                    //case "star":                //endeca    //~[tbl_001_ContributorRoles]      
                        break;
                    //case "afterword": //by      //endeca
                    //case "foreword": //by       //endeca
                    //case "introduction": //by   //endeca
                    //case "narrated": //by       //endeca

                    //case "photographed": //by   //endeca
                    //case "preface": //by        //endeca

                    //case "translated": //by     //endeca
                    //    //case ""
                    //    trim = string.Format("{0} {1}", name, cType[1]);
                    //    name += "By";
                    //    valu = word.TrimStart(trim.ToCharArray()).Trim();
                    //    break;
                    //case "as": //told by or as told to
                    //    trim = string.Format("{0} {1} {2}", name, cType[1], cType[2]);
                    //    name += string.Format("Told{0}", cType[2]);
                    //    valu = word.TrimStart(trim.ToCharArray()).Trim();
                    //    break;
                    //case "with":
                    //case "abridged": //by       //~[tbl_001_ContributorRoles]
                    //case "annotations": //by
                    //case "arranged": //by
                    //case "epilogue": //by
                    //case "memoir": //by
                    //case "notes": //by
                    //case "prologue": //by
                    //case "read": //by
                    //case "retold": //by
                    //case "revised": //by
                    //case "text": //by
                    //case "transcribed": //by
                    //case "featured":
                    //case "voice":
                    //    continue;
                    default:
                        name = string.Empty;
                        value = string.Empty; 
                        break;
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    var key = name.ToLowerInvariant();
                    if (listC.ContainsKey(key))
                        listC[key].Add(value);
                    else
                    {
                        listC.Add(key, new List<string> { value });
                    }
                        
                }
            }

            return listC;
        }

        /// <summary>
        /// creates feed file name based on supplied identifier. creates folder if does not exist
        /// </summary>
        /// <param name="fileInstruction"></param>
        /// <returns>feed file path</returns>
        private static string GetFeedFileName(FeedGenerationFileInstruction fileInstruction)
        {
            //feed file name
            var fileName = String.Format(FileNameFormat, fileInstruction.Key);
            if (!Directory.Exists(OutputFolderPath))
            {
                Directory.CreateDirectory(OutputFolderPath);
            }
            return Path.Combine(OutputFolderPath, fileName);
        }

        private bool IsZeroCommissionElement(string sanitizedTitle, int feedId, bool hasImage, int? availabilityId, string recordType)
        {
            string message;
            return IndigoBreadcrumbRepositoryUtils.IsExcludedDueToData(feedId, sanitizedTitle, hasImage, availabilityId, recordType, false, out message);
        }

        private static XE GetDeletedElement(DeletedProductInfo deletedProductInfo)
        {
            var entry = new XE("product");

            var name = new XE("name", deletedProductInfo.Name);
            entry.Add(name);

            var keywords = new XE("keywords", deletedProductInfo.Keywords);
            entry.Add(keywords);

            var description = new XE("description", deletedProductInfo.Description);
            entry.Add(description);

            var gaId = new XE("sku", deletedProductInfo.Pid);
            entry.Add(gaId);

            var buyUrl = new XE("buyurl", deletedProductInfo.BuyUrl);
            entry.Add(buyUrl);

            var available = new XE("available", deletedProductInfo.IsAvailable);
            entry.Add(available);

            var price = new XE("price", deletedProductInfo.Price);
            entry.Add(price);

            var merchandiseType = new XE("merchandisetype", ZeroCommissionListName);
            entry.Add(merchandiseType);

            return entry;
        }

        private void PopulateDeletedProductInfo(DateTime fromTime)
        {
            var datas = new List<DeletedProductInfo>();
            using (var sqlConnection = new SqlConnection(OdysseyCommerceConnectionString))
            {
                sqlConnection.Open();
                using (var sqlCommand = new SqlCommand(IncrementalRunDeletedProductsStoredProcedureName, sqlConnection)
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
                                datas.Add(new DeletedProductInfo { Pid = sqlDataReader["PID"].ToString(), ProductTypeId = int.Parse(sqlDataReader["ProductTypeId"].ToString()) });
                            }
                        }
                    }//using sqldatareader
                } //using sqlCommand    
            }

            Log.InfoFormat("There were {0} deleted products retrieved from the database", datas.Count);
            _deletedProductInfos = datas.GroupBy(dpi => (dpi.Pid.Length <= 2) ? int.Parse(dpi.Pid) : int.Parse(dpi.Pid.Substring(dpi.Pid.Length - 2))).ToDictionary(kvp => kvp.Key, kvp => kvp.ToList());
        }

        private IEnumerable<DeletedProductInfo> GetDeletedProductInfos(string identifier)
        {
            var parts = identifier.Split(new[] {'_'});
            var catalog = parts[0];
            var rangeBegin = int.Parse(parts[1]);
            var rangeEnd = int.Parse(parts[2]);

            var candidates = _deletedProductInfos.Where(kvp => kvp.Key >= rangeBegin && kvp.Key <= rangeEnd).Select(kvp => kvp.Value).ToList();

            var result = new List<DeletedProductInfo>();
            if (!candidates.Any())
                return result; 

            switch (catalog) 
            {
                case "books":
                    return GetMatchingDeletedProductInfos(candidates, new [] {1});
                case "generalMerchandise":
                    var otherProductTypeIds = new[] {0, 1, 2, 3, 7};
                    foreach (var list in candidates)
                    {
                        result.AddRange(list.Where(deletedProductInfo => !otherProductTypeIds.Contains(deletedProductInfo.ProductTypeId)));
                    }
                    return result;
                default: 
                    throw new ArgumentException("Invalid identifier.");
            }
        }

        private IEnumerable<DeletedProductInfo> GetMatchingDeletedProductInfos(IEnumerable<List<DeletedProductInfo>> productInfos, ICollection<int> productTypeIds)
        {
            var result = new List<DeletedProductInfo>();
            foreach (var list in productInfos)
            {
                result.AddRange(list.Where(deletedProductInfo => productTypeIds.Contains(deletedProductInfo.ProductTypeId)));
            }
            return result;
        }

        private struct DeletedProductInfo
        {
            public string Pid { get; set; }
            public int ProductTypeId { get; set; }
            public string Name { get { return "Product is removed."; } }
            public string Description { get { return "Product is removed.";  }}
            public string Keywords { get { return "Proudct is removed.";  } }
            public string BuyUrl { get { return BaseUrl; } }
            public string IsAvailable { get { return "No"; }}
            public string Price { get { return "0.00"; } }
        }

        private struct MissingMerchandiseTypeProductInfo
        {
            public string Sku { get; set; }
            public string Breadcrumb { get; set; }
            public string Title { get; set; }
        }

        private void ProcessMissingMerchandiseTypeProductInfos()
        {
            if (!Directory.Exists(AncillaryOutputFolderPath))
                Directory.CreateDirectory(AncillaryOutputFolderPath);

            var filePath = Path.Combine(AncillaryOutputFolderPath, MissingMerchandiseTypeFileName);
            if (File.Exists(filePath)) 
                File.Delete(filePath);

            using (var invalidEntriesStreamWriter = new StreamWriter(filePath))
			{
                foreach (var productInfo in _missingMerchandiseTypeProductInfos)
                {
                    invalidEntriesStreamWriter.WriteLine(productInfo.Sku + ", " + productInfo.Title + ", " + productInfo.Breadcrumb);
                }
			}
        }

        private void ProcessUpdatedMerchandiseTypeProductPids()
        {
            if (!_updatedMerchandiseTypeProductPids.Any())
            {
                Log.Info("There were no products that required merch types to be updated inside CJ table.");
                return;
            }

            var query = string.Empty;
            var time = DateTime.Now;
            const string insertStatementFormat = "UPDATE [dbo].[tbl_CJ_Products] SET [CJ_MERCHANDISETYPE] = '{0}', [DATECHANGED] = '{1}' WHERE [PID] = {2} ";
            query = _updatedMerchandiseTypeProductPids.Aggregate(query, (current, updatedMerchandiseTypeProductPid) => current + string.Format(insertStatementFormat, ZeroCommissionListName, time.ToString("yyyy-MM-dd HH:mm:ss"), updatedMerchandiseTypeProductPid));

            DbUtils.ExecuteTransaction("BatchDB", query, null);
            Log.InfoFormat("There were {0} products whose merch types were updated inside CJ table.", _updatedMerchandiseTypeProductPids.Count);
        }
    }
}