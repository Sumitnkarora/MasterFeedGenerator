using BVRatingImporter.Entities;
using BVRatingImporter.Repositories;
using BVRatingImporter.XmlSerialization;
using Castle.Core.Internal;
using Castle.Core.Logging;
using FeedGenerators.Core.Services.Abstract;
using Indigo.Feeds.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace BVRatingImporter.Execution
{
    public class Runner : IRunner
    {
        private IExecutionLogLogger _executionLogLogger;
        private RatingsImportRepository _ratingsImportRepository;
        private bool _isIncrementalRun;
        private bool _hasError;
        private long _executionLogLoggerMessageCount = 0;

        private static readonly string DownloadFolderPath = ParameterUtils.GetParameter<string>("BVRatingImporter.DownloadFolderPath");
        private static readonly string RatingsXmlFileName = ParameterUtils.GetParameter<string>("RatingsXmlFileName");
        private static readonly int LimitToNumberOfProducts = ParameterUtils.GetParameter<int>("LimitToNumberOfProducts");
        private static readonly int FullRunBatchSize = ParameterUtils.GetParameter<int>("FullRunBatchSize");
        private static readonly int ExecutionLogLoggerMessageLimit = ParameterUtils.GetParameter<int>("ExecutionLogLoggerMessageLimit");
        private static readonly int IncrementalProductsLogCount = ParameterUtils.GetParameter<int>("IncrementalProductsLogCount");

        public ILogger Log { get; set; }

        public void Initialize(IExecutionLogLogger executionLogLogger, int feedId, bool isIncremental,
            DateTime? fromTime, DateTime? effectiveFromTime, DateTime executionTime)
        {
            _executionLogLogger = executionLogLogger;
            _isIncrementalRun = isIncremental;
            _ratingsImportRepository = new RatingsImportRepository(Log);
        }

        public IExecutionLogLogger Execute()
        {
            try
            {
                if (_isIncrementalRun)
                {
                    ExecuteIncrementalRun();
                }
                else
                {
                    ExecuteFullInitialRun();
                }
            }
            catch (Exception ex)
            {
                _hasError = true;
                Log.Error("Error running import procedure.", ex);
            }
            
            _executionLogLogger.HasError = _hasError;
            _executionLogLogger.SetExecutionEndTime(DateTime.Now);
            return _executionLogLogger;
        }

        private void ExecuteRun(Action<IEnumerable<ProductRating>> mainCallback)
        {
            const string productNodeName = "Product";
            long productsCounter = 0;
            var serializer = new XmlSerializer(typeof (Product), "http://www.bazaarvoice.com/xs/PRR/SyndicationFeed/5.6");

            long incrementalLogProductsCounter = 0;

            using (var xmlReader = XmlReader.Create(Path.Combine(DownloadFolderPath, RatingsXmlFileName)))
            {
                if (!xmlReader.ReadToFollowing(productNodeName))
                {
                    throw new ApplicationException("No Product nodes in xml file.");
                }

                // Create a loop to loop through product nodes
                Log.Debug("Entering do while loop");

                Product product = null;
                try
                {
                    bool readState = true;

                    do
                    {
                        // Inside the loop, for each product:

                        // Instantiate a Product object
                        var currentProductXmlString = xmlReader.ReadOuterXml();

                        product =
                            (Product)
                                serializer.Deserialize(new MemoryStream(Encoding.UTF8.GetBytes(currentProductXmlString)));

                        var localizedProductRatings = GetProductRatings(product);
                        productsCounter++;
                        incrementalLogProductsCounter++;

                        if (incrementalLogProductsCounter >= IncrementalProductsLogCount)
                        {
                            Log.Debug("# of products done:" + productsCounter);
                            incrementalLogProductsCounter = 0;
                        }

                        mainCallback(localizedProductRatings);
                        
                        Log.DebugFormat("PID: {0}, # of rows {1}, Product counter {2}", product.Id,
                            localizedProductRatings.Count(), productsCounter);

                        if (!localizedProductRatings.Any())
                        {
                            Log.InfoFormat("Warning: Zero Product Ratings objects/rows generated for PID {0}",
                                product.Id);
                        }

                        if (xmlReader.EOF)
                        {
                            break;
                        }

                        // Advance to the next product in the xml file.
                        if (!xmlReader.Name.Equals(productNodeName, StringComparison.Ordinal))
                        {
                            readState = xmlReader.ReadToFollowing(productNodeName);
                        }

                    } while (readState && (LimitToNumberOfProducts == 0 || productsCounter < LimitToNumberOfProducts));
                }
                catch (Exception)
                {
                    var message = "Error in ExecuteRun() routine. Latest PID: " +
                                  (product != null ? product.Id : "<not available>");
                    Log.Error(message);
                    _executionLogLogger.AddCustomMessage(message);

                    throw;
                }
                finally
                {
                    var message = string.Format("ExecuteRun(). Total product/language pairs processed: {0}",
                        productsCounter);

                    _executionLogLogger.AddCustomMessage(message);
                    Log.Info(message);
                }

                Log.Debug("Exited do while loop");
            }

        }

        private void ExecuteFullInitialRun()
        {
            Log.Info("Executing a full run.");
            Log.Debug("Entering ExecuteFullInitialRun");

            // Truncate the database via the repository
            _ratingsImportRepository.RemoveAllRatings();

            // Create a counter for number of products processed
            int batchProductsCounter = 0;
            var productRatingsList = new List<ProductRating>();

            ExecuteRun(generatedProductRatingItems =>
            {
                productRatingsList.AddRange(generatedProductRatingItems);

                batchProductsCounter += generatedProductRatingItems.Count();

                if (batchProductsCounter >= FullRunBatchSize)
                {
                    _ratingsImportRepository.BulkInsert(productRatingsList);

                    batchProductsCounter = 0;
                    productRatingsList = new List<ProductRating>();
                }
            });

            if (batchProductsCounter > 0)
            {
                _ratingsImportRepository.BulkInsert(productRatingsList);
            }

            Log.Debug("Exited ExecuteFullInitialRun");
        }

        private void ExecuteIncrementalRun()
        {
            Log.Info("Executing an incremental run.");
            // Create a hashset and populate with the unique pid call from the repository

            var dbPidSet = _ratingsImportRepository.GetPidsWithRatings();

            // Create a counter for number of products updated
            // Create a counter for number of products deleted
            // Create a counter for number of products inserted
            long productsUpdated = 0, productsInserted = 0, productsUnchanged = 0;

            // Open an XmlReader and point it to the unzipped file
            // Create a loop to loop through product nodes
            ExecuteRun(generatedProductRatingItems =>
            {
                // Inside the loop, for each product: 
                // Get the existing ProductRatings for the pid via the repository

                // Compare the ProductRatings from the file to the ProductRatings from the database
                // If you find any difference, take appropriate action where actions are insert, update or delete

                var productRatingItems = generatedProductRatingItems as IList<ProductRating> ?? generatedProductRatingItems.ToList();
                var first = productRatingItems.FirstOrDefault();
                var xmlPid = first != null ? first.PID : (long?) null;

                if (xmlPid.HasValue)
                {
                    Log.Debug("xmlPid.HasValue");

                    if (!dbPidSet.Contains(xmlPid.Value))
                    {
                        Log.Debug("false == dbPidSet.Contains(xmlPid.Value)");
                        // Insert
                        productRatingItems.ForEach(xmlProductRating =>
                        {
                            Log.Debug("Inserting");
                            Log.Debug(xmlProductRating.ToString());
                            // NOTE: The SP that handles the insert checks if the current entry exists in the db and updates if it does
                            _ratingsImportRepository.Insert(xmlProductRating);
                                
                            Log.Debug("Inserted PID: " + xmlProductRating.PID);
                            productsInserted++;
                        });
                    }
                    else
                    {
                        Log.Debug("true == dbPidSet.Contains(xmlPid.Value)");

                        var dbProductRatingList = _ratingsImportRepository.GetRatings(xmlPid.Value);

                        productRatingItems.ForEach(xmlProductRating =>
                        {
                            foreach (var dbProductRating in dbProductRatingList)
                            {
                                if (xmlProductRating.LanguageString.Equals(dbProductRating.LanguageString,
                                    StringComparison.Ordinal))
                                {
                                    // Update
                                    if (!xmlProductRating.Equals(dbProductRating))
                                    {
                                        Log.Debug("Updating");
                                        Log.Debug(xmlProductRating.ToString());

                                        _ratingsImportRepository.Update(xmlProductRating);
                                        
                                        Log.Debug("Updated PID: " + xmlProductRating.PID);
                                        productsUpdated++;
                                    }
                                    else
                                    {
                                        productsUnchanged++;
                                        Log.Debug("Unchanged PID :" + xmlProductRating.PID);
                                    }

                                    return;
                                }
                            }

                            // Insert
                            Log.Debug("Inserting");
                            Log.Debug(xmlProductRating.ToString());

                            // NOTE: The SP that handles the insert checks if the current entry exists in the db and updates if it does
                            _ratingsImportRepository.Insert(xmlProductRating);
                            
                            Log.Debug("Inserted PID: " + xmlProductRating.PID);
                            productsInserted++;
                        });

                        // Remove pid from the hashset
                        dbPidSet.Remove(xmlPid.Value);
                    }
                }
            });

            // Once the loop has completed execution, take what's inside the hashset, and remove all ProductRatings for that pid via a call to the repository
            dbPidSet.ForEach(pid =>
            {
                _ratingsImportRepository.Delete(pid);
                Log.Info("Deleted PID: " + pid);
            });            
            Log.Info("Deleted count: " + dbPidSet.Count());

            // Add messages to Logger and ExecutionLogger
            string message = string.Format("ExecuteIncrementalRun complete. Inserted: {0}, Updated: {1}, Deleted: {2}, Unchanged {3}",
                productsInserted, productsUpdated, dbPidSet.Count(), productsUnchanged);

            Log.Info(message);
            _executionLogLogger.AddCustomMessage(message);
        }

        #region Helper Methods

        private IEnumerable<string> GetDisplayLocaleList(IEnumerable<LocaleDistributionItem> itemList)
        {
            var result = itemList.Select(item => item.DisplayLocale);
            return result;
        }

        private IEnumerable<ProductRating> GetProductRatings(Product product)
        {
            Log.Debug("In GetProductRatings");

            long productId;

            if (!long.TryParse(product.Id, out productId))
            {
                var message = string.Format("Product ID is not in the correct format. Skipping. PID: {0}", product.Id);
                Log.Error(message);
                if (ExecutionLogLoggerMessageLimit == 0 || _executionLogLoggerMessageCount++ <= ExecutionLogLoggerMessageLimit)
                {
                    _executionLogLogger.AddCustomMessage(message);
                }
                else if (ExecutionLogLoggerMessageLimit != 0)
                {
                    Log.ErrorFormat("ExecutionLogLogger message limit of {0} has been exceeded.",
                        ExecutionLogLoggerMessageLimit);
                }

                return Enumerable.Empty<ProductRating>();
            }

            var externalReviewStatistics = ComputeExternalReviewStatistics(product.Id, product.NativeReviewStatistics,
                product.ReviewStatistics);

            var nativeLocaleDistributionItems = GetAggregatedLocaleDistributionItems(product.NativeReviewStatistics);
            var externalLocaleDistributionItems = GetAggregatedLocaleDistributionItems(externalReviewStatistics);

            var generalLocaleList = GetCombinedLocaleList(GetDisplayLocaleList(nativeLocaleDistributionItems), GetDisplayLocaleList(externalLocaleDistributionItems));

            var result = generalLocaleList.Select(generalLocaleString =>
            {
                LocaleDistributionItem nativeDistributionItem =
                    nativeLocaleDistributionItems.FirstOrDefault(
                        item => item.DisplayLocale.Equals(generalLocaleString, StringComparison.OrdinalIgnoreCase));

                LocaleDistributionItem externalDistributionItem =
                    externalLocaleDistributionItems.FirstOrDefault(
                        item => item.DisplayLocale.Equals(generalLocaleString, StringComparison.OrdinalIgnoreCase));

                var localizedNativeReviewStatistics = GetReviewStatistics(nativeDistributionItem);
                var localizedExternalReviewStatistics = GetReviewStatistics(externalDistributionItem);

                var resultItem = new ProductRating(productId, generalLocaleString,
                    localizedNativeReviewStatistics,
                    localizedExternalReviewStatistics);

                return resultItem;
            });

            Log.Debug("Exiting GetProductRatings");

            return result;
        }

        private static ReviewStatistics GetReviewStatistics(LocaleDistributionItem localeDistributionItem)
        {
            var result = (localeDistributionItem ?? LocaleDistributionItem.Empty).ReviewStatistics;
            return result;
        }

        private static IEnumerable<string> GetCombinedLocaleList(IEnumerable<string> nativeLanguageStrings, IEnumerable<string> externalLanguageStrings)
        {
            var result = nativeLanguageStrings.Union(externalLanguageStrings);
            return result;
        }

        private ReviewStatistics ComputeExternalReviewStatistics(string pid, ReviewStatistics nativeReviewStatistics, ReviewStatistics reviewStatisticsNodeFromBazaarVoiceData)
        {
            var result = new ReviewStatistics();

            var resultLocaleDistributionList = new List<LocaleDistributionItem>();
            result.LocaleDistribution = resultLocaleDistributionList;

            foreach (var nativeLocaleDistributionItem in nativeReviewStatistics.LocaleDistribution ?? Enumerable.Empty<LocaleDistributionItem>())
            {
                var matchFound = false;

                foreach (var combinedLocaleDistributionItem in reviewStatisticsNodeFromBazaarVoiceData.LocaleDistribution ?? Enumerable.Empty<LocaleDistributionItem>())
                {
                    if (nativeLocaleDistributionItem.DisplayLocale.Equals(combinedLocaleDistributionItem.DisplayLocale, StringComparison.Ordinal))
                    {
                        matchFound = true;
                        break;
                    }
                }

                if (matchFound)
                {
                    continue;
                }

                throw new ApplicationException(
                    string.Format(
                        "There is a locale {0} in the native LocaleDistributionItem which doesn't exist in the combined LocaleDistributionItem. PID: {1}",
                        nativeLocaleDistributionItem.DisplayLocale, pid));
            }

            foreach (var combinedLocaleDistributionItem in reviewStatisticsNodeFromBazaarVoiceData.LocaleDistribution ?? Enumerable.Empty<LocaleDistributionItem>())
            {
                ReviewStatistics resultReviewStatistics;
                LocaleDistributionItem foundNativeLocaleDistributionItem = null;
                
                foreach (var nativeLocaleDistributionItem in nativeReviewStatistics.LocaleDistribution ?? Enumerable.Empty<LocaleDistributionItem>())
                {
                    if (combinedLocaleDistributionItem.DisplayLocale.Equals(nativeLocaleDistributionItem.DisplayLocale, StringComparison.Ordinal))
                    {
                        foundNativeLocaleDistributionItem = nativeLocaleDistributionItem;
                        break;
                    }
                }

                if (foundNativeLocaleDistributionItem != null)
                {
                    Log.DebugFormat("GetExternalReviewStatistics. PID: {0}", pid);
                    resultReviewStatistics = GetExternalReviewStatistics(pid, foundNativeLocaleDistributionItem, combinedLocaleDistributionItem);
                }
                else
                {
                    resultReviewStatistics = GetCopyOfReviewStatistics(combinedLocaleDistributionItem);
                }

                var resultLocaleDistributionItem = new LocaleDistributionItem
                {
                    DisplayLocale = combinedLocaleDistributionItem.DisplayLocale,
                    ReviewStatistics = resultReviewStatistics
                };

                resultLocaleDistributionList.Add(resultLocaleDistributionItem);
            }

            return result;
        }

        private static ReviewStatistics GetCopyOfReviewStatistics(LocaleDistributionItem localeDistributionItem)
        {
            var result = new ReviewStatistics();

            result.RatingDistribution = localeDistributionItem.ReviewStatistics.RatingDistribution.Select(
                item => new RatingDistributionItem {RatingValue = item.RatingValue, Count = item.Count})
                .ToList();

            result.TotalReviewCount = localeDistributionItem.ReviewStatistics.TotalReviewCount;
            
            return result;
        }

        private static ReviewStatistics GetExternalReviewStatistics(string pid, LocaleDistributionItem nativeLocaleDistributionItem, LocaleDistributionItem combinedLocaleDistributionItem)
        {
            var result = new ReviewStatistics {RatingDistribution = new List<RatingDistributionItem>(5)};

            for (int i = 1; i <= 5; i++)
            {
                var nativeItemCount = GetRatingDistributionItemCount(i, nativeLocaleDistributionItem);
                var combinedItemCount = GetRatingDistributionItemCount(i, combinedLocaleDistributionItem);

                if (combinedItemCount == null && nativeItemCount == null)
                {
                    continue;
                }

                if (nativeItemCount == null)
                {
                    nativeItemCount = 0;
                }

                if (combinedItemCount == null)
                {
                    combinedItemCount = 0;
                }

                if (combinedItemCount >= nativeItemCount)
                    combinedItemCount = combinedItemCount - nativeItemCount;
                else if (nativeItemCount > 0)
                    combinedItemCount = 0;

                var externalItemCount = combinedItemCount;

                result.RatingDistribution.Add(new RatingDistributionItem
                {
                    RatingValue = i,
                    Count = externalItemCount.Value
                });
            }

            result.TotalReviewCount = result.RatingDistribution.Sum(rd => rd.Count);

            return result;
        }

        internal static int? GetRatingDistributionItemCount(int ratingValue, LocaleDistributionItem item)
        {
            var result =
                item.ReviewStatistics.RatingDistribution.FirstOrDefault(
                    ratingDistributionItem => ratingDistributionItem.RatingValue == ratingValue);

            return result != null ? (int?)result.Count : null;
        }

        private class GeneralLocalLocaleDistributionItemComparer : IEqualityComparer<LocaleDistributionItem>
        {

            public bool Equals(LocaleDistributionItem x, LocaleDistributionItem y)
            {
                var result = GetGeneralLocaleString(x.DisplayLocale).Equals(GetGeneralLocaleString(y.DisplayLocale), StringComparison.OrdinalIgnoreCase);
                return result;
            }

            public int GetHashCode(LocaleDistributionItem obj)
            {
                var result = GetGeneralLocaleString(obj.DisplayLocale).ToLowerInvariant().GetHashCode();
                return result;
            }
        }

        private static readonly GeneralLocalLocaleDistributionItemComparer GeneralLocalLocaleDistributionItemComparerObject =
            new GeneralLocalLocaleDistributionItemComparer();

        private static IEnumerable<LocaleDistributionItem> GetAggregatedLocaleDistributionItems(
            ReviewStatistics reviewStatistics)
        {
            var distinctGeneralLocaleStrings = reviewStatistics.LocaleDistribution.Distinct(GeneralLocalLocaleDistributionItemComparerObject)
                .Select(item => GetGeneralLocaleString(item.DisplayLocale));

            var result = distinctGeneralLocaleStrings.Select(localeString =>
                reviewStatistics.LocaleDistribution.Where(
                    item => GetGeneralLocaleString(item.DisplayLocale).Equals(localeString, StringComparison.OrdinalIgnoreCase))
                    .Select(
                        item =>
                            new LocaleDistributionItem
                            {
                                DisplayLocale = GetGeneralLocaleString(item.DisplayLocale),
                                ReviewStatistics = item.ReviewStatistics
                            })
                    .Aggregate((aggregate, next) => aggregate + next));
            
            return result;
        }

        private static string GetGeneralLocaleString(string specificLocaleString)
        {
            var result = specificLocaleString.Substring(0, 2);
            return result;
        }

        #endregion Helper Methods
    }

}