using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RRFeedGenerator.Execution.FileFeedWriter.Helpers;

namespace RRFeedGenerator.Execution.FileFeedWriter
{
    // Not Thread safe
    class ProductToCategoryFileFeedWriter : AbstractFileFeedWriter
    {
        public ProductToCategoryFileFeedWriter(InputContext inputContext) : base(inputContext) { }
        
        protected override string FileFeedPathBase
        {
            get { return Constants.ProductCategoryFilesPath; }
        }

        protected override bool DoWrite(IDataReader dataReader)
        {
            Context.Log.Debug("Enter ProductToCategoryFileFeedWriter.DoWrite");

            var utility = new Utility(dataReader, Context);

            int? defaultIndigoCategoryId =

                    utility.GetDefaultIndigoCategoryId();

            if (defaultIndigoCategoryId == null)
                if (Constants.ExecutionLogBreadCrumbErrors)
                    throw new ApplicationException("ProductToCategoryFileFeedWriter.DoWrite(). defaultIndigoCategoryId is not available");
                else
                    return false;

            var productId = utility.GetAttributeValue("gId").ToString();

            var outputLine = defaultIndigoCategoryId + "|" + productId;
            Context.StreamWriter.WriteLine(outputLine);

            Context.Log.Debug("Exit ProductToCategoryFileFeedWriter.DoWrite");

            return true;
        }
    }
}
