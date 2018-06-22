using Indigo.Feeds.Generator.Core.Models;
using System;

namespace Sterling.Feed.Export.RetailCategorization.Models
{
    public class TaxonomyData : ExportData
    {
        public string TaxonomyId { get; set; }
        public bool IsGeneralMerchandise { get; set; }
        public string Description_En { get; set; }
        public string Description_Fr { get; set; }
    }
}
