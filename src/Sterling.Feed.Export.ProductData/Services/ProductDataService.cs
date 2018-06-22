using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Xml.Linq;
using FeedGenerators.Core.Utils;
using Indigo.Feeds.Generator.Core.Enums;
using Indigo.Feeds.Generator.Core.Extensions;
using Indigo.Feeds.Generator.Core.Models;
using Indigo.Feeds.Generator.Core.Services;
using Indigo.Feeds.Utils;
using Sterling.Feed.Export.ProductData.Models;

namespace Sterling.Feed.Export.ProductData.Services
{
    public class ProductDataService : IDataService
    {
        private static readonly string RootTag = "ItemList";
        private const string DATE_FORMAT = "yyyy-MM-ddTH:mm:ss.fffK";
        private static readonly string ItemTag = "Item";
        private static readonly string PrimaryInformationTag = "PrimaryInformation";
        private static readonly string ClassificationCodesTag = "ClassificationCodes";
        private static readonly string SafetyFactorDefinitionsTag = "SafetyFactorDefinitions";
        private static readonly string SafetyFactorDefinitionTag = "SafetyFactorDefinition";
        private static readonly string ItemLocaleListTag = "ItemLocaleList";
        private static readonly string ItemLocaleTag = "ItemLocale";
        private static readonly string AdditionalAttributeListTag = "AdditionalAttributeList";
        private static readonly string AdditionalAttributeTag = "AdditionalAttribute";
        private static readonly bool IncludeMerchantCategoryInLayoutModule = ParameterUtils.GetParameter<bool>("IncludeMerchantCategoryInLayoutModule");
        private static readonly decimal OnhandSafetyFactorQuantity = ParameterUtils.GetParameter<decimal>("OnhandSafetyFactorQuantity");




