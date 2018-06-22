using System.Collections.Generic;
using FeedGenerators.Core.Services.Abstract;

namespace GooglePlaFeedGenerator.Execution
{
    public interface IRunner
    {
        void Initialize(IExecutionLogLogger executionLogLogger, IExecutionLogLogger secondaryExecutionLogger, int feedId, int? secondaryRunId);
        IEnumerable<IExecutionLogLogger> Execute();
    }
}
