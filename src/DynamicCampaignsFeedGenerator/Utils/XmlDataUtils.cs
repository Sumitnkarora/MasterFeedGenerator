using System.Configuration;
using Castle.Core.Internal;
using FeedGenerators.Core.Utils;
using DynamicCampaignsFeedGenerator.Execution;
using Indigo.Feeds.Entities.Abstract;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Indigo.Feeds.Utils;

namespace DynamicCampaignsFeedGenerator.Utils
{
    internal static class XmlDataUtils
    {
        private static readonly string OutputFolderPath = ParameterUtils.GetParameter<string>("DynamicCampaignsFeedGenerator.OutputFolderPath");
        private static readonly string FileNameFormat = ParameterUtils.GetParameter<string>("FileNameFormat");
        private static readonly string BaseUrl = ParameterUtils.GetParameter<string>("BaseUrl");
        private static readonly XNamespace FeedXmlns = ParameterUtils.GetParameter<string>("FeedXmlns");
        private static readonly DateTime FeedUpdated = DateTime.UtcNow;

        internal static string GetFeedEntryLinkValue(IDataReader reader, string catalog, string sku)
        {
            return FeedUtils.GetProductUrl(BaseUrl, catalog, sku);
        }

        internal static XElement FeedEntryLink(string url)
        {
            return new XElement(FeedXmlns + "Url", url);
        }

        // This indicates the priority of which contributor type to look for
        // and try to return. When one isn't found than the next is scanned for.
        private static string[] bookContributorTypes;
        private static string[] BookContributorTypes
        {
            get
            {
                if (bookContributorTypes != null)
                {
                    return bookContributorTypes;
                }

                var configValue = ConfigurationManager.AppSettings["BookContributorTypes"];
                if (configValue == null)
                {
                    bookContributorTypes = new[]
                    {
                        "author",
                        "editor",       // In the dev data most times "editor" should take priority over 
                        "illustrator",  // "illustrator", however sometimes the case is reversed.
                        "other"         // Not sure how to deal with that.
                    };
                    return bookContributorTypes;
                }

                bookContributorTypes = configValue.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);

                return bookContributorTypes;
            }
        }

        internal static string GetBrand(string brand, string catalog)
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

        internal static string GetFeedFileName(string identifier)
        {
            return String.Format(FileNameFormat, identifier);
        }

        internal static string GetFeedFilePath(string identifier, bool isSecondaryFeed)
        {
            //feed file name
            var fileName = GetFeedFileName(identifier);
            if (!Directory.Exists(OutputFolderPath))
                Directory.CreateDirectory(OutputFolderPath);

            return Path.Combine(OutputFolderPath, fileName);
        }

        internal static void StartXmlDocument(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("feed", FeedXmlns.NamespaceName);
            var rootupdated = new XElement(FeedXmlns + "updated", FeedUpdated.ToUniversalTime().ToString(CultureInfo.InvariantCulture));
            rootupdated.WriteTo(xmlWriter);
        }

        internal static void EndXmlDocument(XmlWriter xmlWriter)
        {
            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();
        }
    }
}
