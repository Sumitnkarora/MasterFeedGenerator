using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Xml.Linq;
using Indigo.Feeds.Generator.Core.Enums;
using Indigo.Feeds.Generator.Core.Models;
using Indigo.Feeds.Generator.Core.Services;
using Sterling.Feed.Export.RetailCategorization.Models;
using System.Linq;
using System.Configuration;
using Indigo.Feeds.Utils;

namespace Sterling.Feed.Export.RetailCategorization.Services
{
    public class RetailCategorizationService : IDataService
    {
        private static readonly string RootTag = "CategoryList";
        private static readonly string StatusCode = "3000";
        private static readonly string OrganizationCode = "Indigo_CA";
        private static readonly string CategoryDomain = "IndigoInStoreCategory";
        private static readonly string FrenchLanguage = "fr";
        private static readonly string Country = "CA";
        private static readonly bool IncludeMerchantCategoryInLayoutModule = ParameterUtils.GetParameter<bool>("IncludeMerchantCategoryInLayoutModule");


        public XElement ConvertToXml(ExportData data)
        {
            var taxonomyData = (TaxonomyData)data;
            var descriptionEn = TrimDescription(taxonomyData.Description_En);
            var descriptionFr = taxonomyData.Description_Fr != null ? TrimDescription(taxonomyData.Description_Fr) : null;
            var overrideDescFr = Boolean.TryParse(ConfigurationManager.AppSettings["overrideNullFrenchDescriptionsWithEnglishValues"], out bool b) && b;

            return new XElement("Category",
                         new XAttribute("Status", StatusCode),
                         new XAttribute("ShortDescription", descriptionEn),
                         new XAttribute("OrganizationCode", OrganizationCode),
                         new XAttribute("CategoryPath", GetPathString(taxonomyData)),
                         IncludeMerchantCategoryInLayoutModule ? new XAttribute("CategoryID", taxonomyData.TaxonomyId) : new XAttribute("CategoryID", taxonomyData.TaxonomyId.Split('-').Last()),
                         new XAttribute("CategoryDomain", CategoryDomain),
                         descriptionFr != null || overrideDescFr ? ProcessNullFrenchDescription(descriptionFr ?? descriptionEn) : null
                     );
        }

        private XElement ProcessNullFrenchDescription(string description)
        {
            return new XElement("CategoryLocaleList",
                new XAttribute("Reset", "Y"),
                    new XElement("CategoryLocale",
                    new XAttribute("ShortDescription",description),
                    new XAttribute("Language", FrenchLanguage),
                    new XAttribute("Country", Country)
                ));
        }

        private string TrimDescription(string description)
        {
            var desc = "";

            if (!string.IsNullOrEmpty(description))
            {
                desc = description.TrimEnd('/');
                desc = desc.Substring(desc.LastIndexOf('/') + 1);
            }

            return desc;
        }

        public DataResult GetData(StringDictionary attributeDictionary, IDataReader reader, string catalog, RunType runType)
        {
            var taxonomyData = new TaxonomyData
            {
                TaxonomyId = reader["TaxonomyId"].ToString(),
                IsGeneralMerchandise = reader["TaxonomyId"].ToString().StartsWith("P", StringComparison.OrdinalIgnoreCase),
                Description_En = reader["Description_En"] == DBNull.Value ? null : reader["Description_En"].ToString(),
                Description_Fr = reader["Description_Fr"] == DBNull.Value ? null : reader["Description_Fr"].ToString()
            };

            return new DataResult
            {
                ExportData = taxonomyData
            };
        }

        public Type GetDataType()
        {
            return typeof(TaxonomyData);
        }

        public IList<DataResult> GetDeletedData(DateTime fromTime, DateTime? toTime)
        {
            throw new NotImplementedException();
        }

        public string GetXmlRoot(RunType runType)
        {
            if (runType == RunType.Full)
            {
                return RootTag;
            }
            return null;
        }

        public ExportData MergeData(ExportData previousRecord, ExportData data)
        {
            throw new NotImplementedException();
        }

        private static string GetPathString(TaxonomyData data)
        {
            // TaxonomyId (note, for items which are MC + LM, split the LM on the - from the MC
            // /IndigoInStoreCategory/(Specific L1)/MC/LM
            var innerPath = data.IsGeneralMerchandise ?
                "/L1-GeneralMerchandise/" + data.TaxonomyId.Replace('-', '/')
                : "/L1-Books/" + data.TaxonomyId.Replace('-', '/');

            return "/IndigoInStoreCategory" + innerPath;
        }
    }
}
