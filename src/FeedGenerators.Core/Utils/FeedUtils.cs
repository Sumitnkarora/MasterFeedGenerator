using BinaryAnalysis.UnidecodeSharp;
using Castle.Core.Logging;
using FeedGenerators.Core.Enums;
using FeedGenerators.Core.Services.Abstract;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Models.Abstract;
using Indigo.Feeds.Types;
using Indigo.Feeds.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Xml.Linq;

namespace FeedGenerators.Core.Utils
{
    using XE = XElement;

    public static class FeedUtils
    {
        private static readonly bool ReplaceSpecialCharacters = ParameterUtils.GetParameter<bool>("ReplaceSpecialCharacters");
        private static readonly char[] AllowedSpecialCharacters = ParameterUtils.GetParameter<string>("AllowedSpecialCharacters").Replace(",", "").ToCharArray();
        internal static readonly Dictionary<string, Tuple<string, string>> StringResourceDictionary = ConfigurationManager.GetSection("stringresourcedictionary") as Dictionary<string, Tuple<string, string>>;
        private static readonly string BlacklistedGoogleAvailabilities = ParameterUtils.GetParameter<string>("BlacklistedGoogleAvailabilities");
        private static readonly StringDictionary ItemFormatDictionary = ConfigurationManager.GetSection("itemformatdict") as StringDictionary;

        /// <summary>
        /// Removes non-printable characters from the string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string SanitizeString(IEnumerable<char> input)
        {
            var sanitizedString = "";
            foreach (var character in input)
            {
                if (character == 0x9 /* == '\t' == 9   */          ||
                    character == 0xA /* == '\n' == 10  */          ||
                    character == 0xD /* == '\r' == 13  */          ||
                    (character >= 0x20 && character <= 0xD7FF) ||
                    (character >= 0xE000 && character <= 0xFFFD) ||
                    (character >= 0x10000 && character <= 0x10FFFF))
                {
                    sanitizedString += character;
                }
            }

            return (!ReplaceSpecialCharacters) ? sanitizedString : sanitizedString.Unidecode(AllowedSpecialCharacters);
        }

        /// <summary>
        /// Maps availabilityId to google's availability strings
        /// </summary>
        /// <param name="availabilityId"></param>
        /// <returns>Null if it's an "invalid" availability, otherwise a string that's inline with google's g:availability node's requirements</returns>
        public static string GetGoogleAvailability(int availabilityId)
        {
            string id;
            switch (availabilityId)
            {
                case 1:
                case 15:
                    id = "instock";
                    break;
                case 4:
                    id = "preorder";
                    break;
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                case 12:
                case 14:
                case 26:
                    id = "outofstock";
                    break;
                default:
                    id = "availablefororder";
                    break;
            }

            return (BlacklistedGoogleAvailabilities.Contains(id)) ? null : StringResourceDictionary[id].Item2;
        }

        public static FeedSectionType GetFeedSectionType(string catalog)
        {
            switch (catalog.ToLowerInvariant())
            {
                case "books":
                case "ebooks":
                    return FeedSectionType.Books;
                case "generalmerchandise":
                case "toys":
                    return FeedSectionType.Gifts;
                default:
                    throw new ArgumentException("Invalid catalog value: " + catalog);

            }
        }

        public static IIndigoBreadcrumbCategory GetFeedGeneratorIndigoCategory(IFeedGeneratorIndigoCategoryService feedGeneratorIndigoCategoryService, IDataReader reader, StringDictionary dict, string catalog, ILogger log)
        {
            var feedSectionType = GetFeedSectionType(catalog);
            var browseCategories = new List<int>();
            string recordType = null;

            if (dict.ContainsKey("gProductType"))
                browseCategories = reader[dict["gProductType"]].ToString().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

            if (feedSectionType == FeedSectionType.Gifts)
                recordType = reader[dict["recordType"]].ToString();

            return GetFeedGeneratorIndigoCategory(feedGeneratorIndigoCategoryService, feedSectionType, browseCategories,
                recordType);
        }

        private static IIndigoBreadcrumbCategory GetFeedGeneratorIndigoCategory(IFeedGeneratorIndigoCategoryService feedGeneratorIndigoCategoryService, FeedSectionType feedSectionType,
            List<int> browseCategories, string recordType)
        {
            return feedGeneratorIndigoCategoryService.GetDefaultIndigoBreadcrumb(feedSectionType, browseCategories, recordType);
        }

