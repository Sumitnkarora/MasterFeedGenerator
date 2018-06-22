using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Castle.Core.Logging;
using Endeca.Navigation;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Entities.Concrete;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Utils;
using IndigoFeedSystemDataProcessor.Services.Contract;
using IndigoFeedSystemDataProcessor.Utils;

namespace IndigoFeedSystemDataProcessor
{
    /// <summary>
    /// Quick code (open to some refactoring) to process the category tree from Endeca and update IndigoCategories accordingly.
    /// Later on we decided to add brand processing from Endeca as well as CMS product list id processing into this application. 
    /// </summary>
    public class Builder : IBuilder
    {
        private readonly IIndigoCategoryService _indigoCategoryService;
        private readonly IIndigoBrandService _indigoBrandService;
        private readonly IFeedCmsProductArchiveEntryService _feedCmsProductArciveEntryService;
        private readonly IDefaultRecosGeneratorService _defaultRecosGeneratorService;

        private static readonly long EndecaSectionId = ParameterUtils.GetParameter<long>("EndecaSectionId");
        private static readonly long EndecaCategoryId = ParameterUtils.GetParameter<long>("EndecaCategoryId");
        private static readonly long EndecaBrandEnId = ParameterUtils.GetParameter<long>("EndecaBrandEnId");
        private static readonly int TopCategoryId = ParameterUtils.GetParameter<int>("TopCategoryId");
        private static readonly string BreadcrumbTrailSplitter = ParameterUtils.GetParameter<string>("BreadcrumbTrailSplitter");
        private static readonly string IdTrailSplitter = ParameterUtils.GetParameter<string>("IdTrailSplitter");
        private List<IIndigoCategory> _existingIndigoCategories = new List<IIndigoCategory>();
        private const string ModifiedByValue = "CategoryProcessor";
        private int _newItemCount;
        private int _unmodifiedItemCount;
        private int _modifiedItemCount;
        private int _deletedItemCount;
        private int _newItemCountDelta;
        private int _unmodifiedItemCountDelta;
        private int _modifiedItemCountDelta;
        private int _deletedItemCountDelta; 

        public ILogger Log { get; set; }
        public Builder(IIndigoCategoryService indigoCategoryService, IIndigoBrandService indigoBrandService, IFeedCmsProductArchiveEntryService feedCmsProductArciveEntryService, IDefaultRecosGeneratorService defaultRecosGeneratorService)
        {
            _indigoCategoryService = indigoCategoryService;
            _indigoBrandService = indigoBrandService;
            _feedCmsProductArciveEntryService = feedCmsProductArciveEntryService;
            _defaultRecosGeneratorService = defaultRecosGeneratorService;
        }

        public void Build(string[] args)
        {
            var startTime = DateTime.Now;
            Log.Info("Execution started.");

            if (ParameterUtils.GetParameter<bool>("EnableCategoryUpdates"))
            {
                try
                {
                    DoCategoryUpdates();
                }
                catch (Exception ex)
                {
                    Log.Error("An error occurred during processing of categories.", ex);
                }
            }

            if (ParameterUtils.GetParameter<bool>("EnableBrandUpdates"))
            {
                try
                {
                    DoBrandUpdates();
                }
                catch (Exception ex)
                {
                    Log.Error("An error occurred during processing of brands.", ex);
                }
            }

            if (ParameterUtils.GetParameter<bool>("EnableCmsProductListArchiving"))
            {
                try
                {
                    DoCmsProductListArchiving();
                }
                catch (Exception ex)
                {
                    Log.Error("An error occurred during processing of CMS Product Lists.", ex);
                }
            }

            if (ParameterUtils.GetParameter<bool>("EnableDefaultRecosUpdates"))
            {
                try
                {
                    DoDefaultRecosUpdates();
                }
                catch (Exception ex)
                {
                    Log.Error("An error occurred while generation default recommendations.", ex);
                }
            }

            var elapsedTime = DateTime.Now - startTime;
            Log.InfoFormat("Execution completed. Elapsed time: {0}", elapsedTime.ToString(@"dd\.hh\:mm\:ss"));
        }