        public XElement ConvertToXml(ExportData data)
        {
            Item itemData = (Item)data;
            XElement primaryInformation = null;
            XElement classificationCodes = null;
            XElement safetyFactorDefinitions = null;
            XElement safetyFactorDefinition = null;
            XElement itemLocaleList = null;
            XElement itemLocale = null;
            XElement itemLocalePrimaryInformation = null;
            XElement additionalAttributeList = null;


            XElement itemXElement = new XElement(ItemTag,
                                                  new XAttribute("ItemID", itemData.ItemID),
                                                  new XAttribute("SyncTS", itemData.SyncTS));

            //Handle case for new products
            if (itemData.Action != null && itemData.OrganizationCode != null && itemData.UnitOfMeasure != null)
            {
                itemXElement.Add(new XAttribute("Action", itemData.Action));
                itemXElement.Add(new XAttribute("OrganizationCode", itemData.OrganizationCode));
                itemXElement.Add(new XAttribute("UnitOfMeasure", itemData.UnitOfMeasure));
            }
            //Handle case for deleted products
            if (itemData.PrimaryInformation.Status == "2000")
            {
                primaryInformation = new XElement(PrimaryInformationTag, new XAttribute("Status", itemData.PrimaryInformation.Status));
                itemXElement.Add(primaryInformation);
                return itemXElement;
            }

            //Primary Information
            primaryInformation = new XElement(PrimaryInformationTag,
                                                  new XAttribute("AllowGiftWrap", itemData.PrimaryInformation.AllowGiftWrap),
                                                  new XAttribute("ExtendedDescription", itemData.PrimaryInformation.ExtendedDescription),
                                                  new XAttribute("IsPickupAllowed", itemData.PrimaryInformation.IsPickupAllowed),
                                                  new XAttribute("IsReturnable", itemData.PrimaryInformation.IsReturnable),
                                                  new XAttribute("IsShippingAllowed", itemData.PrimaryInformation.IsShippingAllowed),
                                                  new XAttribute("ReturnWindow", itemData.PrimaryInformation.ReturnWindow),
                                                  new XAttribute("ShortDescription", itemData.PrimaryInformation.ShortDescription),
                                                  new XAttribute("Status", itemData.PrimaryInformation.Status),
                                                  new XAttribute("ProductLine", itemData.PrimaryInformation.ProductLine),
                                                  new XAttribute("ImageLocation", itemData.PrimaryInformation.ImageLocation),
                                                  new XAttribute("UnitHeight", itemData.PrimaryInformation.UnitHeight),
                                                  new XAttribute("UnitHeightUOM", itemData.PrimaryInformation.UnitHeightUOM),
                                                  new XAttribute("UnitLength", itemData.PrimaryInformation.UnitLength),
                                                  new XAttribute("UnitLengthUOM", itemData.PrimaryInformation.UnitLengthUOM),
                                                  new XAttribute("UnitWeight", itemData.PrimaryInformation.UnitWeight),
                                                  new XAttribute("UnitWeightUOM", itemData.PrimaryInformation.UnitWeightUOM),
                                                  new XAttribute("UnitWidth", itemData.PrimaryInformation.UnitWidth),
                                                  new XAttribute("UnitWidthUOM", itemData.PrimaryInformation.UnitWidthUOM)
                                                  );


            //Classification codes
            classificationCodes = new XElement(ClassificationCodesTag,
                                                  new XAttribute("CommodityCode", itemData.ClassificationCodes.CommodityCode));

            //Safety factor definitions
            safetyFactorDefinitions = new XElement(SafetyFactorDefinitionsTag,
                                                  new XAttribute("Reset", itemData.SafetyFactorDefinitions.Reset));

            //Safety factor definition
            safetyFactorDefinition = new XElement(SafetyFactorDefinitionTag,
                                                  new XAttribute("DeliveryMethod", itemData.SafetyFactorDefinitions.SafetyFactorDefinition.DeliveryMethod),
                                                  new XAttribute("OnhandSafetyFactorQuantity", itemData.SafetyFactorDefinitions.SafetyFactorDefinition.OnhandSafetyFactorQuantity));

            //Item locale list
            itemLocaleList = new XElement(ItemLocaleListTag,
                                                  new XAttribute("Reset", itemData.ItemLocaleList.Reset));

            //Item Locale
            itemLocale = new XElement(ItemLocaleTag,
                                                  new XAttribute("Country", itemData.ItemLocaleList.ItemLocale.Country),
                                                  new XAttribute("Language", itemData.ItemLocaleList.ItemLocale.Language));

            //Item locale primary information
            itemLocalePrimaryInformation = new XElement(PrimaryInformationTag,
                                                  new XAttribute("ShortDescription", itemData.ItemLocaleList.ItemLocale.PrimaryInformation.ShortDescription),
                                                  new XAttribute("ExtendedDisplayDescription", itemData.ItemLocaleList.ItemLocale.PrimaryInformation.ExtendedDisplayDescription));


            //Additional Attribute List
            additionalAttributeList = new XElement(AdditionalAttributeListTag,
                                                  new XAttribute("Reset", itemData.AditionalAttributeList.Reset));

            //Additional Attributes
            foreach (AdditionalAttribute attribute in itemData.AditionalAttributeList.AdditionalAttribute)
            {
                additionalAttributeList.Add(new XElement(AdditionalAttributeTag,
                    new XAttribute("AttributeDomainID", attribute.AttributeDomainID),
                    new XAttribute("AttributeGroupID", attribute.AttributeGroupID),
                    new XAttribute("Operation", attribute.Operation),
                    new XAttribute("Name", attribute.Name),
                    new XAttribute("Value", attribute.Value)));
            }

            itemXElement.Add(primaryInformation);
            itemXElement.Add(classificationCodes);
            safetyFactorDefinitions.Add(safetyFactorDefinition);
            itemXElement.Add(safetyFactorDefinitions);
            itemLocale.Add(itemLocalePrimaryInformation);
            itemLocaleList.Add(itemLocale);
            itemXElement.Add(itemLocaleList);
            itemXElement.Add(additionalAttributeList);

            return itemXElement;
        }

