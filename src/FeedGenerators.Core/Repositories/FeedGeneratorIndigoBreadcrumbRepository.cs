using Castle.Core.Logging;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Models.Abstract;
using Indigo.Feeds.Models.Concrete;
using Indigo.Feeds.Repositories.Abstract;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Types;
using Indigo.Feeds.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FeedGenerators.Core.Repositories
{
    internal class FeedGeneratorIndigoBreadcrumbRepository : IIndigoBreadcrumbRepository
    {
        private readonly IndigoBreadcrumbDataRetriever _indigoBreadcrumbDataRetriever;
        private ILogger _log;

        public FeedGeneratorIndigoBreadcrumbRepository(IIndigoCategoryService indigoCategoryService, IGoogleCategoryService googleCategoryService, ILogger log)
        {
            _indigoBreadcrumbDataRetriever = IndigoBreadcrumbDataRetriever.GetInstance(indigoCategoryService, googleCategoryService, log);
            _log = log;
        }

        public IIndigoBreadcrumbCategory GetTopLevelIndigoCategory(FeedSectionType feedSectionType, string recordType = null)
        {
            return _indigoBreadcrumbDataRetriever.GetTopLevelIndigoCategory(feedSectionType, recordType);
        }

        public IEnumerable<IIndigoBreadcrumbCategory> GetIndigoBreadcrumbCategoriesByBrowseCategoryId(int browseCategoryId)
        {
            return _indigoBreadcrumbDataRetriever.GetFeedGeneratorIndigoCategoriesByBrowseCategoryId(browseCategoryId);
        }

        public IIndigoCategory GetIndigoCategory(int indigoCategoryId)
        {
            return _indigoBreadcrumbDataRetriever[indigoCategoryId];
        }

        private class IndigoBreadcrumbDataRetriever
        {
            private static volatile IndigoBreadcrumbDataRetriever _instance;
            private static ILogger _log;
            private static readonly object SyncRoot = new Object();
            private static ConcurrentDictionary<int, List<IIndigoBreadcrumbCategory>> _indigoCategoriesByBrowseCategoryId;
            private static ConcurrentDictionary<int, IIndigoBreadcrumbCategory> _indigoCategoriesByIndigoCategoryId;
            private static ConcurrentDictionary<int, IIndigoBreadcrumbCategory> _topLevelIndigoCategories;
            //private static ConcurrentDictionary<string, List<FeedGeneratorIndigoCategory>> _entertainmentIndigoCategoriesByL2Name;
            private static List<int> _excludedBrowseCategoryIds;
            private static IIndigoCategoryService _indigoCategoryService;
            private static IGoogleCategoryService _googleCategoryService;

            private static readonly string BreadcrumbTrailSplitter = ParameterUtils.GetParameter<string>("BreadcrumbTrailSplitter");
            private static readonly string BrowseCategoriesEndecaXmlFileName = ParameterUtils.GetParameter<string>("BrowseCategoriesEndecaXmlFileName");
            private static readonly int EndecaRootCategoryDimensionId = ParameterUtils.GetParameter<int>("EndecaRootCategoryDimensionId");
            private static readonly bool AllowDeletedCategories = ParameterUtils.GetParameter<bool>("AllowDeletedCategories");

            private IndigoBreadcrumbDataRetriever(IIndigoCategoryService indigoCategoryService, IGoogleCategoryService googleCategoryService, ILogger log)
            {
                _indigoCategoriesByBrowseCategoryId = new ConcurrentDictionary<int, List<IIndigoBreadcrumbCategory>>();
                _indigoCategoriesByIndigoCategoryId = new ConcurrentDictionary<int, IIndigoBreadcrumbCategory>();
                _topLevelIndigoCategories = new ConcurrentDictionary<int, IIndigoBreadcrumbCategory>();
                _excludedBrowseCategoryIds = new List<int>();
                _indigoCategoryService = indigoCategoryService;
                _googleCategoryService = googleCategoryService;
                _log = log;

                LoadData();
            }

            public static IndigoBreadcrumbDataRetriever GetInstance(IIndigoCategoryService indigoCategoryService, IGoogleCategoryService googleCategoryService, ILogger log)
            {
                if (_instance != null) return _instance;
                lock (SyncRoot)
                {
                    if (_instance == null)
                        _instance = new IndigoBreadcrumbDataRetriever(indigoCategoryService, googleCategoryService, log);
                }

                return _instance;
            }

            private void LoadData()
            {
                var indigoCategories = _indigoCategoryService.GetAllIndigoCategories().OrderBy(ic => ic.ParentId.HasValue).ToList();
                var googleCategories = _googleCategoryService.GetAllGoogleCategories().ToList();
                _excludedBrowseCategoryIds = GetExcludedBrowseCategoryIds();

                // Loop through all Indigo categories and create the list of "composite" models which also contains Google info
                // and add them to the Indigo categories dictionary
                foreach (var indigoCategory in indigoCategories.Where(ic => AllowDeletedCategories || !ic.IsDeleted))
                {
                    IGoogleCategory googleCategory = null;
                    if (indigoCategory.GoogleCategoryId.HasValue)
                    {
                        googleCategory = googleCategories.SingleOrDefault(gc => gc.GoogleCategoryId == indigoCategory.GoogleCategoryId.Value);
                    }
                    var compositeModel = new IndigoBreadcrumbCategory(indigoCategory, googleCategory);
                    // Set the parent level category id on the composite model
                    if (compositeModel.ParentId.HasValue)
                    {
                        var crumbs = compositeModel.Breadcrumb.Split(new[] { BreadcrumbTrailSplitter }, StringSplitOptions.RemoveEmptyEntries);
                        var parentId = compositeModel.ParentId;
                        var id = 0;
                        while (parentId.HasValue)
                        {
                            var parent = indigoCategories.First(ic => ic.IndigoCategoryId == parentId.Value);
                            parentId = parent.ParentId;
                            id = parent.IndigoCategoryId;
                        }
                        compositeModel.ParentLevelIndigoCategoryId = id;
                        compositeModel.Crumbs = crumbs;
                    }
                    else
                    {
                        compositeModel.ParentLevelIndigoCategoryId = compositeModel.IndigoCategoryId;
                        compositeModel.Crumbs = new[] {compositeModel.Breadcrumb};
                    }

                    _indigoCategoriesByIndigoCategoryId.AddOrUpdate(compositeModel.IndigoCategoryId, compositeModel, (i, model) => model);

                    if (!compositeModel.ParentId.HasValue)
                        _topLevelIndigoCategories.AddOrUpdate(compositeModel.IndigoCategoryId, compositeModel, (i, category) => category);

                }

                // Now that the main Indigo categories dictionary is populated, now populate the supporting cast...
                _indigoCategoriesByBrowseCategoryId = new ConcurrentDictionary<int, List<IIndigoBreadcrumbCategory>>(_indigoCategoriesByIndigoCategoryId.Values.GroupBy(ic => ic.BrowseCategoryId).ToDictionary(kvp => kvp.Key, kvp => kvp.ToList()));
                //_entertainmentIndigoCategoriesByL2Name = new ConcurrentDictionary<string, List<FeedGeneratorIndigoCategory>>(entertainmentCategoryList.GroupBy(ic => ic.Crumbs[1]).ToDictionary(kvp => kvp.Key.ToLowerInvariant(), kvp => kvp.ToList()));
            }

            private List<int> GetExcludedBrowseCategoryIds()
            {
                return IndigoBreadcrumbRepositoryUtils.GetExcludedBrowseCategoryIds();
            }

            public IIndigoBreadcrumbCategory this[int indigoCategoryId]
            {
                get
                {
                    return _indigoCategoriesByIndigoCategoryId[indigoCategoryId];
                }
            }

            public IEnumerable<IIndigoBreadcrumbCategory> GetFeedGeneratorIndigoCategoriesByBrowseCategoryId(int browseCategoryId)
            {
                try
                {
                    var result = _excludedBrowseCategoryIds.Contains(browseCategoryId) ? null : _indigoCategoriesByBrowseCategoryId[browseCategoryId];
                    return result;
                }
                catch (Exception)
                {
                    _log.ErrorFormat("Browsecategoryid of {0} was missing in Indigo categories table", browseCategoryId);
                    throw;
                }
            }

            public IIndigoBreadcrumbCategory GetTopLevelIndigoCategory(FeedSectionType feedSectionType, string recordType = null)
            {
                return IndigoBreadcrumbRepositoryUtils.GetTopLevelIndigoCategory(feedSectionType, _topLevelIndigoCategories, recordType);
            }
        }
    }
}
