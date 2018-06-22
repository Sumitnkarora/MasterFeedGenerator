using Castle.Core.Logging;
using FeedGenerators.Core.Services.Abstract;
using FeedGenerators.Core.Services.Concrete;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Models.Abstract;
using Indigo.Feeds.Services.Concrete;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CjFeedGenerator.Execution
{
    internal class RunnerHelper
    {
        private readonly IFeedRuleService _feedRuleService;
        private readonly IFeedGeneratorIndigoCategoryService _feedGeneratorIndigoCategoryService;
        private readonly IFeedCmsProductArchiveEntryService _feedCmsProductArciveEntryService;
        private readonly List<IFeedRuleModel> _zeroCommissionRules = new List<IFeedRuleModel>();
        private readonly List<IFeedRuleModel> _zeroCommissionRulesPast = new List<IFeedRuleModel>();
        private readonly List<IFeedRuleModel> _promotionalTextRules = new List<IFeedRuleModel>();
        private readonly List<IFeedRuleModel> _promotionalTextRulesPast = new List<IFeedRuleModel>();
        private readonly bool _allowRuleOptimizations;
        private readonly bool _allowRuleEntryRemovals;
        private readonly bool _allowIEnumerableRuleEvaluations;

        //private int _feedId;
        private bool _isIncrementalRun;
        private DateTime? _fromTime;
        private ILogger _log;
        private IFeedGeneratorCmsDataService _feedGeneratorCmsDataService;
        private IFeedGeneratorCmsDataService _feedGeneratorCmsDataServicePast;
        private IRuleEvaluatorService _zeroCommissionRuleEvaluatorService;
        private IRuleEvaluatorService _zeroCommissionRuleEvaluatorServicePast;
        private IRuleEvaluatorService _promotionalTextRuleEvaluatorService;
        private IRuleEvaluatorService _promotionalTextRuleEvaluatorServicePast;

        public bool HaveRulesChanged { get; private set; }

        public RunnerHelper(IFeedRuleService feedRuleService, IFeedGeneratorIndigoCategoryService feedGeneratorIndigoCategoryService, IFeedCmsProductArchiveEntryService feedCmsProductArciveEntryService, ILogger log, bool allowRuleOptimizations, bool allowRuleEntryRemovals, bool allowIEnumerableRuleEvaluations)
        {
            _feedRuleService = feedRuleService;
            _feedGeneratorIndigoCategoryService = feedGeneratorIndigoCategoryService;
            _feedCmsProductArciveEntryService = feedCmsProductArciveEntryService;
            _log = log;
            _allowRuleOptimizations = allowRuleOptimizations;
            _allowRuleEntryRemovals = allowRuleEntryRemovals;
            _allowIEnumerableRuleEvaluations = allowIEnumerableRuleEvaluations;
        }

        public void Initialize(int feedId, bool isIncremental, DateTime? fromTime, DateTime executionTime)
        {
            //_feedId = feedId;
            _isIncrementalRun = isIncremental;
            _fromTime = fromTime;

            // First get rules associated with this feed
            var rules = _feedRuleService.GetFeedRuleModels(feedId, executionTime).ToList();
            _zeroCommissionRules.AddRange(rules.Where(r => r.FeedRule.FeedRuleType == FeedRuleType.No_Commission));
            _promotionalTextRules.AddRange(rules.Where(r => r.FeedRule.FeedRuleType == FeedRuleType.Promotional_Text));
            // Instantiate the IFeedGeneratorCmsDataService
            _feedGeneratorCmsDataService = new FeedGeneratorCmsDataService(_feedCmsProductArciveEntryService, null);
            // Instantiate the rule evaluator services
            _zeroCommissionRuleEvaluatorService = new RuleEvaluatorService(_zeroCommissionRules, _feedGeneratorCmsDataService, _feedGeneratorIndigoCategoryService, _allowRuleOptimizations, _allowRuleEntryRemovals, _allowIEnumerableRuleEvaluations);
            _promotionalTextRuleEvaluatorService = new RuleEvaluatorService(_promotionalTextRules, _feedGeneratorCmsDataService, _feedGeneratorIndigoCategoryService, _allowRuleOptimizations, _allowRuleEntryRemovals, _allowIEnumerableRuleEvaluations);
            // If it's an incremental run, get the rule set at the time of of the previous execution
            // Then make comparisons to check if there were updates to rules. If there were rule updates, 
            // then populate the secondary rule services which will be used to determine if a product is modified
            // even if the product data hasn't changed
            if (!_isIncrementalRun || !_fromTime.HasValue)
                return;

            var previousRules = _feedRuleService.GetFeedRuleModels(feedId, _fromTime.Value).ToList();
            if (!previousRules.Any())
                return;

            _zeroCommissionRulesPast.AddRange(previousRules.Where(r => r.FeedRule.FeedRuleType == FeedRuleType.No_Commission));
            _promotionalTextRulesPast.AddRange(previousRules.Where(r => r.FeedRule.FeedRuleType == FeedRuleType.Promotional_Text));
            _feedGeneratorCmsDataServicePast = new FeedGeneratorCmsDataService(_feedCmsProductArciveEntryService, fromTime);
            _zeroCommissionRuleEvaluatorServicePast = new RuleEvaluatorService(_zeroCommissionRulesPast, _feedGeneratorCmsDataServicePast, _feedGeneratorIndigoCategoryService, _allowRuleOptimizations, _allowRuleEntryRemovals, _allowIEnumerableRuleEvaluations);
            _promotionalTextRuleEvaluatorServicePast = new RuleEvaluatorService(_promotionalTextRulesPast, _feedGeneratorCmsDataServicePast, _feedGeneratorIndigoCategoryService, _allowRuleOptimizations, _allowRuleEntryRemovals, _allowIEnumerableRuleEvaluations);
            HaveRulesChanged = !AreSameRules(new RuleComparisonData { RuleSet = rules, FeedGeneratorCmsDataService = _feedGeneratorCmsDataService },
                new RuleComparisonData { RuleSet = previousRules, FeedGeneratorCmsDataService = _feedGeneratorCmsDataServicePast });
        }

        public IRuleEvaluationResult GetZeroCommissionRuleResult(IProductData productData, bool processOldRule)
        {
            return (processOldRule) ? _zeroCommissionRuleEvaluatorServicePast.GetEvaluationResult(productData, FeedRuleType.No_Commission)
                : _zeroCommissionRuleEvaluatorService.GetEvaluationResult(productData, FeedRuleType.No_Commission);
        }

        public IRuleEvaluationResult GetPromotionalTextRuleResult(IProductData productData, bool processOldRule)
        {
            return (processOldRule) ? _promotionalTextRuleEvaluatorServicePast.GetEvaluationResult(productData, FeedRuleType.Promotional_Text)
                : _promotionalTextRuleEvaluatorService.GetEvaluationResult(productData, FeedRuleType.Promotional_Text);
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
