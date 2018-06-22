using Indigo.Feeds.Entities.Concrete;
using System.Collections.Generic;

namespace GoogleApiPlaFeedGenerator.Json
{
    public class OutputInstruction
    {
        public OutputFormat Format { get; set; }
        public IList<string> Deletions { get; set; }
        public IList<GooglePlaProductData> Updates { get; set; }
        public int FileCount { get; set; }
    }
}
