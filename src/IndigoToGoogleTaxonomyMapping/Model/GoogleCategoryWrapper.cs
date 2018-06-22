using Indigo.Feeds.Entities.Abstract;

namespace IndigoToGoogleTaxonomyMapping.Model
{
    class GoogleCategoryWrapper
    {
        public GoogleCategoryWrapper(IGoogleCategory googleCategory)
        {
            GoogleCategory = googleCategory;
            Status = StatusEnum.NotProcessed;
        }

        public enum StatusEnum
        {
            NotProcessed,
            Same,
            Different,
        }

        public IGoogleCategory GoogleCategory
        {
            get;
            protected set;
        }

        public StatusEnum Status
        {
            get;
            set;
        }
    }
}
