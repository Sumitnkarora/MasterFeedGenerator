using Castle.Core.Logging;
using FeedGenerators.Core.Services.Abstract;
using FeedGenerators.Core.Utils;
using Indigo.Feeds.Models.Abstract;
using Indigo.Feeds.Types;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Text;
using Indigo.Feeds.Utils;
using FeedGenerators.Core.Enums;

namespace RRFeedGenerator.Execution.FileFeedWriter.Helpers
{
    class Utility
    {
        private readonly FeedWriterContext _context;
        private readonly IDataReader _reader;

        private static readonly string ImgBaseUrl = ParameterUtils.GetParameter<string>("DynamicImagesUrl");
        private static readonly string BaseUrl = ParameterUtils.GetParameter<string>("PhoenixOnlineBaseUrl");
        private static readonly string ImagePathSuffix = ParameterUtils.GetParameter<string>("ImagePathSuffix");

        public Utility(IDataReader reader, FeedWriterContext context)
        {
            _context = context;
            _reader = reader;
        }

        private StringDictionary AttributesDictionary
        {
            get { return _context.AttributesDictionary; }
        }

        private ILogger Log
        {
            get { return _context.Log; }
        }

        private IExecutionLogLogger ExecutionLogLogger
        {
            get { return _context.ExecutionLogLogger; }
        }

        public string GetEntryImageLink(string sku)
        {
            Log.Debug("Entering Uitlity.GetEntryImageLink");
            
            var imgSection = FeedUtils.GetImageFileSectionName(_reader);

            decimal salePrice = GetSalePrice();
            decimal listPrice = GetListPrice();

            var imageUrl = string.Format("{0}/{1}/{2}.jpg{3}", ImgBaseUrl, imgSection, sku, ImagePathSuffix);

            if (listPrice > 0 && (listPrice - salePrice) > 0)
            {
                var hasDollarDiscount = GetAttributeValue("hasDollarDiscount");

                if (hasDollarDiscount != DBNull.Value && (bool)hasDollarDiscount)
                {
                    imageUrl += string.Format("?sale={0:#}&saleType=d", listPrice - salePrice);
                }
                else
                {
                    imageUrl += string.Format("?sale={0:###}", Decimal.Multiply((listPrice - salePrice) / listPrice, 100M));
                }
            }

            Log.Debug("Exiting Uitlity.GetEntryImageLink");

            return imageUrl;
        }

        public string GetFeedEntryLinkValue(FeedGenerators.Core.Enums.Language language)
        {
            var catalog = FeedUtils.GetCatalog(AttributesDictionary, language);
            var sku = GetAttributeValue("linkSku").ToString();

            return catalog == "ebooks" ? _reader["KoboItemPageURL"].ToString() : FeedUtils.GetProductUrl(BaseUrl, catalog, sku, language, false);
        }

        public string GetSalePriceString()
        {
            var salePrice = GetSalePrice();
            var listPrice = GetListPrice();

            var result = salePrice >= listPrice ? string.Empty : salePrice.ToString("0.00");
            return result;
        }

        private decimal GetSalePrice()
        {
            var result = (decimal)GetAttributeValue("adjustedPrice");
            return result;
        }

        private decimal GetListPrice()
        {
            var result = (decimal)GetAttributeValue("price");
            return result;            
        }
        
        public string GetListPriceString()
        {
            string result =
                ((decimal)GetAttributeValue("price")).ToString("0.00");

            return result;
        }

        public object GetAttributeValue(string attributeKey)
        {
            Log.DebugFormat("Attribute Key: {0}", attributeKey);

            if (!AttributesDictionary.ContainsKey(attributeKey))
            {
                Log.Debug("AttributesDictionary does not contain key: " + attributeKey);
                return string.Empty;
            }

            Log.DebugFormat("AttributeValue: {0}", AttributesDictionary[attributeKey]);
            
            return _reader[AttributesDictionary[attributeKey]];
        }

        public static string LineToString(StringBuilder stringBuilder)
        {
            if (stringBuilder.Length == 0)
                return string.Empty;

            return stringBuilder.Remove(stringBuilder.Length - 1, 1).ToString();
        }

