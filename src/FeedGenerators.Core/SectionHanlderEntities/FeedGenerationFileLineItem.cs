
using System;
using System.Collections.Generic;
using System.Linq;

namespace FeedGenerators.Core.SectionHanlderEntities
{
    public class FeedGenerationFileLineItem
    {
        public bool IsIncluded { get; set; }
        public string Catalog { get; set; }
        public string StoredProcedureName { get; set; }
        public string Catalogattributesection { get; set; }
        public string RangeDatas { get; set; }

        public FeedGenerationFileItemRange[] GetRanges()
        {
            if (string.IsNullOrWhiteSpace(RangeDatas))
                throw new Exception("Must provide value for ranges attribute.");

            return RangeDatas.Split(new[] {';'}).Select(rangeData => rangeData.Split('-')).Select(parts => new FeedGenerationFileItemRange {Begin = int.Parse(parts[0]), End = int.Parse(parts[1])}).ToArray();
        }
    }
}
