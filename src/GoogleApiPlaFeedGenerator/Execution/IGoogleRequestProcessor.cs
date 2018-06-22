using Indigo.Feeds.Entities.Concrete;
using System.Collections.Generic;

namespace GoogleApiPlaFeedGenerator.Execution
{
    public interface IGoogleRequestProcessor
    {
        bool SendDeletionBatch(IList<string> productIdentifiers);

        bool SendUpdateBatch(IList<GooglePlaProductData> productDatas);

        IList<string> GetIdentifiersWithErroneousDescriptions();
    }
}