        public DataResult GetData(StringDictionary attributeDictionary, IDataReader reader, string catalog, RunType runType)
        {
            decimal productHeight = 0;
            decimal productWeight = 0;
            decimal productDepth = 0;
            decimal productWidth = 0;
            decimal onHandSafetyFactor = 0;
            string people = string.Empty;
            string brand = string.Empty;
            string series = string.Empty;

            string[] pairs;
            string[] nameAndValue;
            Item itemData = new Item()
            {
                PrimaryInformation = new PrimaryInformation(),
                ClassificationCodes = new ClassificationCodes(),
                SafetyFactorDefinitions = new SafetyFactorDefinitions(),
                ItemLocaleList = new ItemLocaleList(),
                AditionalAttributeList = new AdditionalAttributeList()
            };

            //Item
            itemData.ItemID = reader["SKU"] == DBNull.Value ? string.Empty : reader["SKU"].ToString().Trim();
            itemData.SyncTS = reader["LastModified"] == DBNull.Value ? string.Empty : Convert.ToDateTime(reader["LastModified"]).ToUniversalTime().ToString(DATE_FORMAT);
            if (runType == RunType.Incremental)
            {
                itemData.Action = "Manage";
                itemData.OrganizationCode = "Indigo_CA";
                itemData.UnitOfMeasure = "Each";
            }
            else
            {
                itemData.Action = null;
                itemData.OrganizationCode = null;
                itemData.UnitOfMeasure = null;
            }

            itemData.PrimaryInformation.Status = reader["IsDeleted"] == DBNull.Value ? "3000" : Convert.ToBoolean(reader["IsDeleted"]) ? "2000" : "3000";
            if (itemData.PrimaryInformation.Status == "2000" && runType == RunType.Incremental)
            {
                return new DataResult() { ExportData = itemData };
            }


            //Primary Information
            itemData.PrimaryInformation.AllowGiftWrap = reader["AllowGiftWrap"] == DBNull.Value ? string.Empty : reader["AllowGiftWrap"].ToString().Trim();
            itemData.PrimaryInformation.ExtendedDescription = reader["FrenchTitle"] == DBNull.Value ? string.Empty : reader["FrenchTitle"].ToString().Trim();
            itemData.PrimaryInformation.IsPickupAllowed = reader["IsPickupAllowed"] == DBNull.Value ? string.Empty : reader["IsPickupAllowed"].ToString().Trim();
            itemData.PrimaryInformation.IsShippingAllowed = reader["IsShippingAllowed"] == DBNull.Value ? string.Empty : reader["IsShippingAllowed"].ToString().Trim();
            itemData.PrimaryInformation.IsReturnable = reader["IsReturnable"] == DBNull.Value ? string.Empty : reader["IsReturnable"].ToString().Trim();
            itemData.PrimaryInformation.ShortDescription = reader["Title"] == DBNull.Value ? string.Empty : reader["Title"].ToString().Trim();

            itemData.PrimaryInformation.ProductLine = reader["MerchCategoryId"] == DBNull.Value ? string.Empty : reader["MerchCategoryId"].ToString().Trim();
            itemData.PrimaryInformation.ImageLocation = reader["ImageLocation"] == DBNull.Value ? string.Empty : reader["ImageLocation"].ToString().Trim();
            itemData.PrimaryInformation.Catalogue = reader["ImageLocation"] == DBNull.Value ? string.Empty : reader["ImageLocation"].ToString().Trim();
            if (itemData.PrimaryInformation.Catalogue == "Books")
            {
                itemData.PrimaryInformation.UnitHeightUOM = "CM";
                itemData.PrimaryInformation.UnitLengthUOM = "CM";
                itemData.PrimaryInformation.UnitWidthUOM = "CM";
                itemData.PrimaryInformation.UnitWeightUOM = "KG";
            }
            else
            {
                itemData.PrimaryInformation.UnitHeightUOM = "IN";
                itemData.PrimaryInformation.UnitLengthUOM = "IN";
                itemData.PrimaryInformation.UnitWidthUOM = "IN";
                itemData.PrimaryInformation.UnitWeightUOM = "LBS";


            }
            if (!string.IsNullOrEmpty(reader["ProductHeight"].ToString()) && decimal.TryParse(reader["ProductHeight"].ToString(), out productHeight))
            {
                itemData.PrimaryInformation.UnitHeight = productHeight;
            }
            if (!string.IsNullOrEmpty(reader["ProductWidth"].ToString()) && decimal.TryParse(reader["ProductWidth"].ToString(), out productWidth))
            {
                itemData.PrimaryInformation.UnitWidth = productWidth;
            }
            if (!string.IsNullOrEmpty(reader["Weight"].ToString()) && decimal.TryParse(reader["Weight"].ToString(), out productWeight))
            {
                itemData.PrimaryInformation.UnitWeight = productWeight;
            }
            if (!string.IsNullOrEmpty(reader["ProductDepth"].ToString()) && decimal.TryParse(reader["ProductDepth"].ToString(), out productDepth))
            {
                itemData.PrimaryInformation.UnitLength = productDepth;
            }

            //classification code
            if (IncludeMerchantCategoryInLayoutModule)
            {
                itemData.ClassificationCodes.CommodityCode = reader["LayoutModuleId"] == DBNull.Value ? string.Empty : itemData.PrimaryInformation.ProductLine + "-" + reader["LayoutModuleId"].ToString().Trim();
            }
            else
            {
                itemData.ClassificationCodes.CommodityCode = reader["LayoutModuleId"] == DBNull.Value ? string.Empty : reader["LayoutModuleId"].ToString().Trim();
            }

            //SafetyFactor
            if (!string.IsNullOrEmpty(reader["OnHandSafetyFactor"].ToString()) && decimal.TryParse(reader["OnHandSafetyFactor"].ToString(), out onHandSafetyFactor))
            {
                itemData.SafetyFactorDefinitions.SafetyFactorDefinition.OnhandSafetyFactorQuantity = onHandSafetyFactor;
            }
            else
            {
                itemData.SafetyFactorDefinitions.SafetyFactorDefinition.OnhandSafetyFactorQuantity = OnhandSafetyFactorQuantity;
            }
            itemData.SafetyFactorDefinitions.Reset = "Y";
            itemData.SafetyFactorDefinitions.SafetyFactorDefinition.DeliveryMethod = "PICK";

            //ItemLocaleList
            itemData.ItemLocaleList.Reset = "Y";
            itemData.ItemLocaleList.ItemLocale.PrimaryInformation.ShortDescription = itemData.PrimaryInformation.ExtendedDescription;
            itemData.ItemLocaleList.ItemLocale.PrimaryInformation.ExtendedDisplayDescription = itemData.PrimaryInformation.ExtendedDescription;

            itemData.ItemLocaleList.ItemLocale.Country = "CA";
            itemData.ItemLocaleList.ItemLocale.Language = "fr";

            //AdditionalAttributeList
            itemData.AditionalAttributeList.Reset = "Y";
            people = reader["People"] == DBNull.Value ? string.Empty : reader["People"].ToString().Trim();
            if (!string.IsNullOrEmpty(people.Trim()))
            {
                pairs = people.Split('~');
                if (pairs.Length > 0)
                {
                    foreach (string pair in pairs)
                    {
                        nameAndValue = pair.Split('^');
                        if (nameAndValue.Length > 1 && !string.IsNullOrEmpty(nameAndValue[0]) && !string.IsNullOrEmpty(nameAndValue[1]))
                        {
                            itemData.AditionalAttributeList.AdditionalAttribute.Add(new AdditionalAttribute()
                            {
                                Name = nameAndValue[0],
                                Value = nameAndValue[1],
                                AttributeDomainID = "ItemAttribute",
                                AttributeGroupID = "ItemReferenceGroup",
                                Operation = "Manage"
                            });
                        }
                    }
                }
            }

            brand = reader["Brand"] == DBNull.Value ? string.Empty : reader["Brand"].ToString().Trim();
            if (!string.IsNullOrEmpty(brand.Trim()))
            {
                itemData.AditionalAttributeList.AdditionalAttribute.Add(new AdditionalAttribute()
                {
                    Name = "Brand",
                    Value = brand,
                    AttributeDomainID = "ItemAttribute",
                    AttributeGroupID = "ItemReferenceGroup",
                    Operation = "Manage"
                });
            }

            series = reader["Series"] == DBNull.Value ? string.Empty : reader["Series"].ToString().Trim();
            if (!string.IsNullOrEmpty(series.Trim()))
            {
                itemData.AditionalAttributeList.AdditionalAttribute.Add(new AdditionalAttribute()
                {
                    Name = "Series",
                    Value = series,
                    AttributeDomainID = "ItemAttribute",
                    AttributeGroupID = "ItemReferenceGroup",
                    Operation = "Manage"
                });
            }
            TrimFieldsLengthAndSanitizeString(itemData);
            return new DataResult() { ExportData = itemData };
        }

