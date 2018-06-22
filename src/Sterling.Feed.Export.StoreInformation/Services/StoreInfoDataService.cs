using Indigo.Feeds.Generator.Core.Enums;
using Indigo.Feeds.Generator.Core.Extensions;
using Indigo.Feeds.Generator.Core.Models;
using Indigo.Feeds.Generator.Core.Services;
using Sterling.Feed.Export.StoreInformation.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sterling.Feed.Export.StoreInformation.Services
{
    public class StoreInfoDataService : IDataService
    {
        private static readonly string RootTag = "StoreList";
        private static readonly string OrganizationTag = "Organization";
        private static readonly string CorporatePersonInfoTag = "CorporatePersonInfo";
        private static readonly string NodeTag = "Node";
        private static readonly string Quebec = "Quebec";
        private static readonly string EnglishCanadaLang = "en_CA";
        private static readonly string FrenchCanadaLang = "fr_CA";
        private static readonly string Store_PickUpOnly = "Store_PickUpOnly";
        private static readonly string Store_NoFulfillment = "Store_NoFulfillment";
        private static readonly string Node_Type = "Store";
        private Dictionary<string, string> ProvinceCodes = new Dictionary<string, string>()
        {
            {"Alberta", "AB"},
            {"British Columbia",    "BC"},
            {"Colombie-Britannique",    "BC"},
            {"Manitoba",    "MB"},
            {"New Brunswick",   "NB"},
            {"Nouveau-Brunswick",   "NB"},
            {"Newfoundland and Labrador",   "NL"},
            {"Terre-Neuve", "NL"},
            {"Nouvelle-Écosse", "NS"},
            {"Nova Scotia", "NS"},
            {"Northwest Territories",   "NT"},
            {"Territoires du Nord-Ouest",   "NT"},
            {"Nunavut", "NU"},
            {"Ontario", "ON"},
            { "Île-du-Prince-Édouard",   "PE"},
            {"Prince Edward Island",    "PE"},
            {"Quebec",  "QC"},
            {"Québec",  "QC"},
            {"Saskatchewan",    "SK"},
            {"Yukon",   "YT"}
        };
        public XElement ConvertToXml(ExportData data)
        {
            Organization organizationData = (Organization)data;
            string province = string.Empty;
            if (!ProvinceCodes.TryGetValue(organizationData.CorporatePersonInfo.State, out province))
            {
                province = organizationData.CorporatePersonInfo.State;
            }

            XElement organizationXElement = new XElement(OrganizationTag,
                                                  new XAttribute("CapacityOrganizationCode", organizationData.CapacityOrganizationCode),
                                                  new XAttribute("CatalogOrganizationCode", organizationData.CatalogOrganizationCode),
                                                  new XAttribute("InheritConfigFromEnterprise", organizationData.InheritConfigFromEnterprise),
                                                  new XAttribute("InventoryOrganizationCode", organizationData.InventoryOrganizationCode),
                                                  new XAttribute("InventoryPublished", organizationData.InventoryPublished),
                                                  new XAttribute("IsHubOrganization", organizationData.IsHubOrganization),
                                                  new XAttribute("IsSourcingKept", organizationData.IsSourcingKept),
                                                  new XAttribute("LocaleCode", organizationData.LocaleCode),
                                                  new XAttribute("Operation", organizationData.Operation),
                                                  new XAttribute("OrganizationCode", organizationData.OrganizationCode),
                                                  new XAttribute("OrganizationName", organizationData.OrganizationName),
                                                  new XAttribute("ParentOrganizationCode", organizationData.ParentOrganizationCode),
                                                  new XAttribute("PrimaryEnterpriseKey", organizationData.PrimaryEnterpriseKey));

            XElement corporatePersonInfoXElement = new XElement(CorporatePersonInfoTag,
                                                new XAttribute("AddressLine1", organizationData.CorporatePersonInfo.AddressLine1),
                                                new XAttribute("AddressLine2", organizationData.CorporatePersonInfo.AddressLine2),
                                                new XAttribute("City", organizationData.CorporatePersonInfo.City),
                                                new XAttribute("Company", organizationData.CorporatePersonInfo.Company),
                                                new XAttribute("Country", organizationData.CorporatePersonInfo.Country),
                                                new XAttribute("DayPhone", organizationData.CorporatePersonInfo.DayPhone),
                                                new XAttribute("EmailID", organizationData.CorporatePersonInfo.EmailID),
                                                new XAttribute("EMailID", organizationData.CorporatePersonInfo.EmailID),
                                                new XAttribute("Latitude", organizationData.CorporatePersonInfo.Latitude),
                                                new XAttribute("Longitude", organizationData.CorporatePersonInfo.Longitude),
                                                new XAttribute("State", province),
                                                new XAttribute("ZipCode", organizationData.CorporatePersonInfo.ZipCode));


            XElement nodeXElement = new XElement(NodeTag,
                                        new XAttribute("ActivateFlag", organizationData.Node.ActivateFlag),
                                        new XAttribute("AllowGiftWrap", organizationData.Node.AllowGiftWrap),
                                        new XAttribute("InventoryTracked", organizationData.Node.InventoryTracked),
                                        new XAttribute("IsFulfillmentNode", organizationData.Node.IsFulfillmentNode),
                                        new XAttribute("IsItemBasedAllocationAllowed", organizationData.Node.IsItemBasedAllocationAllowed),
                                        new XAttribute("Latitude", organizationData.Node.Latitude),
                                        new XAttribute("Longitude", organizationData.Node.Longitude),
                                        new XAttribute("NodeType", organizationData.Node.NodeType),
                                        new XAttribute("ShipNodeClass", organizationData.Node.ShipNodeClass),
                                        new XAttribute("ReceivingNode", organizationData.Node.ReceivingNode),
                                        new XAttribute("ReturnCenterFlag", organizationData.Node.ReturnCenterFlag),
                                        new XAttribute("ReturnsNode", organizationData.Node.ReturnsNode),
                                        new XAttribute("ShipNode", organizationData.Node.ShipNode),
                                        new XAttribute("ShipnodeType", organizationData.Node.ShipnodeType),
                                        new XAttribute("TimeDiff", organizationData.Node.TimeDiff));

            organizationXElement.Add(corporatePersonInfoXElement);
            organizationXElement.Add(nodeXElement);
            return organizationXElement;
        }

        public DataResult GetData(StringDictionary attributeDictionary, IDataReader reader, string catalog, RunType runType)
        {
            decimal latitiude = 0;
            decimal longitude = 0;
            string timeZoneAbbreviation = string.Empty;
            //if (!string.IsNullOrEmpty(dict["adjustedPrice"]) && decimal.TryParse(reader[dict["adjustedPrice"]].ToString(), out parsedSalePrice))
            //    adjustedPrice = parsedSalePrice;

            Organization organizationData = new Organization() { CorporatePersonInfo = new CorporatePersonInfo(), Node = new Node() };

            //Organization
            organizationData.OrganizationName = (reader["Store_type"] == DBNull.Value ? string.Empty : reader["Store_type"].ToString().Trim())
                                                + "-"
                                                + (reader["Store_name"] == DBNull.Value ? string.Empty : reader["Store_name"].ToString().Trim());

            organizationData.OrganizationCode = reader["Store_num"] == DBNull.Value ? string.Empty : Convert.ToInt32(reader["Store_num"]).ToString("0000").Trim();
            //Corporate Person Info
            organizationData.CorporatePersonInfo.AddressLine1 = reader["AddressLine1"] == DBNull.Value ? string.Empty : reader["AddressLine1"].ToString().Trim();
            organizationData.CorporatePersonInfo.AddressLine2 = reader["AddressLine2"] == DBNull.Value ? string.Empty : reader["AddressLine2"].ToString().Trim();
            organizationData.CorporatePersonInfo.City = reader["City"] == DBNull.Value ? string.Empty : reader["City"].ToString().Trim();
            organizationData.CorporatePersonInfo.Company = reader["Store_type"] == DBNull.Value ? string.Empty : reader["Store_type"].ToString().Trim();
            organizationData.CorporatePersonInfo.DayPhone = reader["Phone_num"] == DBNull.Value ? string.Empty : reader["Phone_num"].ToString().Trim();
            organizationData.CorporatePersonInfo.EmailID = reader["EmailAddress"] == DBNull.Value ? string.Empty : reader["EmailAddress"].ToString().Trim();
            if (!string.IsNullOrEmpty(reader["Latitude"].ToString()) && decimal.TryParse(reader["Latitude"].ToString(), out latitiude))
            {
                organizationData.CorporatePersonInfo.Latitude = latitiude;
            }
            if (!string.IsNullOrEmpty(reader["Longitude"].ToString()) && decimal.TryParse(reader["Longitude"].ToString(), out longitude))
            {
                organizationData.CorporatePersonInfo.Longitude = longitude;
            }
            organizationData.CorporatePersonInfo.State = reader["Province"] == DBNull.Value ? string.Empty : reader["Province"].ToString().Trim();
            organizationData.LocaleCode = organizationData.CorporatePersonInfo.State == Quebec ? FrenchCanadaLang : EnglishCanadaLang;
            organizationData.CorporatePersonInfo.ZipCode = reader["Postal_code"] == DBNull.Value ? string.Empty : reader["Postal_code"].ToString().Trim();

            //Node
            organizationData.Node.Latitude = organizationData.CorporatePersonInfo.Latitude;
            organizationData.Node.Longitude = organizationData.CorporatePersonInfo.Longitude;
            organizationData.Node.ShipNode = organizationData.OrganizationCode;
            organizationData.Node.NodeType = Node_Type;
            organizationData.Node.ShipNodeClass = Convert.ToBoolean(reader["BOPISEligible"]) ? Store_PickUpOnly : Store_NoFulfillment;
            if (reader["TimeZone"] != DBNull.Value)
            {
                string timeZone = reader["TimeZone"].ToString();
                organizationData.Node.TimeDiff = GetOffset(timeZone);
                if (reader["StandardZoneCode"] != DBNull.Value)
                {
                    organizationData.LocaleCode += "_" + reader["StandardZoneCode"];
                }
                //Use this code if daylight savings needs to be handled.
                //if(IsTimeZoneDayLightSaving(timeZone))
                //{
                //    if (reader["DayLightZoneCode"] != DBNull.Value)
                //    {
                //        organizationData.LocaleCode += "_" + reader["DayLightZoneCode"];
                //    }
                //}
                //else
                //{
                //    if (reader["StandardZoneCode"] != DBNull.Value)
                //    {
                //        organizationData.LocaleCode += "_" + reader["StandardZoneCode"];
                //    }
                //}
            }
            TrimFieldsLength(organizationData);

            return new DataResult() { ExportData = organizationData };
        }

        public Type GetDataType()
        {
            return typeof(Organization);
        }

        public IList<DataResult> GetDeletedData(DateTime fromTime, DateTime? toTime)
        {
            throw new NotImplementedException();
        }

        public string GetXmlRoot(RunType runType)
        {
            if (runType == RunType.Full || runType == RunType.OnDemand)
            {
                return RootTag;
            }
            return null;
        }

        public ExportData MergeData(ExportData previousRecord, ExportData data)
        {
            throw new NotImplementedException();
        }

        private bool IsTimeZoneDayLightSaving(string timeZoneId)
        {
            try
            {
                var storeTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                if (storeTimeZone.IsDaylightSavingTime(DateTime.Now))
                    return true;
                return false;
            }
            catch (TimeZoneNotFoundException ex)
            {
                throw new DataException(
                    "Time zone id " + timeZoneId + " was not found", ex);
            }
        }

        private decimal GetOffset(string timeZoneId)
        {
            TimeSpan difference;
            try
            {
                var est = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                var storeTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                var now = DateTimeOffset.UtcNow;
                TimeSpan torontonOffset = est.GetUtcOffset(now);
                TimeSpan storeOffset = storeTimeZone.GetUtcOffset(now);
                difference = torontonOffset - storeOffset;
            }
            catch (TimeZoneNotFoundException ex)
            {
                throw new DataException(
                    "Time zone id " + timeZoneId + " was not found", ex);
            }
            return (decimal)difference.TotalSeconds;
        }

        private void TrimFieldsLength(Organization organization)
        {
            //Organization
            organization.CapacityOrganizationCode = organization.CapacityOrganizationCode.LimitLength(24);
            organization.CatalogOrganizationCode = organization.CatalogOrganizationCode.LimitLength(24);
            organization.InventoryOrganizationCode = organization.InventoryOrganizationCode.LimitLength(24);
            organization.LocaleCode = organization.LocaleCode.LimitLength(20);
            organization.Operation = organization.Operation.LimitLength(40);
            organization.OrganizationCode = organization.OrganizationCode.LimitLength(24);
            organization.OrganizationName = organization.OrganizationName.LimitLength(100);
            organization.ParentOrganizationCode = organization.ParentOrganizationCode.LimitLength(24);
            organization.PrimaryEnterpriseKey = organization.PrimaryEnterpriseKey.LimitLength(24);

            //Corporate Person Info
            organization.CorporatePersonInfo.AddressLine1 = organization.CorporatePersonInfo.AddressLine1.LimitLength(70);
            organization.CorporatePersonInfo.AddressLine2 = organization.CorporatePersonInfo.AddressLine2.LimitLength(70);
            organization.CorporatePersonInfo.City = organization.CorporatePersonInfo.City.LimitLength(35);
            organization.CorporatePersonInfo.Company = organization.CorporatePersonInfo.Company.LimitLength(50);
            organization.CorporatePersonInfo.Country = organization.CorporatePersonInfo.Country.LimitLength(40);
            organization.CorporatePersonInfo.DayPhone = organization.CorporatePersonInfo.DayPhone.LimitLength(40);
            organization.CorporatePersonInfo.EmailID = organization.CorporatePersonInfo.EmailID.LimitLength(150);
            organization.CorporatePersonInfo.State = organization.CorporatePersonInfo.State.LimitLength(35);
            organization.CorporatePersonInfo.ZipCode = organization.CorporatePersonInfo.ZipCode.LimitLength(35);

            organization.Node.NodeType = organization.Node.NodeType.LimitLength(40);
            organization.Node.ShipNode = organization.Node.ShipNode.LimitLength(24);
            organization.Node.ShipnodeType = organization.Node.ShipnodeType.LimitLength(20);

        }
    }
}
