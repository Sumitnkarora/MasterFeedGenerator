using Indigo.Feeds.Generator.Core.Enums;
using System;
using System.Collections.Generic;

namespace Indigo.Feeds.Generator.Core.Models
{
    /// <summary>
    /// Used to generate a report file.
    /// </summary>
    public class ReportInformation
    {
        public ReportInformation(int feedId)
        {
            FeedId = feedId;
            CustomMessages = new List<string>();
        }

        public DateTime ExecutionStartTime;

        public DateTime? ExecutionEndTime;

        public int FeedId;
        
        public string FeedName;

        public int? FeedRunId;

        public DateTime GenerationTimeUtc;

        public DateTime? EffectiveStartTime;

        public DateTime? EffectiveEndTime;

        public bool HasErrors;

        public int? NumberOfNewRecords;

        public int? NumberOfModifiedRecords;

        public int? NumberOfDeletedRecords;

        public int? NumberOfErrorRecords;

        public int? NumberOfRecordsPerFile;

        public IList<string> CustomMessages;

        public RunType RunType;
    }
}
