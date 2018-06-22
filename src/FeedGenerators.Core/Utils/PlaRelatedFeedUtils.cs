using Castle.Core.Internal;
using FeedGenerators.Core.Execution;
using FeedGenerators.Core.Types;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Types;
using Indigo.Feeds.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace FeedGenerators.Core.Utils
{
    public static class PlaRelatedFeedUtils
    {
        private const string NsG = "g";
        private static readonly string PrimaryOutputFolderName = ParameterUtils.GetParameter<string>("PrimaryOutputFolderName");
        private static readonly string SecondaryOutputFolderName = ParameterUtils.GetParameter<string>("SecondaryOutputFolderName");
        private static readonly string FileNameFormat = ParameterUtils.GetParameter<string>("FileNameFormat");
        private static readonly string BaseUrl = ParameterUtils.GetParameter<string>("PhoenixOnlineBaseUrl");
        private static readonly XNamespace FeedXmlsnsG = ParameterUtils.GetParameter<string>("FeedXmlsnsG");
        private static readonly XNamespace FeedXmlns = ParameterUtils.GetParameter<string>("FeedXmlns");
        private static readonly string GoogleFeedTitle = ParameterUtils.GetParameter<string>("GoogleFeedTitle");
        private static readonly string GoogleImagePathSuffix = ParameterUtils.GetParameter<string>("GoogleImagePathSuffix");
        private static readonly string YahooImagePathSuffix = ParameterUtils.GetParameter<string>("YahooImagePathSuffix");
        private static readonly string ImgBaseUrl = ParameterUtils.GetParameter<string>("DynamicImagesUrl");
        private static readonly string DefaultShippingCountryAbbreviation = ParameterUtils.GetParameter<string>("DefaultShippingCountryAbbreviation");
        private static readonly string DefaultShippingServiceName = ParameterUtils.GetParameter<string>("DefaultShippingServiceName");
        private static readonly string DefaultShippingPriceText = ParameterUtils.GetParameter<string>("DefaultShippingPriceText");
        private static readonly string CpcValueFormat = ParameterUtils.GetParameter<string>("CpcValueFormat");
        private static readonly string DefaultCpcValue = ParameterUtils.GetParameter<string>("DefaultCpcValue");
        private static readonly string CanonicalProductDataValue = ParameterUtils.GetParameter<string>("CanonicalProductDataValue");

        public static string GetFeedEntryLinkValue(IDataReader reader, string catalog, string sku)
        {
            return catalog == "ebooks" ? reader["KoboItemPageURL"].ToString() : FeedUtils.GetProductUrl(BaseUrl, catalog, sku);
        }

        public static XElement FeedEntryLink(string url)
        {
            return new XElement(FeedXmlns + "link", url);
        }

        public static XElement EntryImageLink(IDataRecord reader, string sku, XNamespace ns, GoogleRunFeedType feedType)
        {
            var imgSection = FeedUtils.GetImageFileSectionName(reader);
            var imagePathSuffix = (feedType == GoogleRunFeedType.Google) ? GoogleImagePathSuffix : YahooImagePathSuffix;
            var imgUrl = string.Format("{0}/{1}/{2}.jpg{3}", ImgBaseUrl, imgSection, sku, imagePathSuffix);

            return new XElement(ns + "image_link", imgUrl);
        }

        public static XElement ContributorAttributes(StringDictionary dict, IDataRecord reader, string pid)
        {
            if (!dict.ContainsKey("contributors")) return null;

            var contributors = FeedUtils.SanitizeString(reader[dict["contributors"]].ToString());
            var dictC = GetContributors(contributors, pid);

            if (dictC.IsNullOrEmpty()) return null;

            const string star = "star";
            var dictContainsStar = dictC.ContainsKey(star);

            return new XElement(dictC.First().Key, string.Join(", ", dictC.First(keyValuePair =>
            {
                if (dictContainsStar && keyValuePair.Key.Equals(star))
                    return true;

                if (!dictC.ContainsKey(star))
                    return true;

                return false;

            }).Value));
        }

        private static Dictionary<string, List<string>> GetContributors(string contributors, string pid)
        {
            if (string.IsNullOrWhiteSpace(contributors))
            {
                return null;
            }

            var listC = new Dictionary<string, List<string>>();

            //get first word from the arg

            var words = contributors.Split(new[] { '^' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                var cType = word.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var name = cType[0];
                var value = word.TrimStart(name.ToCharArray()).Trim();
                //string trim;

                switch (name.ToLowerInvariant())
                {
                    case "author":              //endeca
                        break;
                    default:
                        name = string.Empty;
                        value = string.Empty;
                        break;
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    var key = name.ToLowerInvariant();
                    if (listC.ContainsKey(key))
                        listC[key].Add(value);
                    else
                    {
                        listC.Add(key, new List<string> { value });
                    }

                }
            }

            return listC;
        }

        public static XElement ShippingAttribute(string country, string service, string price, XNamespace ns)
        {
            var shippingAttribute = new XElement(ns + "shipping",
                                                      new XElement(FeedXmlsnsG + "country", country ?? DefaultShippingCountryAbbreviation),
                                                      new XElement(FeedXmlsnsG + "service", service ?? DefaultShippingServiceName),
                                                      new XElement(FeedXmlsnsG + "price", price ?? DefaultShippingPriceText));
            return shippingAttribute;
        }

        public static string GetDynamicMerchLabelValue(IGooglePlaFeedRuleHelper runnerFeed, IProductData productData)
        {
            var value = runnerFeed.GetDynamicMerchLabelValue(productData);
            return value != null ? value.Value : null;
        }

        public static string GetCustomLabelValue(IGooglePlaFeedRuleHelper runnerFeed, IProductData productData, FeedRuleType feedRuleType)
        {
            var customLabelValue = runnerFeed.GetCustomLabelValue(productData, feedRuleType);
            return customLabelValue != null ? customLabelValue.Value : null;
        }

        // If there's a value for the canonical data element in config, and if the product is canonical, then send that value in 
        // custom label 4
        public static string GetCustomLabel4Value(IGooglePlaFeedRuleHelper runnerFeed, IProductData productData, StringDictionary dict, IDataRecord reader)
        {
            string customLabelValue = null;
            if (!string.IsNullOrWhiteSpace(CanonicalProductDataValue) && dict.ContainsKey("isCanonical"))
            {
                bool databaseValue = false;
                bool.TryParse(reader[dict["isCanonical"]].ToString(), out databaseValue);
                if (databaseValue)
                    customLabelValue = CanonicalProductDataValue;
            }

            if (string.IsNullOrWhiteSpace(customLabelValue))
            {
                var ruleValue = runnerFeed.GetCustomLabelValue(productData, FeedRuleType.Custom_Label_4);
                customLabelValue = ruleValue != null ? ruleValue.Value : null;
            }

            return customLabelValue;
        }

        public static string GetCpcValue(IGooglePlaFeedRuleHelper runnerFeed, IProductData productData, out bool isDefaultMatch)
        {
            isDefaultMatch = false;
            var cpcValue = string.Empty;
            if (runnerFeed.GetRunFeedType() != GoogleRunFeedType.Yahoo) return cpcValue;

            var cpcResult = runnerFeed.GetCpcValue(productData);
            if (cpcResult == null)
            {
                isDefaultMatch = true;
                return string.Format(CpcValueFormat, DefaultCpcValue);
            }

            // Ensure proper cpc value
            var value = cpcResult.Value;
            isDefaultMatch = cpcResult.IsDefaultMatch || string.IsNullOrWhiteSpace(value);

            if (string.IsNullOrWhiteSpace(value))
                value = DefaultCpcValue;
            else
            {
                try
                {
                    var parts = value.Split(new[] { ',', '.' }, StringSplitOptions.None);
                    if (parts.Length == 1)
                        value = value.Replace(",", string.Empty).Replace(".", string.Empty) + ".00";

                    if (parts.Length == 2)
                    {
                        if (string.IsNullOrWhiteSpace(parts[0]))
                            value = "0" + value;

                        var cents = parts[1];
                        if (string.IsNullOrWhiteSpace(cents))
                            value += "00";
                        else
                        {
                            if (parts[1].Length == 1)
                                value += "0";
                            else if (cents.Length > 2)
                                value = parts[0] + "." + cents.Substring(0, 2);

                        }
                    }
                }
                catch (Exception)
                {
                    value = DefaultCpcValue;
                    isDefaultMatch = true;
                }
            }

            return string.Format(CpcValueFormat, value);

        }

        public static string GetFeedFileName(string identifier)
        {
            return String.Format(FileNameFormat, identifier);
        }

        public static string GetFeedFilePath(string identifier, bool isSecondaryFeed, string outputFolderPath)
        {
            //feed file name
            var fileName = GetFeedFileName(identifier);
            if (!Directory.Exists(outputFolderPath))
                Directory.CreateDirectory(outputFolderPath);

            var folderPath = (isSecondaryFeed) ? Path.Combine(outputFolderPath, SecondaryOutputFolderName) : Path.Combine(outputFolderPath, PrimaryOutputFolderName);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            return Path.Combine(folderPath, fileName);
        }

        public static void StartXmlDocument(XmlWriter xmlWriter, GoogleRunFeedType feedType, DateTime fileTime)
        {
            xmlWriter.WriteStartDocument();

            xmlWriter.WriteStartElement("feed", FeedXmlns.NamespaceName);
            if (feedType == GoogleRunFeedType.Google)
                xmlWriter.WriteAttributeString("xmlns", NsG, string.Empty, FeedXmlsnsG.NamespaceName);
            //<title>
            var roottitle = new XElement(FeedXmlns + "title", GoogleFeedTitle);
            roottitle.WriteTo(xmlWriter);
            //<link ref="">
            var rootlink = new XElement(FeedXmlns + "link", new XAttribute("href", BaseUrl));
            rootlink.WriteTo(xmlWriter);
            //<updated>
            var rootupdated = new XElement(FeedXmlns + "updated", fileTime.ToUniversalTime().ToString(CultureInfo.InvariantCulture));
            rootupdated.WriteTo(xmlWriter);
        }

        public static void EndXmlDocument(XmlWriter xmlWriter)
        {
            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();
        }

        // This indicates the priority of which contributor type to look for
        // and try to return. When one isn't found than the next is scanned for.
        private static string[] _bookContributorTypes;
        private static string[] BookContributorTypes
        {
            get
            {
                if (_bookContributorTypes != null)
                {
                    return _bookContributorTypes;
                }

                var configValue = ConfigurationManager.AppSettings["BookContributorTypes"];
                if (configValue == null)
                {
                    _bookContributorTypes = new[]
                    {
                        "author",
                        "editor",       // In the dev data most times "editor" should take priority over 
                        "illustrator",  // "illustrator", however sometimes the case is reversed.
                        "other"         // Not sure how to deal with that.
                    };
                    return _bookContributorTypes;
                }

                _bookContributorTypes = configValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                return _bookContributorTypes;
            }
        }

        public static string GetBrand(string brand, string catalog)
        {
            string result;

            switch (catalog)
            {
                case "Books":
                    result = FindMainContributor(brand, BookContributorTypes);
                    break;

                case "GeneralMerchandise":
                    result = brand;
                    break;

                default:
                    throw new ArgumentException(string.Format("Cannot recognize catalog: '{0}'", catalog));
            }

            return result;
        }

        private static string FindMainContributor(string contributorString, string[] knownContributorTypes)
        {
            if (contributorString == null)
            {
                return string.Empty;
            }

            var contributors = contributorString.Split('^');

            int contributorNameIndex = 0;
            string foundContributor = null;

            foreach (var knownContributorType in knownContributorTypes)
            {
                foundContributor = FindFirstContributorOfContributorType(contributors, knownContributorType,
                    out contributorNameIndex);

                if (foundContributor != null)
                {
                    break;
                }
            }

            if (foundContributor == null)
            {
                foundContributor = contributors[0];
                contributorNameIndex = foundContributor.IndexOf(" ") + 1;
            }

            var result = foundContributor.Substring(contributorNameIndex);

            return result;
        }

        private static string FindFirstContributorOfContributorType(string[] contributors, string contributorType, out int contributorNameIndex)
        {
            int innerContributorNameIndex = 0;

            var foundContributor = contributors.FirstOrDefault(contributor =>
            {
                if ((innerContributorNameIndex = contributor.IndexOf(" ")) == -1)
                {
                    return false;
                }

                var innerContributorType = contributor.Substring(0, innerContributorNameIndex);

                var result = innerContributorType.Equals(contributorType, StringComparison.OrdinalIgnoreCase);

                return result;
            });

            contributorNameIndex = foundContributor != null ? innerContributorNameIndex + 1 : 0;

            return foundContributor;
        }
    }
}
