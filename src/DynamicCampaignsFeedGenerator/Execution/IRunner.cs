using FeedGenerators.Core.Services.Abstract;
using System.Collections.Generic;

namespace DynamicCampaignsFeedGenerator.Execution
{
    public interface IRunner
    {
        void Initialize(IExecutionLogLogger executionLogLogger, int feedId);
        IExecutionLogLogger Execute();
    }
}
