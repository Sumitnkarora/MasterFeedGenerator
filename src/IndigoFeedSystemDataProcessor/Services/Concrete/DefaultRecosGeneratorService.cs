using Castle.Core.Logging;
using Dapper;
using Indigo.CSI;
using Indigo.CSI.Client.WcfHelpers;
using Indigo.CSI.Data.Entities.Merchandising;
using Indigo.CSI.Data.Entities.ProductDetails;
using Indigo.CSI.Shared.Enums;
using Indigo.Feeds.Utils;
using Indigo.Utils;
using IndigoFeedSystemDataProcessor.Services.Contract;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace IndigoFeedSystemDataProcessor.Services.Concrete
{
    public class DefaultRecosGeneratorService : IDefaultRecosGeneratorService
    {
        private const string DefaultRecosProductListsSetting = "Default Recommendation Cms Product List IDs";
        private const string DefaultFrenchRecosProductListsSetting = "Default French Recommendation Cms Product List IDs";
        private static readonly string AllowedDefaultRecommendationLanguageIdsValue = ParameterUtils.GetParameter<string>("AllowedDefaultRecommendationLanguageIds");
        private static readonly List<int> AllowedDefaultRecommendationLanguageIds;

        private readonly IWcfClientFactory<IMerchandisingServiceContract> _merchandisingServiceFactory;
        private readonly IWcfClientFactory<ICatalogueServiceContract> _catalogueServiceFactory;

        private int _productListsDeleted;
        private int _productListsAdded;
        private int _productListsUpdated;
        private int _productListsUnchanged;

        public ILogger Log { get; set; }

        static DefaultRecosGeneratorService()
        {
            AllowedDefaultRecommendationLanguageIds = new List<int>();
            if (string.IsNullOrWhiteSpace(AllowedDefaultRecommendationLanguageIdsValue))
            {
                return;
            }

            foreach (var allowedDefaultRecommendationLanguageId in AllowedDefaultRecommendationLanguageIdsValue.Split(new [] {','}, StringSplitOptions.RemoveEmptyEntries))
            {
                AllowedDefaultRecommendationLanguageIds.Add(int.Parse(allowedDefaultRecommendationLanguageId.Trim()));
            }
        }

        public DefaultRecosGeneratorService(IWcfClientFactory<IMerchandisingServiceContract> merchandisingServiceFactory, IWcfClientFactory<ICatalogueServiceContract> catalogueServiceFactory)
        {
            _merchandisingServiceFactory = merchandisingServiceFactory;
            _catalogueServiceFactory = catalogueServiceFactory;
        }

        public void Run()
        {
            foreach (var allowedDefaultRecommendationLanguageId in AllowedDefaultRecommendationLanguageIds)
            {
                ResetCounts(); 

                // Get the product lists we want to add to the default recos table
                var newProductListIds = GetNewProductListIds(allowedDefaultRecommendationLanguageId);

                // Get the product lists currently in the default recos table
                var existingProductListIds = GetExistingProductListIds(allowedDefaultRecommendationLanguageId);

                // Determine the product lists that have been deleted from the config
                var deletedProductListIds = existingProductListIds.Except(newProductListIds);

                // Delete all recos that came from deleted product lists
                foreach (var id in deletedProductListIds)
                {
                    Log.InfoFormat("Deleting product list {0} for language id of {1}", id, allowedDefaultRecommendationLanguageId);

                    DeleteRecosForProductList(id, allowedDefaultRecommendationLanguageId, false);

                    _productListsDeleted++;
                }

                // Add all new recos from the product lists we loaded from CMS
                foreach (var id in newProductListIds)
                {
                    UpdateRecosFromProductList(id, allowedDefaultRecommendationLanguageId);
                }

                // First delete all items from the order table
                DeleteProductListFromOrderTable(allowedDefaultRecommendationLanguageId);

                var order = 1;
                foreach (var newProductListId in newProductListIds)
                {
                    // Add items to the order table
                    AddOrdering(newProductListId, order, allowedDefaultRecommendationLanguageId);
                    order++;
                }

                Log.InfoFormat("There were {0} new, {1} modified, {2} deleted, and {3} unchanged product lists for language id of {4}.", _productListsAdded, _productListsUpdated, _productListsDeleted, _productListsUnchanged, allowedDefaultRecommendationLanguageId);    
            }
        }

        private void ResetCounts()
        {
            _productListsAdded = 0;
            _productListsUpdated = 0;
            _productListsDeleted = 0; 
            _productListsUnchanged = 0;
        }

        /// <summary>
        /// Gets the CMS content IDs of the product lists to use for default recommendations.
        /// </summary>
        private long[] GetNewProductListIds(int allowedDefaultRecommendationLanguageId)
        {
            // Load Rewards Centre config from CMS
            // It contains a setting specifying the product lists to use for default recos

            IDictionary<string, string> rewardsCentreSettings;

            try
            {
                var cmsRewardsCentreConfigId = ParameterUtils.GetParameter<int>("CmsRewardsCentreConfigId");

                Log.InfoFormat("Loading Rewards Centre config from CMS using content ID {0} for language id of {1}.", cmsRewardsCentreConfigId, allowedDefaultRecommendationLanguageId);

                using (var merchandisingService = _merchandisingServiceFactory.Create())
                {
                    rewardsCentreSettings = merchandisingService.ServiceClient.GetSettingsNoCache(cmsRewardsCentreConfigId, false);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to load Rewards Centre config from CMS", ex);
            }

            // Get the product list IDs setting
            var setting = GetProductListSetting(allowedDefaultRecommendationLanguageId);
            string productListIdsSetting;
            rewardsCentreSettings.TryGetValue(setting, out productListIdsSetting);

            if (string.IsNullOrWhiteSpace(productListIdsSetting))
            {
                // All list ids have been removed from CMS config. log a message and delete all the entries in recosdb
                Log.InfoFormat("All product list ids have been removed from CMS configuration for language id of {0}. Removing all entries in recosdb as a result.", allowedDefaultRecommendationLanguageId);

                return new long[0];
            }

            Log.InfoFormat("Found setting of {0}: {1} for language id of {2}", setting, productListIdsSetting, allowedDefaultRecommendationLanguageId);

            // Parse ints

            try
            {
                var productListIds = productListIdsSetting
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(long.Parse)
                    .ToArray();

                Log.InfoFormat("Parsed product list IDs: {0} for {1}", string.Join(", ", productListIds), allowedDefaultRecommendationLanguageId);

                return productListIds;
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Failed to parse product list IDs ({0})", productListIdsSetting), ex);
            }
        }

        private static string GetProductListSetting(int allowedDefaultRecommendationLanguageId)
        {
            switch (allowedDefaultRecommendationLanguageId)
            {
                case 4105:
                    return DefaultRecosProductListsSetting;
                case 3084:
                    return DefaultFrenchRecosProductListsSetting;
                default:
                    throw new Exception(string.Format("No product list setting was found for language id of {0}", allowedDefaultRecommendationLanguageId));
            }
        }

        private static CMSLanguage GetCmsLanguage(int allowedDefaultRecommendationLanguageId)
        {
            switch (allowedDefaultRecommendationLanguageId)
            {
                case 4105:
                    return CMSLanguage.English;
                case 3084:
                    return CMSLanguage.French;
                default:
                    throw new Exception(string.Format("No CMS language found for language id of {0}", allowedDefaultRecommendationLanguageId));
                
            }
        }

        /// <summary>
        /// Query the default recos table for the list of product list IDs currently existing in the table.
        /// </summary>
        private long[] GetExistingProductListIds(int allowedDefaultRecommendationLanguageId)
        {
            using (var connection = GetRecosDbConnection())
            {
                var data = new { LanguageID = allowedDefaultRecommendationLanguageId };
                return connection.Query<long>("usp_ListAllCmsProductLists", commandType: CommandType.StoredProcedure, param: data).ToArray();
            }
        }

        private string[] GetExistingProductListSkus(long id, int allowedDefaultRecommendationLanguageId)
        {
            using (var connection = GetRecosDbConnection())
            {
                var data = new { CmsProductListId = id, LanguageID = allowedDefaultRecommendationLanguageId };
                var recos = connection.Query("usp_GetDefaultRecosFromCmsProductListsData", data, commandType: CommandType.StoredProcedure);
                return recos
                    .OrderBy(r => r.Order)
                    .Select(r => r.SKU)
                    .Cast<string>()
                    .ToArray();
            }
        }

        /// <summary>
        /// Update the default recos table for the specified product list
        /// (i.e. replace all existing recommendations that came from this product list with the latest data).
        /// </summary>
        private void UpdateRecosFromProductList(long id, int allowedDefaultRecommendationLanguageId)
        {
            try
            {
                // Load the SKUs from the specified product list
                var skus = LoadCmsProductList(id, allowedDefaultRecommendationLanguageId);

                // Get the SKUs already in the default recos table for this product list
                var existingSkus = GetExistingProductListSkus(id, allowedDefaultRecommendationLanguageId);

                // Check if the two lists are identical
                if (skus.SequenceEqual(existingSkus))
                {
                    _productListsUnchanged++;

                    Log.InfoFormat("Product list {0} for language {1} is unchanged", id, allowedDefaultRecommendationLanguageId);
                }
                else
                {
                    var update = false;

                    if (existingSkus.Any())
                    {
                        update = true;

                        // Delete all old recos for this product list
                        DeleteRecosForProductList(id, allowedDefaultRecommendationLanguageId, true);
                    }

                    // Now add the SKUs from the product list as new recos
                    for (int i = 0; i < skus.Length; i++)
                    {
                        AddReco(id, skus[i], i, allowedDefaultRecommendationLanguageId);
                    }

                    if (update)
                    {
                        _productListsUpdated++;

                        Log.InfoFormat("Product list {0} for language {1} updated", id, allowedDefaultRecommendationLanguageId);
                    }
                    else
                    {
                        _productListsAdded++;

                        Log.InfoFormat("Product list {0} for language {1} added", id, allowedDefaultRecommendationLanguageId);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorFormat(ex, "Failed to update recos for product list ID {0} for language {1}", id, allowedDefaultRecommendationLanguageId);
            }
        }

        /// <summary>
        /// Load a product list from the CMS and get the list of SKUs (in order) of the products within.
        /// Products that don't exist are filtered out.
        /// </summary>
        private string[] LoadCmsProductList(long id, int allowedDefaultRecommendationLanguageId)
        {
            string[] skus = new string[0];

            Log.InfoFormat("Loading product list {0}", id);

            try
            {
                CMSContent content;

                // Load the content from the CMS through CSI
                using (var merchandisingService = _merchandisingServiceFactory.Create())
                {
                    content = merchandisingService.ServiceClient.GetGenericContentById((int)id, GetCmsLanguage(allowedDefaultRecommendationLanguageId), false, false);
                }

                if (content != null)
                {
                    // Parse content XML to read the product SKUs
                    var xml = content.Content;
                    if (xml != null)
                    {
                        // Get the products
                        var skuPidCatalogs = ParseCmsProductList(xml);

                        // Filter out products that don't exist
                        skuPidCatalogs = VerifyProductsExist(skuPidCatalogs);

                        if (skuPidCatalogs.Length > 0)
                        {
                            skus = skuPidCatalogs.Select(spc => spc.Sku).ToArray();
                        }
                        else
                        {
                            Log.WarnFormat("Product list {0} has no products", id);
                        }
                    }
                    else
                    {
                        Log.WarnFormat("The content for product list {0} is null or empty (the product list may be expired)", id);
                    }
                }
                else
                {
                    Log.WarnFormat("Product list {0} could not be loaded (it might not exist, or if it does, it might not be published)", id);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Failed to load product list {0}", id), ex);
            }

            return skus;
        }

        /// <summary>
        /// Parses the XML from a CMS Product List and returns a list of SkuPidCatalog objects.
        /// </summary>
        private SkuPidCatalog[] ParseCmsProductList(string xml)
        {
            var skuPidCatalogs = new List<SkuPidCatalog>();

            // Ensure content has the root element.
            if (!xml.StartsWith("<root"))
            {
                xml = string.Format("<root>{0}</root>", xml);
            }

            var root = XElement.Load(new StringReader(xml));
            var products = root.Elements("ProductList").Elements("Product");
            
            foreach (var product in products)
            {
                var sku = product.Element("ProductId").Value;
                var catalog = product.Element("Catalog").Value;

                if (!string.IsNullOrWhiteSpace(sku) && !string.IsNullOrWhiteSpace(catalog))
                {
                    skuPidCatalogs.Add(new SkuPidCatalog
                    {
                        Sku = sku,
                        Pid = ISBNUtils.ConvertUPCToPID(sku),
                        Catalog = catalog
                    });
                }
            }

            return skuPidCatalogs.ToArray();
        }

        /// <summary>
        /// Takes a list of products (as a SkuPidCatalog array) and filters out products that don't exist.
        /// </summary>
        private SkuPidCatalog[] VerifyProductsExist(SkuPidCatalog[] skuPidCatalogs)
        {
            // Handle empty list efficiently
            if (skuPidCatalogs.Length == 0)
            {
                return skuPidCatalogs;
            }

            Product[] products = null;

            // Load the products from CSI
            using (var catalogueService = _catalogueServiceFactory.Create())
            {
                var request = new ProductDetailRequest()
                {
                    GetRatingsData = false,
                    GetExtendedInfo = false,
                    GetExternalResources = false,
                    GetCdTracks = false,
                    GetStorePrices = false,
                    UpdateProductWithSales = false,
                    GetReviews = false,
                    GetProductAvailability = false,
                    SearchAllCatalogues = true,
                    Language = Language.English,
                    GetEndecaData = false,
                    OfferDetailLevel = SpecialOfferDetailLevel.None,
                    PromotionsTestEffectiveDate = null,
                    IsPreview = false
                };

                var pidCatalogs = skuPidCatalogs
                    .Select(spc => string.Format("{0}:{1}", spc.Pid, spc.Catalog))
                    .ToArray();

                products = catalogueService.ServiceClient.GetProducts(pidCatalogs, request, Channel.Online);
            }

            // Create list of PIDs found
            string[] pidsFound = new string[0];
            if (products != null)
            {
                pidsFound = products
                    .Where(p => p != null && !string.IsNullOrWhiteSpace(p.PID) && !string.IsNullOrWhiteSpace(p.Title))
                    .Select(product => product.PID)
                    .ToArray();
            }

            // Collect results
            var verifiedSkuPidCatalogs = new List<SkuPidCatalog>();
            foreach (var skuPidCatalog in skuPidCatalogs)
            {
                if (pidsFound.Contains(skuPidCatalog.Pid))
                {
                    verifiedSkuPidCatalogs.Add(skuPidCatalog);
                }
            }

            // Log SKUs that were removed
            var removedSkuPidCatalogs = skuPidCatalogs.Except(verifiedSkuPidCatalogs);
            if (removedSkuPidCatalogs.Any())
            {
                Log.WarnFormat("The following SKUs were removed because the products don't exist: {0}", string.Join(", ", removedSkuPidCatalogs.Select(spc => spc.Sku)));
            }
            
            return verifiedSkuPidCatalogs.ToArray();
        }

        /// <summary>
        /// Delete all existing recommendations that came from the specified CMS product list.
        /// </summary>
        private void DeleteRecosForProductList(long id, int allowedDefaultRecommendationLanguageId, bool throwException)
        {
            try
            {
                using (var connection = GetRecosDbConnection())
                {
                    var data = new { CmsProductListId = id, LanguageID = allowedDefaultRecommendationLanguageId };
                    connection.Execute("usp_DeleteDefaultRecosFromCmsProductLists", data, commandType: CommandType.StoredProcedure);
                }
            }
            catch (Exception ex)
            {
                Log.ErrorFormat(ex, "Failed to delete existing recos for product list {0} for languageId of {1}", id, allowedDefaultRecommendationLanguageId);

                if (throwException)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Add a single recommendation for the specified product list, SKU, and order.
        /// </summary>
        private void AddReco(long productListId, string sku, int order, int allowedDefaultRecommendationLanguageId)
        {
            using (var connection = GetRecosDbConnection())
            {
                var data = new
                {
                    CmsProductListId = productListId,
                    SKU = sku,
                    Order = order,
                    LanguageID = allowedDefaultRecommendationLanguageId
                };
                connection.Execute("usp_AddDefaultRecosFromCmsProductLists", data, commandType: CommandType.StoredProcedure);
            }

            Log.DebugFormat("Added default reco {0} from product list {1} for language {2}", sku, productListId, allowedDefaultRecommendationLanguageId);
        }

        private void AddOrdering(long newProductListId, int order, int allowedDefaultRecommendationLanguageId)
        {
            using (var connection = GetRecosDbConnection())
            {
                var data = new
                {
                    CmsProductListId = newProductListId,
                    Rank = order,
                    LanguageID = allowedDefaultRecommendationLanguageId
                };
                connection.Execute("usp_AddDefaultRecosFromCmsProductListsRanks", data, commandType: CommandType.StoredProcedure);
            }
        }

        private void DeleteProductListFromOrderTable(int allowedDefaultRecommendationLanguageId)
        {
            var data = new
            {
                LanguageID = allowedDefaultRecommendationLanguageId
            };

            using (var connection = GetRecosDbConnection())
            {
                connection.Execute("usp_DeleteDefaultRecosFromCmsProductListsRanks", data, commandType: CommandType.StoredProcedure);
            }
        }

        private SqlConnection GetRecosDbConnection()
        {
            return DbUtils.GetDbConnection("RecosDB");
        }

        //private class CmsProductListOrdering
        //{
        //    public long CmsProductListId { get; set; }
        //    public int Rank { get; set; }
        //}

        private class SkuPidCatalog
        {
            public string Sku { get; set; }
            public string Pid { get; set; }
            public string Catalog { get; set; }
        }
    }
}
