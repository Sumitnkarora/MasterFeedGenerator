using FeedGenerators.Core.Services.Abstract;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Models.Abstract;
using Indigo.Feeds.Services.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FeedGenerators.Core.Services.Concrete
{
    /// <summary>
    /// This class is to add a layer to "cache" the cms data results in memory for the feed generators
    /// </summary>

    public class FeedGeneratorCmsDataService : IFeedGeneratorCmsDataService
    {
        private readonly DateTime? _fromTime;
        private readonly IFeedCmsProductArchiveEntryService _feedCmsProductArchiveEntryService;
        private static ConcurrentDictionary<long, IEnumerable<string>> _productListSkus;
        private static ConcurrentDictionary<PermanentCmsProductListIdCacheKey, IEnumerable<string>> _permanentCmsProductListSkus;

        public class PermanentCmsProductListIdCacheKey : IEquatable<PermanentCmsProductListIdCacheKey>
        {
            public readonly long ProductListId;
            public readonly DateTime FromTime;

            public bool Equals(PermanentCmsProductListIdCacheKey other)
            {
                if (ReferenceEquals(this, other))
                    return true;

                if (ReferenceEquals(other, null))
                    return false;

                return ProductListId == other.ProductListId && FromTime == other.FromTime;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as PermanentCmsProductListIdCacheKey);
            }

            public override int GetHashCode()
            {
                var val = (int)ProductListId;
                return val ^ FromTime.GetHashCode();
            }

            public PermanentCmsProductListIdCacheKey(long productListId, DateTime time)
            {
                ProductListId = productListId;
                FromTime = time; 
            }
        }

        public FeedGeneratorCmsDataService(IFeedCmsProductArchiveEntryService feedCmsProductArciveEntryService, DateTime? fromTime)
        {
            _feedCmsProductArchiveEntryService = feedCmsProductArciveEntryService;
            _fromTime = fromTime;
            _productListSkus = new ConcurrentDictionary<long, IEnumerable<string>>();
            _permanentCmsProductListSkus = new ConcurrentDictionary<PermanentCmsProductListIdCacheKey, IEnumerable<string>>();
        }

        public IEnumerable<string> GetSkusForProductListId(long productListId)
        {
            IEnumerable<string> result;
            if (_productListSkus.TryGetValue(productListId, out result))
                return result;

            result = new List<string>();
            var archiveEntry = (_fromTime.HasValue) ? _feedCmsProductArchiveEntryService.GetCmsProductListArchiveEntry(productListId, _fromTime.Value) 
                : _feedCmsProductArchiveEntryService.GetLatest(productListId);
            
            if (archiveEntry != null)
                result = archiveEntry.SkusList;

            var addValue = result as IList<string> ?? result.ToList();
            _productListSkus.AddOrUpdate(productListId, addValue, (l, enumerable) =>
            {
                var enumerable1 = enumerable as string[] ?? enumerable.ToArray();
                return enumerable1;
            });
            return addValue;
        }

        #region Permanent CMS Product List Id-related calls

        public IEnumerable<string> GetSkusForPermanentProductListId(long productListId, DateTime fromTime)
        {
            IEnumerable<string> result;
            var key = new PermanentCmsProductListIdCacheKey(productListId, fromTime);

            if (_permanentCmsProductListSkus.TryGetValue(key, out result))
                return result;

            result = _feedCmsProductArchiveEntryService.GetSkusForPermanentProductListId(productListId, fromTime);
            var addValue = result as IList<string> ?? result.ToList();
            _permanentCmsProductListSkus.AddOrUpdate(key, addValue, (l, enumerable) =>
            {
                var enumerable1 = enumerable as IList<string> ?? enumerable.ToList();
                return enumerable1;
            });

            return addValue;
        }
        #endregion

        #region Methods not used within Feed Generators
        // No need to implement
        public IFeedCmsProductListArchiveEntry GetLatest(long cmsProductListId)
        {
            throw new NotImplementedException();
        }

        // No need to implement
        public IFeedCmsProductListArchiveEntry GetCmsProductListArchiveEntry(long cmsProductListId, DateTime time)
        {
            throw new NotImplementedException();
        }

        // No need to implement
        public IFeedCmsProductListArchiveEntry Insert(IFeedCmsProductListArchiveEntry feedCmsProductListArchiveEntry)
        {
            throw new NotImplementedException();
        }

        // No need to implement
        public void ArchiveCmsProductLists(out int newItemCount, out int modifiedItemCount, out int unmodifiedItemCount)
        {
            throw new NotImplementedException();
        }

        // No need to implement
        public Dictionary<long, IList<string>> GetPermanentCmsProductListBreakdown(int feedRuleGroupingId, int numberOfCmsLists = 20, int pageNumber = 1, int startingPointOffset = 0)
        {
            throw new NotImplementedException();
        }

        // No need to implement
        public IEnumerable<string> GetArchivedSkusForPermanentProductListId(long productListId, DateTime fromTime)
        {
            throw new NotImplementedException();
        }

        // No need to implement
        public IPermanentCmsProductListArchivedSkusRequestModel GetPermanentCmsProductListArchivedSkusBreakdown(int feedRuleGroupingId, int numberOfCmsLists = 20, int pageNumber = 1, int startingPointOffset = 0, bool excludeListsWithNoArchivedSkus = true)
        {
            throw new NotImplementedException();
        }
        #endregion

    }
}