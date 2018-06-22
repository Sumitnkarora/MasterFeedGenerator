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

namespace RrFeedGenerator.Execution
{
    internal class RunnerHelper
    {
        private readonly IFeedRuleService _feedRuleService;
        private readonly IFeedCmsProductArchiveEntryService _feedCmsProductArciveEntryService;

        //private int _feedId;
        private bool _isIncrementalRun;
        private DateTime? _fromTime;
        private ILogger _log;
        private IFeedGeneratorCmsDataService _feedGeneratorCmsDataService;
        private IFeedGeneratorCmsDataService _feedGeneratorCmsDataServicePast;
        private IFeedGeneratorIndigoCategoryService _feedGeneratorIndigoCategoryService;

        public bool HaveRulesChanged { get; private set; }

        public RunnerHelper(IFeedRuleService feedRuleService, IFeedGeneratorIndigoCategoryService feedGeneratorIndigoCategoryService, IFeedCmsProductArchiveEntryService feedCmsProductArciveEntryService, ILogger log)
        {
            _feedRuleService = feedRuleService;
            _feedGeneratorIndigoCategoryService = feedGeneratorIndigoCategoryService;
            _feedCmsProductArciveEntryService = feedCmsProductArciveEntryService;
            _log = log;
        }

        public void Initialize(int feedId, bool isIncremental, DateTime? fromTime, DateTime executionTime)
        {
            //_feedId = feedId;
            _isIncrementalRun = isIncremental;
            _fromTime = fromTime;

            // First get rules associated with this feed
            var rules = _feedRuleService.GetFeedRuleModels(feedId, executionTime).ToList();
            // Instantiate the IFeedGeneratorCmsDataService
            _feedGeneratorCmsDataService = new FeedGeneratorCmsDataService(_feedCmsProductArciveEntryService, null);
            // If it's an incremental run, get the rule set at the time of of the previous execution
            // Then make comparisons to check if there were updates to rules. If there were rule updates, 
            // then populate the secondary rule services which will be used to determine if a product is modified
            // even if the product data hasn't changed
            if (!_isIncrementalRun || !_fromTime.HasValue)
                return;

            var previousRules = _feedRuleService.GetFeedRuleModels(feedId, _fromTime.Value).ToList();
            if (!previousRules.Any())
                return;

            _feedGeneratorCmsDataServicePast = new FeedGeneratorCmsDataService(_feedCmsProductArciveEntryService, fromTime);
            HaveRulesChanged = !AreSameRules(new RuleComparisonData { RuleSet = rules, FeedGeneratorCmsDataService = _feedGeneratorCmsDataService },
                new RuleComparisonData { RuleSet = previousRules, FeedGeneratorCmsDataService = _feedGeneratorCmsDataServicePast });
        }

        private bool AreSameRules(RuleComparisonData ruleCriteria1, RuleComparisonData ruleCriteria2)
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
