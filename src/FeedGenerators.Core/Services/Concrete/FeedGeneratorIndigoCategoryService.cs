using Castle.Core.Logging;
using FeedGenerators.Core.Repositories;
using FeedGenerators.Core.Services.Abstract;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Models.Abstract;
using Indigo.Feeds.Services.Concrete;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Types;
using System.Collections.Generic;

namespace FeedGenerators.Core.Services.Concrete
{
    public class FeedGeneratorIndigoCategoryService : IFeedGeneratorIndigoCategoryService
    {
        private readonly FeedGeneratorIndigoBreadcrumbRepository _feedGeneratorIndigoBreadcrumbRepository;
        private readonly IIndigoBreadcrumbService _indigoBreadcrumbService;
        private readonly IIndigoCategoryService _indigoCategoryService;
        private ILogger _log;

        public FeedGeneratorIndigoCategoryService(IIndigoCategoryService indigoCategoryService, IGoogleCategoryService googleCategoryService, ILogger log)
        {
            _log = log;
            _feedGeneratorIndigoBreadcrumbRepository = new FeedGeneratorIndigoBreadcrumbRepository(indigoCategoryService, googleCategoryService, log);
            _indigoBreadcrumbService = new IndigoBreadcrumbService(_feedGeneratorIndigoBreadcrumbRepository);
            _indigoCategoryService = indigoCategoryService;
        }

        public IIndigoBreadcrumbCategory GetDefaultIndigoBreadcrumb(FeedSectionType feedSectionType, List<int> browseCategoryIds, string recordType = null)
        {
            return _indigoBreadcrumbService.GetDefaultIndigoBreadcrumb(feedSectionType, browseCategoryIds, recordType);
        }

        public IEnumerable<IIndigoBreadcrumbCategory> GetIndigoBreadcrumbCategories(int browseCategoryId)
        {
            return _indigoBreadcrumbService.GetIndigoBreadcrumbCategories(browseCategoryId);
        }

        public IEnumerable<IIndigoCategory> GetIndigoCategories(int browseCategoryId)
        {
            return _feedGeneratorIndigoBreadcrumbRepository.GetIndigoBreadcrumbCategoriesByBrowseCategoryId(browseCategoryId);
        }

        public IIndigoCategory GetIndigoCategory(int indigoCategoryId)
        {
            return _feedGeneratorIndigoBreadcrumbRepository.GetIndigoCategory(indigoCategoryId);
        }

        public IEnumerable<IIndigoCategory> GetAllIndigoCategories()
        {
            return _indigoCategoryService.GetAllIndigoCategories();
        }
    }
}