        public static IProductData GetProductData(StringDictionary dict, IDataReader reader, string sku, string catalog, string brandName, XE contributorElement, IIndigoBreadcrumbCategory defaultCategory)
        {
            var browseCategories = new List<int>();
            string secondarySku = null;
            string author = null; 
            if (dict.ContainsKey("gProductType"))
            {
                var browseCategoriesValue = reader[dict["gProductType"]];
                if (browseCategoriesValue != null)
                {
                    browseCategories.AddRange(browseCategoriesValue.ToString().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
                }
            }

            // For books and ebooks, set the secondary sku too
            if (catalog.Equals("books", StringComparison.InvariantCultureIgnoreCase) ||
                catalog.Equals("ebooks", StringComparison.InvariantCultureIgnoreCase))
            {
                secondarySku = reader[dict["secondarySku"]].ToString();
                if (contributorElement != null)
                    author = contributorElement.Value;
            }

            return IndigoBreadcrumbRepositoryUtils.GetProductData(sku, secondarySku, brandName, author, defaultCategory, browseCategories);
        }

        public static string RemoveHtmlTags(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            var array = new char[input.Length];
            var arrayIndex = 0;
            var inside = false;

            for (var ii = 0; ii < input.Length; ii++)
            {
                char let = input[ii];
                if (let == '<')
                {
                    inside = true;
                    continue;
                }
                if (let == '>')
                {
                    inside = false;
                    continue;
                }
                if (inside) continue;
                array[arrayIndex] = @let;
                arrayIndex++;
            }
            return (new string(array, 0, arrayIndex)).Trim().Replace("&nbsp;", " ");
        }

        public static string GetImageFileSectionName(IDataRecord reader)
        {
            var imgSection = string.Empty;

            var recordType = reader["RecordType"].ToString().ToLowerInvariant();

            switch (recordType)
            {
                case "book":
                    imgSection = "books";
                    break;
                case "dvd":
                    imgSection = "dvd";
                    break;
                case "popular":
                case "classical":
                    imgSection = "music";
                    break;
                case "videogame":
                    imgSection = "videogames";
                    break;
                case "gift":
                case "ipod":
                case "ereadingaccessory":
                case "ereadingdevice":
                case "gcard":
                case "gcard_ireward":
                case "appleelectronics":
                case "ag":
                case "gcard_electronic":
                    imgSection = "gifts";
                    break;
                case "toy":
                    imgSection = "toys";
                    break;
                case "ebook":
                    imgSection = "ebooks";
                    break;
                case "generalmerchandise":
                    imgSection = recordType.Contains("toy") ? "toys" : "gifts";
                    break;
                default:
                    throw new Exception("Can't get the image file section name.");
            }

            return imgSection;
        }

        public static string GetProductUrl(string baseUrl, string catalog, string sku, Enums.Language language = Enums.Language.English, bool excludeExtension = false)
        {
            string languageComponent;

            switch (language)
            {
                case(Enums.Language.French):
                    languageComponent = "fr-ca";
                    break;
                default:
                    languageComponent = "en-ca";
                    break;
            }
            var extension = (language == Enums.Language.French) ? "-article.html" : "-item.html";
            var format = (excludeExtension) ? "{0}/{1}/{2}/product/{3}" : "{0}/{1}/{2}/product/{3}" + extension;

            return string.Format(format, baseUrl, languageComponent, catalog, sku);
        }

        /// <summary>
        /// Gets a catalog name localized to the language specified from the attributes section that was passed in.
        /// </summary>
        /// <param name="attributesDictionary">Config-based dictionary containing different config settings</param>
        /// <param name="language"></param>
        /// <returns>A string with the localized catalog name</returns>
        public static string GetCatalog(StringDictionary attributesDictionary, Enums.Language language)
        {
            var languagePrefix = string.Empty;

            switch (language)
            {
                case(Enums.Language.French):
                    languagePrefix = "Fr";
                    break;
            }

            return attributesDictionary["linkCatalog" + languagePrefix];
        }

        public static string GetFormat(string code, string pid)
        {
            var key = code.ToLower().Trim();
            if (ItemFormatDictionary.ContainsKey(key))
            {
                var format = ItemFormatDictionary[key];
                return format;
            }

            return "Other Format";
        }

        public static string GetTruncatedTitle(string title, string format, int maxCharacters, bool truncateTitle = true)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return title.Length > maxCharacters ? title.Substring(0, maxCharacters) : title;
            }

            if(truncateTitle)
            { 
                var maxLength = maxCharacters - (format.Length + 3);
                return title.Length > maxLength ? $"{title.Substring(0, maxLength)} - {format}" : $"{title} - {format}"; 
            }

            var titleFormat = $"{title} - {format}";
            return titleFormat.Length > maxCharacters ? titleFormat.Substring(0, maxCharacters) : titleFormat;
        }
    }
}
