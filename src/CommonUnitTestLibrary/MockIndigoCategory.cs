using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Indigo.Feeds.Entities.Abstract;

namespace CommonUnitTestLibrary
{
    public class MockIndigoCategory : IIndigoCategory
    {
        public string BreadcrumbPath { get; set; }

        public int BrowseCategoryId { get; set; }

        public DateTime DateModified { get; set; }

        public string EndecaBreadcrumbId { get; set; }

        public int? GoogleCategoryId { get; set; }

        public int IndigoCategoryId { get; set; }

        public bool IsDeleted { get; set; }

        public bool IsModified { get; set; }

        public string ModifiedBy { get; set; }

        public string Name { get; set; }

        public string NameFr { get; set; }

        public int? ParentId { get; set; }
    }
}