        private void DoCategoryUpdates()
        {
            Log.Info("Starting working on category updates.");
            _existingIndigoCategories = _indigoCategoryService.GetAllIndigoCategories().ToList();
            // These values get used for log reporting purposes
            SetInitialCategoryTypeCounts();
            var topLevelBrowseSectionDimVals = GetTopLevelBrowseDimVals();
            foreach (var topLevelBrowseSection in topLevelBrowseSectionDimVals)
            {
                var match = _existingIndigoCategories.FirstOrDefault(ic => ic.EndecaBreadcrumbId.Equals(topLevelBrowseSection.Id.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase));
                bool hasChanged = false;
                if (match == null)
                {
                    var indigoCategory = GetIndigoCategory(null, topLevelBrowseSection.Name, topLevelBrowseSection.GetFrenchName(),
                        topLevelBrowseSection.Id.ToString(CultureInfo.InvariantCulture), (int)topLevelBrowseSection.Id,
                        topLevelBrowseSection.Name, false);
                    match = _indigoCategoryService.Insert(indigoCategory);
                    _newItemCountDelta++;
                }
                else
                {
                    // Check if the name has changed. If so, update this category and its entire branch as the breadcrumb values have changed
                    if (!(topLevelBrowseSection.Name).Equals(match.Name, StringComparison.OrdinalIgnoreCase)
                        && (topLevelBrowseSection.GetFrenchName().Equals(match.NameFr, StringComparison.OrdinalIgnoreCase)))
                    {
                        hasChanged = true;
                        match.Name = topLevelBrowseSection.Name;
                        match.NameFr = topLevelBrowseSection.GetFrenchName();
                        match.BreadcrumbPath = topLevelBrowseSection.Name;
                        match.IsModified = true;
                        match.IsDeleted = false;
                        match = _indigoCategoryService.Update(match);
                        _modifiedItemCountDelta++;
                    }
                    else if (match.IsDeleted)
                    {
                        match.IsDeleted = false;
                        match = _indigoCategoryService.Update(match);
                        _modifiedItemCountDelta++;
                    }
                    else
                    {
                        _unmodifiedItemCountDelta++;
                    }
                    _existingIndigoCategories.Remove(match);
                }


                var breadcrumbTrail = topLevelBrowseSection.Name;
                ProcessDimVals(topLevelBrowseSection, EndecaCategoryId, topLevelBrowseSection.Id.ToString(CultureInfo.InvariantCulture), breadcrumbTrail, true, match.IndigoCategoryId, hasChanged);
            }

            // At this point, the indigo categories that are left are the ones that have been "modified" (e.g. they aren't part of the current tree anymore)
            // Set their isModified flags to true
            foreach (var indigoCategory in _existingIndigoCategories)
            {
                if (indigoCategory.IsDeleted) continue;

                indigoCategory.IsModified = false;
                indigoCategory.IsDeleted = true;
                indigoCategory.ModifiedBy = ModifiedByValue;
                _indigoCategoryService.Update(indigoCategory);
                _deletedItemCountDelta++;
            }

            Log.InfoFormat("Completed working on categories.");
            Log.InfoFormat("Before the run, there were {0} new, {1} modified, {2} deleted and {3} unchanged categories in the database.", _newItemCount, _modifiedItemCount, _deletedItemCount, _unmodifiedItemCount);
            Log.InfoFormat("During the run, there were {0} newly added, {1} newly modified, {2} newly deleted categories out of the {3} categories retrieved from Endeca.", _newItemCountDelta, _modifiedItemCountDelta, _deletedItemCountDelta, _unmodifiedItemCountDelta);
        }

