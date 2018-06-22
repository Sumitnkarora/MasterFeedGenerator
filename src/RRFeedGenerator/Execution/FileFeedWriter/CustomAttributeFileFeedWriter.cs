using System;
using System.Data;
using RRFeedGenerator.Execution.FileFeedWriter.Helpers;
using Indigo.Feeds.Types;

namespace RRFeedGenerator.Execution.FileFeedWriter
{
    // Not Thread safe
    class CustomAttributeFileFeedWriter : AbstractFileFeedWriter
    {
        public CustomAttributeFileFeedWriter(InputContext inputContext) : base(inputContext) { }

        protected override string FileFeedPathBase
        {
            get { return Constants.AttributeFilesPath; }
        }

        const string GeneralMerchandiseCatalogKey = "generalMerchandise"; 

        protected override bool DoWrite(IDataReader dataReader)
        {
            Context.Log.Debug("Enter CustomAttributeFileFeedWriter.DoWrite. Catalog: " + Context.LineItem.Catalog);

            var utility = new Utility(dataReader, Context);
            var output = new AttributeOutput("gId", dataReader, '|', Context, utility);

            switch (Context.LineItem.Catalog)
            {
                case "books":
                    WriteBooks(output, utility);
                    break;
                case GeneralMerchandiseCatalogKey:
                case "generalMerchandiseGiftCard":
                    WriteGeneralMerchandise(output, utility);
                    break;
                default:
                    throw new NotImplementedException("Catalog type not recognized.");
            }

            Context.StreamWriter.Write(output);

            Context.Log.Debug("Exit CustomAttributeFileFeedWriter.DoWrite");

            return true;
        }

        #region WriteBooks

        private void WriteBooks(AttributeOutput output, Utility utility)
        {
            output
                .Add("PRODUCTSKU", "linkSku");
            SetContributors(utility, output);
            output
                .AddLiteral("MAINCONTRIBUTOR", ExtractContributor(utility.GetMainContributor()))
                .Add("PUBLISHER", "publisherName");
            SetSpecs(utility, output);
            output
                .AddLiteral("CATALOG", Context.AttributesDictionary["linkCatalog"])
                .Add("IMAGEHEADER", "imageHeader");
        }

        private void SetSpecs(Utility utility, AttributeOutput output)
        {
            var pages = (int?)utility.GetAttributeValue("pages").DbNullToNull();
            var height = (decimal?)utility.GetAttributeValue("Height").DbNullToNull();
            var width = (decimal?)utility.GetAttributeValue("Width").DbNullToNull();
            var depth = (decimal?)utility.GetAttributeValue("Depth").DbNullToNull();

            string pagesString = pages != null ? pages.ToString() + " Pages" : null;

            string widthString = width != null ? width.Value.ToString("0.##") : null;
            string heightString = height != null ? height.Value.ToString("0.##") : null;
            string depthString = depth != null ? depth.Value.ToString("0.##") : null;

            string dimensions = null;

            if (widthString != null && heightString != null)
            {
                if (depthString != null)
                    dimensions = string.Join(" x ", new[] { heightString, widthString, depthString });
                else
                    dimensions = string.Join(" x ", new[] { heightString, widthString });

                dimensions += " in.";
            }

            string spec = string.Empty;

            if (pagesString != null && dimensions != null)
                spec = pagesString + ", " + dimensions;

            else if (pagesString != null)
                spec = pagesString;

            else if (dimensions != null)
                spec = dimensions;

            output.AddLiteral("SPECS", spec);
        }

        #endregion WriteBooks

        private void WriteGeneralMerchandise(AttributeOutput output, Utility utility)
        {
            output
                .Add("PRODUCTSKU", "linkSku")
                .Add<string>("MAINCONTRIBUTOR", "gBrand", "gBrand_fr",
                    contributor => contributor != null ? contributor.Replace("\n", string.Empty) : null)
                .AddLiteral("CATALOG", Context.AttributesDictionary["linkCatalog"])
                .Add("IMAGEHEADER", "imageHeader");

            if (Context.LineItem.Catalog.Equals(GeneralMerchandiseCatalogKey, StringComparison.Ordinal))
                output.Add("COLOR", "color_en", "color_fr")
                .Add("SIZE", "size_en", "size_fr")
                .Add("STYLE", "style_en", "style_fr")
                .Add("SCENT", "scent_en", "scent_fr")
                .Add("FLAVOR", "flavor_en", "flavor_fr");
        }

        private void SetContributors(Utility utility, AttributeOutput output)
        {
            var contributors = utility.GetContributorsArray();

            foreach (string contributor in contributors)
            {
                var typeNameDelimiterIndex = contributor.IndexOf(" ", StringComparison.Ordinal);

                if (typeNameDelimiterIndex < 0)
                {
                    var pid = utility.GetAttributeValue("gId");
                    Context.Log.ErrorFormat(
                        "CustomAttributeFileFeedWriter.SetContributors(). Contributor type/value delimiter not found. Not writing to output. contributor: \"{0}\", PID: {1}",
                        contributor, pid);

                    continue;
                }

                var contributorType = contributor.Substring(0, typeNameDelimiterIndex);
                var contributorName = contributor.Substring(typeNameDelimiterIndex + 1);

                output.AddLiteral(contributorType, contributorName);
            }
        }

        private static string ExtractContributor(string input)
        {
            var result = input.Substring(input.IndexOf(" ") + 1);

            return result;
        }

    }

}
