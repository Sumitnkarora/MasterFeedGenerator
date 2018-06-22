using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.Core.Logging;

namespace IndigoToGoogleTaxonomyMapping.Model
{
    internal class Statistics
    {
        public static readonly List<string> SpreadSheetEndecaIdsThatAreNotInDatabase = new List<string>();
        public static readonly List<string> SpreadSheetGoogleBreadcrumbPathNotInDatabase = new List<string>();
        public static readonly List<string> SpreadSheetEndecaIdsForWhichThereIsNoSpreadSheetGoogleMapping = new List<string>();

        private static ILogger Log;
        
        public static void Output(
            Dictionary<string, IndigoCategoryWrapper> indigoCategoryDictionary,
            Dictionary<string, GoogleCategoryWrapper> googleCategoryDictionary,
            ILogger log)
        {
            Log = log;

            // Logic is such that if Indigo category is modified than it was found in the database.
            int countOfModifiedIndigoCategories = indigoCategoryDictionary.Count(entry => entry.Value.IsModified);

            int countOfNotModifiedExistingIndigoCategories = indigoCategoryDictionary.Count(indigoEntry => indigoEntry.Value.IsFoundInDatabase
                                                                                                && !indigoEntry.Value.IsModified);

            int countOfMappedGoogleCategories = googleCategoryDictionary.Count(googleEntry => 
                                                        googleEntry.Value.Status == GoogleCategoryWrapper.StatusEnum.Different);

            int countOfSameGoogleCategories = googleCategoryDictionary.Count(googleEntry => 
                                                        googleEntry.Value.Status == GoogleCategoryWrapper.StatusEnum.Same);

            Output("Modified Indigo Categories:\t{0}", countOfModifiedIndigoCategories);
            Output("Mapped Google Categories:\t{0}", countOfMappedGoogleCategories);
            Output("Google Categories same as Indigo Categories:\t{0}", countOfSameGoogleCategories);
            Output("Total Google Categories Processed:\t{0}", countOfMappedGoogleCategories + countOfSameGoogleCategories);
            Output("Indigo Endeca ID's not found in database:\t{0}", SpreadSheetEndecaIdsThatAreNotInDatabase.Count());
            Output("Google Breadcrumb paths not in database:\t{0}", SpreadSheetGoogleBreadcrumbPathNotInDatabase.Count());
            Output("Blank Google Breadcrumb spreadsheet entries:\t{0}", SpreadSheetEndecaIdsForWhichThereIsNoSpreadSheetGoogleMapping.Count());
        }

        private static void Output(string outputText, params object[] parameters)
        {
            string output = string.Format(outputText, parameters);

            Log.Info(output);
        }
    }
}