        private void DoBrandUpdates()
        {
            Log.Info("Starting to work on brands.");
            ResetCounters();

            // First get all the Indigo brands.
            var indigoBrands = _indigoBrandService.GetAllBrands().ToList();

            // Load all the brands from Endeca
            var brands = EndecaUtils.GetPageLinks(EndecaBrandEnId, false, true).ToList();

            if (brands.Any())
            {
                var time = DateTime.Now;
                var processedIds = new List<long>();
                foreach (var brand in brands)
                {
                    var id = brand.DimensionValue.Id;
                    if (processedIds.Contains(id))
                        continue;

                    processedIds.Add(id);
                    var name = EndecaUtils.GetLocalizedTitle(brand.DimensionValue, false);
                    var newBrand = new IndigoBrand { DateCreated = time, EndecaDimensionId = id, Name = name };
                    var match = indigoBrands.FirstOrDefault(i => i.EndecaDimensionId == id);
                    if (match == null)
                    {
                        _indigoBrandService.Insert(newBrand);
                        _newItemCount++;
                    }
                    else
                    {
                        if (match.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                            _unmodifiedItemCount++;
                        else
                        {
                            _indigoBrandService.Delete(match.IndigoBrandId);
                            _indigoBrandService.Insert(newBrand);
                            _modifiedItemCount++;
                        }
                        indigoBrands.Remove(match);
                    }
                }

                // If there are any items remaining in the Indigo list, then display a message and move on
                if (indigoBrands.Any())
                    Log.InfoFormat("There are {0} brands that weren't found in Endeca. Keeping those brands in the database and moving on.", indigoBrands.Count);
            }

            Log.InfoFormat("Completed working on brands. There were {0} new, {1} modified and {2} unchanged brands.", _newItemCount, _modifiedItemCount, _unmodifiedItemCount);
        }

        private void DoCmsProductListArchiving()
        {
            Log.Info("Starting to work on CMS product list id archiving.");
            ResetCounters();

            // First get all the CMS ids that have been used in Feed Management Tool
            _feedCmsProductArciveEntryService.ArchiveCmsProductLists(out _newItemCount, out _modifiedItemCount, out _unmodifiedItemCount);
            if (_newItemCount > 0 || _modifiedItemCount > 0 || _unmodifiedItemCount > 0)
            {
                Log.InfoFormat("Completed working on CMS Product List archiving. There were {0} new, {1} modified and {2} unchanged CMS Product Lists.", _newItemCount, _modifiedItemCount, _unmodifiedItemCount);
            }
            else
                Log.Info("There were no CMS ids used in the Feed Management system.");
        }

        private void DoDefaultRecosUpdates()
        {
            Log.Info("Starting to work on generating default recommendations.");

            _defaultRecosGeneratorService.Run();

            Log.Info("Completed working on generating default recommendations.");
        }

        private void SetInitialCategoryTypeCounts()
        {
            var newOrModifiedCategories = _indigoCategoryService.GetNewOrModifiedIndigoCategories().ToList();
            _newItemCount = newOrModifiedCategories.Count(ic => !ic.IsModified);
            _modifiedItemCount = newOrModifiedCategories.Count(ic => ic.IsModified);
            _deletedItemCount = _existingIndigoCategories.Count(ic => ic.IsDeleted);
            _unmodifiedItemCount = _existingIndigoCategories.Count() - _newItemCount - _modifiedItemCount - _deletedItemCount;
        }

        private IIndigoCategory GetIndigoCategory(int? parentCategoryId, string englishName, string frenchName, string endecaBreadcrumbId, int browseCategoryId, string breadcrumbPath, bool isModified)
        {
            return new IndigoCategory
            {
                ParentId = parentCategoryId,
                Name = englishName,
                NameFr = frenchName,
                EndecaBreadcrumbId = endecaBreadcrumbId,
                BrowseCategoryId = browseCategoryId,
                BreadcrumbPath = breadcrumbPath,
                IsModified = isModified,
                ModifiedBy = ModifiedByValue
            };
        }

        public IEnumerable<DimVal> GetTopLevelBrowseDimVals()
        {
            var result = new List<DimVal>();
            var usedDimensionIds = new List<long>();
            var sections =
                EndecaUtils.GetRefinements(new List<long> { 0, EndecaSectionId }, null, false, true)
                    .GetDimension(EndecaSectionId).Refinements;

            foreach (var sectionDimVal in sections.Cast<DimVal>())
            {
                // Replacing var dimensionValueLinks = GetDimensionValueLinks(828847, sectionDimVal, null, false);
                var sectionValue = sectionDimVal;
                var dimensionId = TopCategoryId;

                var dims = ProcessDimVals(sectionValue, dimensionId, string.Empty, string.Empty, false, null, false);
                foreach (var dimVal in dims)
                {
                    if (!usedDimensionIds.Contains(dimVal.Id))
                    {
                        result.Add(dimVal);
                        usedDimensionIds.Add(dimVal.Id);
                    }
                }
            }

            return result;
        }

        public IEnumerable<DimVal> ProcessDimVals(DimVal dimFirst, long dimensionId, string idTrail, string breadcrumbTrail, bool appendBreadcrumbTrail, int? indigoParentCategoryId, bool enforceUpdate, DimVal dimSecond = null, bool isFrench = false)
        {
            var result = new List<DimVal>();
            if (string.IsNullOrWhiteSpace(breadcrumbTrail))
                breadcrumbTrail = string.Empty;

            if (string.IsNullOrWhiteSpace(idTrail))
                idTrail = string.Empty;

            DimVal dimensionValue = dimSecond;
            var sectionValue = dimFirst;
            var dimValList = dimensionValue == null
                             ? new List<DimVal> { sectionValue }
                             : new List<DimVal> { sectionValue, dimensionValue };

            var dimension =
            EndecaUtils.GetRefinements(new List<long> { EndecaSectionId, dimensionId }, dimValList, false, false)
                .GetDimension(dimensionId);

            if (dimension != null && dimension.Refinements != null && dimension.Refinements.Count > 0)
            {
                var dimensionRefinements = dimension.Refinements;
                if (dimensionRefinements.Count > 0)
                {
                    var placeHolderBreadcrumbTrail = breadcrumbTrail;
                    var placeHolderIdTrail = idTrail;
                    foreach (var dimRef in dimensionRefinements)
                    {
                        var dimVal = (DimVal)dimRef;
                        idTrail += IdTrailSplitter + dimVal.Id;
                        breadcrumbTrail += BreadcrumbTrailSplitter + dimVal.Name;
                        IIndigoCategory match = null;
                        var hasChanged = enforceUpdate;
                        if (appendBreadcrumbTrail)
                        {
                            Log.Debug(idTrail + ":" + breadcrumbTrail);
                            match = _existingIndigoCategories.FirstOrDefault(ic => ic.EndecaBreadcrumbId.Equals(idTrail, StringComparison.OrdinalIgnoreCase));
                            if (match == null)
                            {
                                var indigoCategory = GetIndigoCategory(indigoParentCategoryId, dimVal.Name,dimVal.GetFrenchName(), idTrail, (int)dimVal.Id, breadcrumbTrail, false);
                                match = _indigoCategoryService.Insert(indigoCategory);
                                _newItemCountDelta++;
                            }
                            else
                            {
                                // Check if the name has changed. If so, update this category and its entire branch as the breadcrumb values have changed
                                if (hasChanged ||
                                    !(string.Equals(dimVal.Name, match.Name, StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(dimVal.GetFrenchName(), match.NameFr, StringComparison.OrdinalIgnoreCase)))
                                {
                                    hasChanged = true;
                                    match.Name = dimVal.Name;
                                    match.NameFr = dimVal.GetFrenchName();
                                    match.BreadcrumbPath = breadcrumbTrail;
                                    match.IsModified = true;
                                    match.IsDeleted = false;
                                    match = _indigoCategoryService.Update(match);
                                    _modifiedItemCountDelta++;
                                }
                                else if (match.IsDeleted)
                                {
                                    match.IsDeleted = false;
                                    match = _indigoCategoryService.Update(match);
                                    _modifiedItemCountDelta++;
                                }
                                else
                                {
                                    _unmodifiedItemCountDelta++;
                                }
                                _existingIndigoCategories.Remove(match);
                            }
                        }
                        
                        result.Add(dimVal);

                        var parentId = (appendBreadcrumbTrail) ? match.IndigoCategoryId : indigoParentCategoryId;

                        var linkInfoListNew = ProcessDimVals(sectionValue, dimensionId, idTrail, breadcrumbTrail, appendBreadcrumbTrail, parentId, hasChanged, dimVal, isFrench).ToList();
                        if (linkInfoListNew.Any())
                        {
                            result.AddRange(linkInfoListNew);
                        }
                        breadcrumbTrail = placeHolderBreadcrumbTrail;
                        idTrail = placeHolderIdTrail;
                    }
                }
            }

            return result;
        }

        private void ResetCounters()
        {
            _newItemCount = 0;
            _unmodifiedItemCount = 0;
            _modifiedItemCount = 0;
            _deletedItemCount = 0;
            _newItemCountDelta = 0;
            _unmodifiedItemCountDelta = 0;
            _modifiedItemCountDelta = 0;
            _deletedItemCountDelta = 0;
    }
    }
}