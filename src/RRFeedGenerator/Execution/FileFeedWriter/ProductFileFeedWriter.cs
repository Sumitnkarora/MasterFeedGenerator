using System;
using System.Data;
using RRFeedGenerator.Execution.FileFeedWriter.Helpers;

namespace RRFeedGenerator.Execution.FileFeedWriter
{
    // Not Thread safe
    class ProductFileFeedWriter : AbstractFileFeedWriter
    {
        public ProductFileFeedWriter(InputContext inputContext) : base(inputContext) { }

        protected override string FileFeedPathBase
        {
            get { return Constants.ProductFilesPath; }
        }

        protected override bool DoWrite(IDataReader dataReader)
        {
            Context.Log.Debug("Enter ProductFeedFileWriter.DoWrite");

            var utility = new Utility(dataReader, Context);
            var outputLine = new OutputLine('|', Context, utility);

            string salePriceString;
            string listPriceString;

            outputLine
                .Add("gId")                                                             // Product ID
                .Add("title", "title_fr", title =>                                      // Product Name
                {
                    var titleResult = ((string) title).Replace("\n", string.Empty).Replace("\r", string.Empty);

                    titleResult = titleResult.Substring(0, Math.Min((titleResult).Length, 255))
                        .Replace('|', '-');

                    return titleResult;
                })
                .Add(null)                                                              // Product Parent ID
                .Add("price", price=> ((decimal)price).ToString("0.00"))                // Unit Price
                .AddLiteral("true")                                                     // Recommendable
                .Add("linkSku", sku => utility.GetEntryImageLink(sku.ToString()))       // Image Path
                .AddLiteral(utility.GetFeedEntryLinkValue(Context.Language))            // Product URL
                // Product Rating
                .Add("AverageRating", "AverageRating_fr", rating => rating == DBNull.Value ? "0" : ((decimal)rating).ToString("0.0000"))
                // Number of reviews
                .Add("NumberOfReviews", "NumberOfReviews_fr", reviewCount => reviewCount == DBNull.Value ? "0" : reviewCount.ToString())
                .Add("gBrand", "gBrand_fr")                                             // Product brand
                .AddLiteral(salePriceString = utility.GetSalePriceString())             // Minimum sale price
                .AddLiteral(salePriceString)                                            // Maximum sale price
                .AddLiteral(listPriceString = utility.GetListPriceString())             // Minimum list price
                .AddLiteral(listPriceString);                                           // Maximum list price

            var result = outputLine.ToString();
            Context.StreamWriter.WriteLine(result);

            Context.Log.Debug("Exit ProductFeedFileWriter.DoWrite");

            return true;
        }
    }

}
