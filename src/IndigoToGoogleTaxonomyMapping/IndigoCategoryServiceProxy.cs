//#define TEST

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Entities.Abstract;

namespace IndigoToGoogleTaxonomyMapping
{
    internal class IndigoCategoryServiceProxy
    {
        private IIndigoCategoryService _indigoCategoryService;

        public IndigoCategoryServiceProxy(IIndigoCategoryService indigoCategoryService)
        {
            _indigoCategoryService = indigoCategoryService;
        }

        public IEnumerable<IIndigoCategory> GetAllIndigoCategories()
        {
            return _indigoCategoryService.GetAllIndigoCategories();
        }

        public IIndigoCategory Update(IIndigoCategory indigoCategory)
        {
            indigoCategory.ModifiedBy = System.Configuration.ConfigurationManager.AppSettings["TaxonomyMapper_ModifiedBy"];
#if !TEST
            return _indigoCategoryService.Update(indigoCategory);
#else
            return indigoCategory;
#endif
        }
    }
}
