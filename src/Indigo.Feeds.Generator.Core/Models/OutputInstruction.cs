using Indigo.Feeds.Generator.Core.Enums;
using System.Collections.Generic;

namespace Indigo.Feeds.Generator.Core.Models
{
    public class OutputInstruction
    {
        public OutputFormat Format { get; set; }

        public string CatalogName { get; set; }

        public IList<BaseExportData> Data { get; set; }
        
        public int Count { get; set; }

        public string OutputLocation { get; set; }

        public string OutputName { get; set; }
    }
}
