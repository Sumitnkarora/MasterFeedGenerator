using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGoogleCategoriesForIndigoCategoriesImporter.Input
{
    public class InputRecord
    {
        public int IndigoCategoryId { get; set; }
        public string IndigoBreadcrumb { get; set; }
        public string CurrentGoogleBreadcrumb { get; set; }
        public string NewGoogleBreadcrumb { get; set; }

        public override string ToString()
        {
            var result =
                string.Format(
                    "IndigoCategoryId: [{0}], IndigoBreadcrumb: [{1}], CurrentGoogleBreadcrumb: [{2}], NewGoogleBreadcrumb: [{3}].",
                    this.IndigoCategoryId, this.IndigoBreadcrumb, this.CurrentGoogleBreadcrumb, this.NewGoogleBreadcrumb);

            return result;
        }
    }
}
