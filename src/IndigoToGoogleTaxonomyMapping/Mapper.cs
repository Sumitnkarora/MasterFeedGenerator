using Castle.Core.Logging;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Utils;
using IndigoToGoogleTaxonomyMapping.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Transactions;

namespace IndigoToGoogleTaxonomyMapping
{
    public class Mapper : IMapper
    {
        private readonly IGoogleCategoryService _googleCategoryService;
        private readonly IndigoCategoryServiceProxy _indigoCategoryServiceProxy;

        private static readonly string InputFileFolderPath = ParameterUtils.GetParameter<string>("InputFileFolderPath");
        private static readonly string InputFileName = ParameterUtils.GetParameter<string>("InputFileName");

        public ILogger Log { get; set; }

        public Mapper(IGoogleCategoryService googleCategoryService, IIndigoCategoryService indigoCategoryService)
        {
            _googleCategoryService = googleCategoryService;
            _indigoCategoryServiceProxy = new IndigoCategoryServiceProxy(indigoCategoryService);
        }

        public void Execute()
        {
            var startTime = DateTime.Now;
            Log.Info("Execution started.");
            // Start

            try
            {
                // Get a dictionary of Indigo categories keyed on the EndecaBreadcrumbId from the database.
                Dictionary<string, IndigoCategoryWrapper> indigoCategoryDictionary =
                    _indigoCategoryServiceProxy.GetAllIndigoCategories()
                    .Select(indigoCategory => new IndigoCategoryWrapper(indigoCategory))
                    .ToDictionary(p => p.IndigoCategory.EndecaBreadcrumbId.Trim());

                // Get a dictionary of Google categories keyed on the Breadcrumb path from the database.
                Dictionary<string, GoogleCategoryWrapper> googleCategoryDictionary = 
                    _googleCategoryService.GetAllGoogleCategories()
                    .Select(googleCategory => new GoogleCategoryWrapper(googleCategory))
                    .ToDictionary(p => p.GoogleCategory.BreadcrumbPath.Trim());

                MapCategoryIds(indigoCategoryDictionary, googleCategoryDictionary);
                UpdateDatabase(indigoCategoryDictionary);
                Statistics.Output(indigoCategoryDictionary, googleCategoryDictionary, Log);
            }
            catch (Exception ex)
            {
                Log.Error("An error occurred during execution. Terminating...", ex);
            }
            
            // End
            var elapsedTime = DateTime.Now - startTime;
            Log.InfoFormat("Execution completed. Elapsed time: {0}", elapsedTime.ToString(@"dd\.hh\:mm\:ss"));
        }

        private static void MapCategoryIds(
            Dictionary<string, IndigoCategoryWrapper> indigoCategoryDictionary,
            Dictionary<string, GoogleCategoryWrapper> googleCategoryDictionary)
        {
            using (var reader = File.OpenText(GetInputFilePath()))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // Get values from the spreadsheet
                    string[] elements = line.Split(new char[] { ',' }, StringSplitOptions.None);

                    string endecaBreadcrumbId = GetCsvValue(elements[0]);
                    string googleCategoryBreadcrumbPath = GetCsvValue(elements[2]);

                    // Search for the spreadsheet Indigo Endeca category Id in the database.
                    IndigoCategoryWrapper indigoCategoryWrapper;
                    bool isEndecaBreadcrumbIdInDatabase = indigoCategoryDictionary.TryGetValue(endecaBreadcrumbId, out indigoCategoryWrapper);

                    bool keepGoing = true;

                    if (!isEndecaBreadcrumbIdInDatabase)
                    {
                        Statistics.SpreadSheetEndecaIdsThatAreNotInDatabase.Add(endecaBreadcrumbId);
                        keepGoing = false;
                    }
                    else
                        indigoCategoryWrapper.IsFoundInDatabase = true;

                    if (string.IsNullOrEmpty(googleCategoryBreadcrumbPath))
                    {
                        Statistics.SpreadSheetEndecaIdsForWhichThereIsNoSpreadSheetGoogleMapping.Add(endecaBreadcrumbId);
                        keepGoing = false;
                    }

                    if (!keepGoing)
                        continue;

                    // Indigo Endeca category Id exists:

                    // Search for spreadsheet Google category breadcrumb path in the database.
                    GoogleCategoryWrapper googleCategoryWrapper;
                    bool isGoogleBreadcrumbPathInDatabase = googleCategoryDictionary.TryGetValue(googleCategoryBreadcrumbPath, out googleCategoryWrapper);

                    if (!isGoogleBreadcrumbPathInDatabase)
                    {
                        Statistics.SpreadSheetGoogleBreadcrumbPathNotInDatabase.Add(googleCategoryBreadcrumbPath);
                        continue;
                    }

                    // Google category breadcrumb exists:

                    // Perform mapping.                    
                    bool isSameGoogleCategoryId = indigoCategoryWrapper.IndigoCategory.GoogleCategoryId == googleCategoryWrapper.GoogleCategory.GoogleCategoryId;
                    indigoCategoryWrapper.IndigoCategory.GoogleCategoryId = googleCategoryWrapper.GoogleCategory.GoogleCategoryId;
                    indigoCategoryWrapper.IsModified = !isSameGoogleCategoryId;
                    googleCategoryWrapper.Status = isSameGoogleCategoryId ? GoogleCategoryWrapper.StatusEnum.Same : GoogleCategoryWrapper.StatusEnum.Different;

                    indigoCategoryWrapper.IndigoCategory.IsModified |= indigoCategoryWrapper.IsModified;
                }
            }
        }

        /// <summary>
        /// To avoid parsing problems the comma (",") character in the input excel file
        /// from the the csv was made was replaced by a replacement value. This method
        /// changes the column value back to have commas in this case.
        /// </summary>
        /// <param name="value">Esacped CSV value</param>
        /// <returns>Reconstituted CSV value</returns>
        private static string GetCsvValue(string value)
        {
            string replacementCharacter = System.Configuration.ConfigurationManager.AppSettings["CSV_CommaReplacement"];
            return value.Trim().Replace(replacementCharacter, ",");
        }

        private void UpdateDatabase(Dictionary<string, IndigoCategoryWrapper> indigoCategoryDictionary)
        {
            List<IIndigoCategory> indigoCategories = indigoCategoryDictionary.Where(dictionaryItem => dictionaryItem.Value.IsModified)
                                                                .Select(dictionaryItem => dictionaryItem.Value.IndigoCategory).ToList();

            var transactionOption = new TransactionOptions(); transactionOption.IsolationLevel = IsolationLevel.ReadCommitted; using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOption))
            {
                foreach (var indigoCategory in indigoCategories)
                {
                    indigoCategory.IsModified = false;
                    _indigoCategoryServiceProxy.Update(indigoCategory);
                }

                scope.Complete();
            }
        }

        private static string GetInputFilePath()
        {
            return Path.Combine(InputFileFolderPath, InputFileName);
        }
    }
}