        public Type GetDataType()
        {
            return typeof(Item);
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

        private void TrimFieldsLengthAndSanitizeString(Item item)
        {
            item.Action = item.Action.LimitLength(40);
            item.ItemID = item.ItemID.LimitLength(40);
            item.OrganizationCode = item.OrganizationCode.LimitLength(24);
            item.UnitOfMeasure = item.UnitOfMeasure.LimitLength(40);

            if (item.PrimaryInformation.ExtendedDescription != null)
                item.PrimaryInformation.ExtendedDescription = FeedUtils.SanitizeString(item.PrimaryInformation.ExtendedDescription.LimitLength(2000));

            if (item.PrimaryInformation.ShortDescription != null)
                item.PrimaryInformation.ShortDescription = FeedUtils.SanitizeString(item.PrimaryInformation.ShortDescription.LimitLength(2000));

            item.PrimaryInformation.Status = item.PrimaryInformation.Status.LimitLength(15);
            item.PrimaryInformation.ProductLine = item.PrimaryInformation.ProductLine.LimitLength(1000);
            item.PrimaryInformation.ImageLocation = item.PrimaryInformation.ImageLocation.LimitLength(255);
            item.ClassificationCodes.CommodityCode = item.ClassificationCodes.CommodityCode.LimitLength(40);
            item.SafetyFactorDefinitions.SafetyFactorDefinition.DeliveryMethod = item.SafetyFactorDefinitions.SafetyFactorDefinition.DeliveryMethod.LimitLength(100);
            item.ItemLocaleList.ItemLocale.Country = item.ItemLocaleList.ItemLocale.Country.LimitLength(40);
            item.ItemLocaleList.ItemLocale.Language = item.ItemLocaleList.ItemLocale.Language.LimitLength(10);

            item.ItemLocaleList.ItemLocale.PrimaryInformation.ShortDescription = item.ItemLocaleList.ItemLocale.PrimaryInformation.ShortDescription.LimitLength(200);

            foreach (AdditionalAttribute attribute in item.AditionalAttributeList.AdditionalAttribute)
            {
                attribute.AttributeGroupID = attribute.AttributeGroupID.LimitLength(40);
                attribute.Operation = attribute.Operation.LimitLength(40);
                attribute.Name = attribute.Name.LimitLength(40);
                attribute.Value = FeedUtils.SanitizeString(attribute.Value.LimitLength(40));
            }
        }
    }
}
