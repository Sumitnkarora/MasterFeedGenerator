using Castle.Core.Logging;
using GoogleApiPlaFeedGenerator.Helpers;
using Indigo.Feeds.Entities.Concrete;
using Indigo.Feeds.GoogleShoppingApiIntegration.Services.Interfaces;
using Indigo.Feeds.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace GoogleApiPlaFeedGenerator.Execution
{
    public class GoogleRequestProcessor : IGoogleRequestProcessor
    {
        private static readonly int NumberOfTrialsPerApiCall = ParameterUtils.GetParameter<int>("NumberOfTrialsPerApiCall");

        private readonly ILogger _logger;
        private readonly IGooglePlaDataService _googlePlaDataService;

        private volatile ConcurrentBag<string> _failedProductIdentifiers = new ConcurrentBag<string>();

        public GoogleRequestProcessor(IGooglePlaDataService googlePlaDataService, ILogger logger)
        {
            _logger = logger;
            _googlePlaDataService = googlePlaDataService;
        }

        public bool SendDeletionBatch(IList<string> productIdentifiers)
        {
            _logger.Debug("Starting to send batch deletion to Google.");
            var request = GoogleShoppingApiIntegrationHelper.GetGooglePlaDeleteProductsOperationRequest(productIdentifiers);
            var trialsCount = 0;
            var isSuccess = false;
            while (trialsCount < NumberOfTrialsPerApiCall)
            {
                var response = _googlePlaDataService.DeleteProducts(request);
                if (response.Status == Indigo.Feeds.GoogleShoppingApiIntegration.Types.GooglePlaDataOperationStatus.Failure)
                {
                    _logger.DebugFormat("Deletion API call to Google failed, tries left is {0}", NumberOfTrialsPerApiCall - 1 - trialsCount);
                    trialsCount++;
                    continue;
                }

                isSuccess = true;
                _logger.DebugFormat("Deletion API call to Google was successful. Number of successful and failed deletions are {0} and {1}", response.SuccessfulProductIdentifiers.Count(), response.UnsuccessfulProductIdentifiers.Count());
                break;
            }

            if (!isSuccess)
                _logger.ErrorFormat("Deletion of batch failed after all tries.");
            else
                _logger.Debug("Completed sending batch deletion to Google.");

            return isSuccess;
        }

        public bool SendUpdateBatch(IList<GooglePlaProductData> productDatas)
        {
            _logger.Debug("Starting to send batch update to Google.");
            var request = GoogleShoppingApiIntegrationHelper.GetGooglePlaUpdateProductsOperationRequest(productDatas);
            var trialsCount = 0;
            var isSuccess = false;
            var lastErrorMessage = string.Empty;
            var failedProducts = new List<GooglePlaProductData>();
            while (trialsCount < NumberOfTrialsPerApiCall)
            {
                var response = _googlePlaDataService.UpdateProducts(request);
                if (response.Status == Indigo.Feeds.GoogleShoppingApiIntegration.Types.GooglePlaDataOperationStatus.Failure)
                {
                    _logger.DebugFormat("Update API call to Google failed, tries left is {0}", NumberOfTrialsPerApiCall - 1 - trialsCount);
                    _logger.DebugFormat("Error message from Google is {0}", response.ErrorResponse);
                    lastErrorMessage = response.ErrorResponse;
                    trialsCount++;
                    continue;
                }

                // Check if there were any products that failed, if so, try sending another request to Google while setting title as description
                // as we have encoding issues with descriptions
                if (response.UnsuccessfulProductIdentifiers != null)
                {
                    foreach (var failedId in response.UnsuccessfulProductIdentifiers)
                    {
                        var productData = productDatas.Single(data => data.Identifier.Equals(failedId, StringComparison.OrdinalIgnoreCase));
                        productData.Description = productData.Title;
                        failedProducts.Add(productData);
                        _failedProductIdentifiers.Add(productData.Identifier);
                    }
                }

                isSuccess = true;
                _logger.DebugFormat("Update API call to Google was successful. Number of successful and failed updates are {0} and {1}", response.SuccessfulProductIdentifiers.Count(), response.UnsuccessfulProductIdentifiers.Count());
                break;
            }

            if (failedProducts.Count > 0)
            {
                var newRequest = GoogleShoppingApiIntegrationHelper.GetGooglePlaUpdateProductsOperationRequest(failedProducts);
                trialsCount = 0;
                while (trialsCount < NumberOfTrialsPerApiCall)
                {
                    var response = _googlePlaDataService.UpdateProducts(newRequest);
                    if (response.Status == Indigo.Feeds.GoogleShoppingApiIntegration.Types.GooglePlaDataOperationStatus.Failure)
                    {
                        _logger.DebugFormat("Update API call to Google failed, tries left is {0}", NumberOfTrialsPerApiCall - 1 - trialsCount);
                        trialsCount++;
                        continue;
                    }

                    _logger.DebugFormat("Update call for failed products was successful. Number of successful and failed updates are {0} and {1}", response.SuccessfulProductIdentifiers.Count(), response.UnsuccessfulProductIdentifiers.Count());
                    break;
                }
            }

            if (!isSuccess)
                _logger.ErrorFormat("Update of batch failed after all tries. Last error message from Google was {0}.", lastErrorMessage);
            else
                _logger.Debug("Completed sending batch update to Google.");

            return isSuccess;
        }

        public IList<string> GetIdentifiersWithErroneousDescriptions()
        {
            return _failedProductIdentifiers.ToList();
        }
    }
}
