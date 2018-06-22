using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Indigo.Feeds.Entities.Abstract;

namespace IndigoToGoogleTaxonomyMapping.Model
{
    internal class IndigoCategoryWrapper
    {
        public IndigoCategoryWrapper(IIndigoCategory indigoCategory)
        {
            IsModified = false;
            IsFoundInDatabase = false;
            IndigoCategory = indigoCategory;
        }

        public bool IsModified
        {
            get;
            set;
        }

        public bool IsFoundInDatabase
        {
            get;
            set;
        }

        public IIndigoCategory IndigoCategory
        {
            get;
            protected set;
        }
    }
}
