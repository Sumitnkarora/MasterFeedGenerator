using System;
using System.Linq;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Utils;

namespace GoogleCategoryImporter
{
    public class Category : IGoogleCategory
    {
        public class NativeCategoryIdParseException : Exception
        {
            public NativeCategoryIdParseException(string message, Exception ex)
                : base(message, ex)
            {
            }
        }

        private static readonly string GoogleCategoryLevelSplitter = ParameterUtils.GetParameter<string>("GoogleCategoryLevelSplitter");

        public string BreadcrumbTrail { get; private set; }
        public int Level { get; private set; }

        public Category(string breadcrumbPath, DateTime modifiedTime)
        {
            int endOfNativeCategoryId = breadcrumbPath.IndexOf(" - ", StringComparison.Ordinal);

            const string errorText = "Error occurred attempting to parse breadcrumbpath [{0}]";

            try
            {
                this.GoogleNativeCategoryId = Int32.Parse(breadcrumbPath.Substring(0, endOfNativeCategoryId));
            }
            catch (FormatException ex)
            {
                throw new NativeCategoryIdParseException(String.Format(errorText, breadcrumbPath), ex);
            }
            catch (OverflowException ex)
            {
                throw new NativeCategoryIdParseException(String.Format(errorText, breadcrumbPath), ex);
            }

            breadcrumbPath = breadcrumbPath.Substring(endOfNativeCategoryId + 3);

            var parts = breadcrumbPath.Split(new[] {GoogleCategoryLevelSplitter}, StringSplitOptions.RemoveEmptyEntries);

            Name = parts.Last();
            if (parts.Length > 1)
                BreadcrumbTrail = String.Join(GoogleCategoryLevelSplitter, parts.Take(parts.Length - 1));

            Level = parts.Length;
            DateModified = modifiedTime;
        }

        public int GoogleCategoryId { get; set; }
        public string Name { get; set; }
        public DateTime DateModified { get; set; }
        public int? ParentId { get; set; }
        public int GoogleNativeCategoryId { get; set; }

        public string BreadcrumbPath
        {
            get
            {
                return (Level == 1) ? Name : BreadcrumbTrail + GoogleCategoryLevelSplitter + Name;
            }
            set { } 
        }


        public bool? ChildrenExist
        {
            get { return Level > 1; }
        }
    }
}