        public int? GetDefaultIndigoCategoryId()
        {
            Log.DebugFormat("Enter Utility.GetDefaultIndigoCategoryId. catalog: {0}", _context.LineItem.Catalog);

            var breadCrumbCategory = GetDefaultIndigoBreadcrumb();

            if (breadCrumbCategory == null)
            {
                return null;
            }

            Log.Debug("Exiting Utility.GetDefaultIndigoCategoryId");

            return breadCrumbCategory.IndigoCategoryId;
        }

        private const string GenMerchCatalogType = "generalMerchandise";
        private static readonly Dictionary<string, string> FeedSectionMap = new Dictionary<string, string>
        {
            { "generalMerchandiseGiftCard", GenMerchCatalogType }
        };

        private string GetMappedCatalog(string catalog)
        {
            string mappedCatalog;
            if (FeedSectionMap.TryGetValue(catalog, out mappedCatalog))
            {
                catalog = mappedCatalog;
            }

            return catalog;
        }

        private IIndigoBreadcrumbCategory GetDefaultIndigoBreadcrumb()
        {
            var catalog = GetMappedCatalog(_context.LineItem.Catalog);

            try
            {
                var breadCrumbCategory = FeedUtils.GetFeedGeneratorIndigoCategory(_context.FeedGeneratorIndigoCategoryService, _reader,
                    AttributesDictionary, catalog, Log);
                
                return breadCrumbCategory;
            }
            catch (Exception ex)
            {
                var message = string.Format(
                    "GetDefaultIndigoBreadcrumb failed. PID: {0}, Catalog: {1}, Language: {2}",
                    GetAttributeValue("gId"), catalog, _context.Language);

                if (Constants.ExecutionLogBreadCrumbErrors)
                    ExecutionLogLogger.AddCustomMessage(message);

                Log.Info(message, ex);

                return null;
            }
            
        }

        public string GetMainContributor()
        {
            var contributorsArray = GetContributorsArray();

            var firstContributor = contributorsArray.Length != 0 ? contributorsArray[0] : string.Empty;
            return firstContributor;
        }

        public string[] GetContributorsArray()
        {
            var contributors = ((string)(GetAttributeValue("contributors").DbNullToNull() ?? string.Empty)).Split(new[] { '^' }, StringSplitOptions.RemoveEmptyEntries);
            
            // SK - Commenting out this fix as per instructions from Cagman.
            //var contributorsList = contributors.ToList();

            //// This is to handle when there's a circumflex ("^") in the name
            //// It won't handle all cases but will probably catch most of them.
            //for (var i = 1; i < contributorsList.Count; i++)
            //    if (!contributors[i].Contains(' '))
            //    {
            //        contributorsList[i - 1] += "^" + contributorsList[i];
            //        contributorsList.RemoveAt(i);
            //    }

            //return contributorsList.Select(contributor => contributor.Replace("\n", string.Empty)).ToArray();

            return contributors;
        }

        public static object EmptyToNull(object value)
        {
            if (string.Empty.Equals(value))
                return null;

            return value;
        }

        public static object DbNullToNull(object value)
        {
            if (DBNull.Value.Equals(value))
                return null;

            return value;
        }

        public string GetLocalizedKey(Func<IDictionary<FeedGenerators.Core.Enums.Language, string>, string> languageWriter,
            string english, string french)
        {
            var localizedKey = languageWriter(new Dictionary<FeedGenerators.Core.Enums.Language, string>
            {
                {FeedGenerators.Core.Enums.Language.English, english},
                {FeedGenerators.Core.Enums.Language.French, french}
            });

            return localizedKey;
        }

        public object GetLocalizedResult(FeedGenerators.Core.Enums.Language language, string localizedKey, string english)
        {
            var result = GetAttributeValue(localizedKey);

            if (language != FeedGenerators.Core.Enums.Language.English &&
                (result == DBNull.Value || result == null ||
                 result is string && string.IsNullOrWhiteSpace((string)result)))
            {
                Log.DebugFormat("Result is null/empty for non-English language: {0}", language);
                result = GetAttributeValue(english);
            }

            return result;
        }
    }
}
