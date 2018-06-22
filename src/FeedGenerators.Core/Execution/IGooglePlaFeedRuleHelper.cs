using FeedGenerators.Core.Types;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Types;
using System;

namespace FeedGenerators.Core.Execution
{
    public interface IGooglePlaFeedRuleHelper
    {
        void Initialize(int feedId, bool isIncremental, DateTime? fromTime, DateTime executionTime, GoogleRunFeedType runFeedType);
        
        bool HaveExclusionRulesChanged();

        GoogleRunFeedType GetRunFeedType();

        bool IsExcludedFromFeed(IProductData productData, bool processOldRule);
        RuleMatchData GetCpcValue(IProductData productData);
        RuleMatchData GetCustomLabelValue(IProductData productData, FeedRuleType feedRuleType);
        RuleMatchData GetDynamicMerchLabelValue(IProductData productData);
    }
}
