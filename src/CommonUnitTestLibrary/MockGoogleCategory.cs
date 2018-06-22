using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Indigo.Feeds.Entities.Abstract;

namespace CommonUnitTestLibrary
{
    public class MockGoogleCategory : IGoogleCategory
    {

        public string BreadcrumbPath
        {
            get;
            set;
        }

        public bool? ChildrenExist
        {
            get;
            set;
        }

        public DateTime DateModified
        {
            get;
            set;
        }

        public int GoogleCategoryId
        {
            get;
            set;
        }

        public int GoogleNativeCategoryId
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public int? ParentId
        {
            get;
            set;
        }
    }
}
