using Castle.Core.Logging;
using FeedGenerators.Core.Services.Abstract;
using FeedGenerators.Core.Services.Concrete;
using FeedGenerators.Core.Types;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Models.Abstract;
using Indigo.Feeds.Services.Concrete;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FeedGenerators.Core.Execution
{
    public class GooglePlaFeedRuleHelper : IGooglePlaFeedRuleHelper
    {
        private readonly bool _allowRuleOptimizations;
        private readonly bool _allowRuleEntryRemovals;
        private readonly bool _allowIEnumerableRuleEvaluations;
        private readonly ILogger _log;
        private readonly IFeedRuleService _feedRuleService;
        private readonly IFeedGeneratorIndigoCategoryService _feedGeneratorIndigoCategoryService;
        private readonly IFeedCmsProductArchiveEntryService _feedCmsProductArchiveEntryService;
        private readonly List<IFeedRuleModel> _exclusionRules = new List<IFeedRuleModel>();
        private readonly List<IFeedRuleModel> _exclusionRulesPast = new List<IFeedRuleModel>();


        private GoogleRunFeedType _runFeedType;
        private bool _isIncrementalRun;
        private DateTime? _fromTime;
        private IFeedGeneratorCmsDataService _feedGeneratorCmsDataService;
        private IFeedGeneratorCmsDataService _feedGeneratorCmsDataServicePast;
        private IRuleEvaluatorService _exclusionRuleEvaluatorService;
        private IRuleEvaluatorService _exclusionRuleEvaluatorServicePast;
        private IRuleEvaluatorService _cpcRuleEvaluatorService;
        private IRuleEvaluatorService _customLabel0Service;
        private IRuleEvaluatorService _customLabel1Service;
        private IRuleEvaluatorService _customLabel2Service;
        private IRuleEvaluatorService _customLabel3Service;
        private IRuleEvaluatorService _customLabel4Service;
        private IRuleEvaluatorService _dynamicMerchLabelService;
        private bool _haveExclusionRulesChanged;

        public GooglePlaFeedRuleHelper(IFeedRuleService feedRuleService, IFeedGeneratorIndigoCategoryService feedGeneratorIndigoCategoryService, IFeedCmsProductArchiveEntryService feedCmsProductArchiveEntryService, ILogger log, bool allowRuleOptimizations, bool allowRuleEntryRemovals, bool allowIEnumerableRuleEvaluations)
        {
            _feedRuleService = feedRuleService;
            _feedGeneratorIndigoCategoryService = feedGeneratorIndigoCategoryService;
            _feedCmsProductArchiveEntryService = feedCmsProductArchiveEntryService;
            _log = log;
            _allowRuleOptimizations = allowRuleOptimizations;
            _allowRuleEntryRemovals = allowRuleEntryRemovals;
            _allowIEnumerableRuleEvaluations = allowIEnumerableRuleEvaluations;
        }

        public void Initialize(int feedId, bool isIncremental, DateTime? fromTime, DateTime executionTime, GoogleRunFeedType runFeedType)
        {
            _log.Debug("Inside Initialize() of GooglePlaFeedRuleHelper.");
            _isIncrementalRun = isIncremental;
            _fromTime = fromTime;
            _runFeedType = runFeedType;

            // First get rules associated with this feed
            var rules = _feedRuleService.GetFeedRuleModels(feedId, executionTime).ToList();
            _exclusionRules.AddRange(rules.Where(r => r.FeedRule.FeedRuleType == FeedRuleType.Exclusion));
            var cpcRuleModels = rules.Where(frm => frm.FeedRule.FeedRuleType == FeedRuleType.CPC_Value).ToList();
            var customLabel0RuleModels = rules.Where(frm => frm.FeedRule.FeedRuleType == FeedRuleType.Custom_Label_0).ToList();
            var customLabel1RuleModels = rules.Where(frm => frm.FeedRule.FeedRuleType == FeedRuleType.Custom_Label_1).ToList();
            var customLabel2RuleModels = rules.Where(frm => frm.FeedRule.FeedRuleType == FeedRuleType.Custom_Label_2).ToList();
            var customLabel3RuleModels = rules.Where(frm => frm.FeedRule.FeedRuleType == FeedRuleType.Custom_Label_3).ToList();
            var customLabel4RuleModels = rules.Where(frm => frm.FeedRule.FeedRuleType == FeedRuleType.Custom_Label_4).ToList();
            var dynamicMerchLabelRuleModels = rules.Where(frm => frm.FeedRule.FeedRuleType == FeedRuleType.Dynamic_Merch_Label).ToList();
            // Instantiate the IFeedGeneratorCmsDataService
            _feedGeneratorCmsDataService = new FeedGeneratorCmsDataService(_feedCmsProductArchiveEntryService, null);
            // Instantiate the rule evaluator services
            _exclusionRuleEvaluatorService = new RuleEvaluatorService(_exclusionRules, _feedGeneratorCmsDataService, _feedGeneratorIndigoCategoryService, _allowRuleOptimizations, _allowRuleEntryRemovals, _allowIEnumerableRuleEvaluations);
            _cpcRuleEvaluatorService = new RuleEvaluatorService(cpcRuleModels, _feedGeneratorCmsDataService, _feedGeneratorIndigoCategoryService, _allowRuleOptimizations, _allowRuleEntryRemovals, _allowIEnumerableRuleEvaluations);
            _customLabel0Service = new RuleEvaluatorService(customLabel0RuleModels, _feedGeneratorCmsDataService, _feedGeneratorIndigoCategoryService, _allowRuleOptimizations, _allowRuleEntryRemovals, _allowIEnumerableRuleEvaluations);
            _customLabel1Service = new RuleEvaluatorService(customLabel1RuleModels, _feedGeneratorCmsDataService, _feedGeneratorIndigoCategoryService, _allowRuleOptimizations, _allowRuleEntryRemovals, _allowIEnumerableRuleEvaluations);
            _customLabel2Service = new RuleEvaluatorService(customLabel2RuleModels, _feedGeneratorCmsDataService, _feedGeneratorIndigoCategoryService, _allowRuleOptimizations, _allowRuleEntryRemovals, _allowIEnumerableRuleEvaluations);
            _customLabel3Service = new RuleEvaluatorService(customLabel3RuleModels, _feedGeneratorCmsDataService, _feedGeneratorIndigoCategoryService, _allowRuleOptimizations, _allowRuleEntryRemovals, _allowIEnumerableRuleEvaluations);
            _customLabel4Service = new RuleEvaluatorService(customLabel4RuleModels, _feedGeneratorCmsDataService, _feedGeneratorIndigoCategoryService, _allowRuleOptimizations, _allowRuleEntryRemovals, _allowIEnumerableRuleEvaluations);
            _dynamicMerchLabelService = new RuleEvaluatorService(dynamicMerchLabelRuleModels, _feedGeneratorCmsDataService, _feedGeneratorIndigoCategoryService, _allowRuleOptimizations, _allowRuleEntryRemovals, _allowIEnumerableRuleEvaluations);
            
            // If it's an incremental run, get the rule set at the time of of the previous execution
            // Then make comparisons to check if there were updates to rules. If there were rule updates, 
            // then populate the secondary rule services which will be used to determine if a product is modified
            // even if the product data hasn't changed
            if (!_isIncrementalRun || !_fromTime.HasValue)
            {
                _log.Debug("Exiting Initialize() of GooglePlaFeedRuleHelper without initializing past rules.");
                return;
            }

            var previousRules = _feedRuleService.GetFeedRuleModels(feedId, _fromTime.Value).ToList();
            _exclusionRulesPast.AddRange(previousRules.Where(r => r.FeedRule.FeedRuleType == FeedRuleType.Exclusion));
            _feedGeneratorCmsDataServicePast = new FeedGeneratorCmsDataService(_feedCmsProductArchiveEntryService, fromTime);
            _exclusionRuleEvaluatorServicePast = new RuleEvaluatorService(_exclusionRulesPast, _feedGeneratorCmsDataServicePast, _feedGeneratorIndigoCategoryService, _allowRuleOptimizations, _allowRuleEntryRemovals, _allowIEnumerableRuleEvaluations);
            _haveExclusionRulesChanged = !AreSameRules(new RuleComparisonData { RuleSet = _exclusionRules, FeedGeneratorCmsDataService = _feedGeneratorCmsDataService },
                new RuleComparisonData { RuleSet = _exclusionRulesPast, FeedGeneratorCmsDataService = _feedGeneratorCmsDataServicePast });

            _log.DebugFormat("Exiting Initialize() of GooglePlaFeedRuleHelper after initializing past rules. HaveExclusionRulesChanged is {0}.", _haveExclusionRulesChanged);
        }

        public bool IsExcludedFromFeed(IProductData productData, bool processOldRule)
        {
            return (processOldRule) ? _exclusionRuleEvaluatorServicePast.GetEvaluationResult(productData, FeedRuleType.Exclusion).HasMatch
                : _exclusionRuleEvaluatorService.GetEvaluationResult(productData, FeedRuleType.Exclusion).HasMatch;
        }

        public bool HaveExclusionRulesChanged()
        {
            return _haveExclusionRulesChanged;
        }

        public GoogleRunFeedType GetRunFeedType()
        {
            return _runFeedType;
        }

        public RuleMatchData GetCpcValue(IProductData productData)
        {
            RuleMatchData result = null;
            var ruleResult = _cpcRuleEvaluatorService.GetEvaluationResult(productData, FeedRuleType.CPC_Value);
            if (ruleResult.HasMatch)
                result = new RuleMatchData { IsDefaultMatch = ruleResult.IsDefaultMatch, Value = ruleResult.MatchingRulePayLoads.First().Replace("$", string.Empty) };

            return result;
        }

        public RuleMatchData GetCustomLabelValue(IProductData productData, FeedRuleType feedRuleType)
        {
            RuleMatchData result = null;
            IRuleEvaluationResult ruleResult = null;
            switch (feedRuleType)
            {
                case (FeedRuleType.Custom_Label_0):
                    {
                        ruleResult = _customLabel0Service.GetEvaluationResult(productData, feedRuleType);
                        break;
                    }
                case (FeedRuleType.Custom_Label_1):
                    {
                        ruleResult = _customLabel1Service.GetEvaluationResult(productData, feedRuleType);
                        break;
                    }
                case (FeedRuleType.Custom_Label_2):
                    {
                        ruleResult = _customLabel2Service.GetEvaluationResult(productData, feedRuleType);
                        break;
                    }
                case (FeedRuleType.Custom_Label_3):
                    {
                        ruleResult = _customLabel3Service.GetEvaluationResult(productData, feedRuleType);
                        break;
                    }
                case (FeedRuleType.Custom_Label_4):
                    {
                        ruleResult = _customLabel4Service.GetEvaluationResult(productData, feedRuleType);
                        break;
                    }
            }
            if (ruleResult != null && ruleResult.HasMatch)
                result = new RuleMatchData { IsDefaultMatch = ruleResult.IsDefaultMatch, Value = ruleResult.MatchingRulePayLoads.First() };

            return result;
        }

        public RuleMatchData GetDynamicMerchLabelValue(IProductData productData)
        {
            RuleMatchData result = null;
            var ruleResult = _dynamicMerchLabelService.GetEvaluationResult(productData, FeedRuleType.Dynamic_Merch_Label);
            if (ruleResult.HasMatch)
                result = new RuleMatchData { IsDefaultMatch = ruleResult.IsDefaultMatch, Value = ruleResult.MatchingRulePayLoads.First() };

            return result;
        }

        private static bool AreSameRules(RuleComparisonData ruleCriteria1, RuleComparisonData ruleCriteria2)
        {
            var ruleSet1 = ruleCriteria1.RuleSet;
            var ruleSet2 = ruleCriteria2.RuleSet;

            // Do the easy and quick checks first
            if (ruleSet1 == null && ruleSet2 == null)
                return true;

            if (ruleSet1 == null || ruleSet2 == null || ruleSet1.Count != ruleSet2.Count)
                return false;

            ruleSet1 = ruleSet1.OrderBy(frm => frm.FeedRule.Ordinal).ToList();
            ruleSet2 = ruleSet2.OrderBy(frm => frm.FeedRule.Ordinal).ToList();
            for (var ii = 0; ii < ruleSet1.Count; ii++)
            {
                var rule1 = ruleSet1[ii];
                var rule2 = ruleSet2[ii];

                if (rule1.FeedRule.FeedRuleId != rule2.FeedRule.FeedRuleId)
                    return false;

                if (rule1.FeedRule.IsDefault)
                    continue;

                // Even if the rules are the same, it's possible that the content of the CMS product lists changed
                // So do a check on all applicable CMS product list ids
                foreach (var entries in rule1.FeedRuleEntryGroupings.Select(g => g.FeedRuleEntries))
                {
                    foreach (var entry in entries)
                    {
                        // Note that Permanent CMS PList Id rule entry type is only used for Dynamic Campaigns Feed Generator, otherwise
                        // this check should have included it too. 
                        if (entry.FeedRuleEntryType != FeedRuleEntryType.CMS_Product_List_Id) continue;

                        var listId = long.Parse(entry.Payload);
                        var skus = ruleCriteria1.FeedGeneratorCmsDataService.GetSkusForProductListId(listId);
                        var skusPast = ruleCriteria2.FeedGeneratorCmsDataService.GetSkusForProductListId(listId);

                        if (skus == null && skusPast == null)
                            continue;

                        if (skus == null || skusPast == null)
                            return false;

                        var enumerable = skus as IList<string> ?? skus.ToList();
                        var intersection = enumerable.Intersect(skusPast);
                        if (intersection.Count() != enumerable.Count())
                            return false;
                    }
                }
            }

            return true;
        }

        internal struct RuleComparisonData
        {
            public List<IFeedRuleModel> RuleSet { get; set; }
            public IFeedGeneratorCmsDataService FeedGeneratorCmsDataService { get; set; }
        }
    }
}
