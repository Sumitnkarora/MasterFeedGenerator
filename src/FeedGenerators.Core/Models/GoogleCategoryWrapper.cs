using System;
using Indigo.Feeds.Entities.Abstract;

namespace FeedGenerators.Core.Models
{
    public class GoogleCategoryWrapper : IGoogleCategory
    {
        private readonly IGoogleCategory googleCategory;

        public GoogleCategoryWrapper(IGoogleCategory googleCategory)
        {
            this.googleCategory = googleCategory;
            this.IsModified = false;
        }

        public string BreadcrumbPath
        {
            get { return this.googleCategory.BreadcrumbPath; }
            set { this.googleCategory.BreadcrumbPath = value; }
        }

        public bool? ChildrenExist
        {
            get { return this.googleCategory.ChildrenExist; }
        }

        public DateTime DateModified
        {
            get { return this.googleCategory.DateModified; }
            set { this.googleCategory.DateModified = value; }
        }

        public int GoogleCategoryId
        {
            get { return this.googleCategory.GoogleCategoryId; }
            set { this.googleCategory.GoogleCategoryId = value; }
        }

        public string Name
        {
            get { return this.googleCategory.Name; }
            set { this.googleCategory.Name = value; }
        }

        public int? ParentId
        {
            get { return this.googleCategory.ParentId; }
            set { this.googleCategory.ParentId = value; }
        }

        public bool IsModified { get; set; }


        public int GoogleNativeCategoryId
        {
            get { return this.googleCategory.GoogleNativeCategoryId; }
            set { this.googleCategory.GoogleNativeCategoryId = value; }
        }
    }
}
