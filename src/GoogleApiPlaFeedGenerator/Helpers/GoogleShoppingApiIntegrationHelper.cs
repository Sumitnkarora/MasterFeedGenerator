using Indigo.Feeds.Entities.Concrete;
using Indigo.Feeds.GoogleShoppingApiIntegration.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace GoogleApiPlaFeedGenerator.Helpers
{
    public static class GoogleShoppingApiIntegrationHelper
    {
        private static readonly ulong MerchantId = ulong.Parse(ConfigurationManager.AppSettings["GoogleShoppingApiIntegration.MerchantId"]);
        private static readonly string ApplicationName = ConfigurationManager.AppSettings["GoogleApiPlaFeedGenerator.ApplicationName"];
        private static readonly string ClientSecretsFileFolderPath = ConfigurationManager.AppSettings["GoogleApiPlaFeedGenerator.GoogleShoppingApiIntegration.ClientSecretsFileFolderPath"];
        private static readonly string ClientSecretsFileName = ConfigurationManager.AppSettings["GoogleApiPlaFeedGenerator.GoogleShoppingApiIntegration.ClientSecretsFileName"];

        private static string GetClientSecretsFilePath()
        {
            return Path.Combine(ClientSecretsFileFolderPath, ClientSecretsFileName);
        }

        public static GooglePlaUpdateProductsOperationRequest GetGooglePlaUpdateProductsOperationRequest(IList<GooglePlaProductData> productDatas)
        {
            if (productDatas == null || !productDatas.Any())
                throw new ArgumentException("Invalid/null product datas.", "productDatas");

            return new GooglePlaUpdateProductsOperationRequest(ApplicationName, GetClientSecretsFilePath(), MerchantId, productDatas);
        }

        public static GooglePlaDeleteProductsOperationRequest GetGooglePlaDeleteProductsOperationRequest(IList<string> productIdentifiers)
        {
            if (productIdentifiers == null || !productIdentifiers.Any())
                throw new ArgumentException("Invalid/null product identifiers.", "productIdentifiers");

            return new GooglePlaDeleteProductsOperationRequest(ApplicationName, GetClientSecretsFilePath(), MerchantId, productIdentifiers);
        }
    }
}
