using Indigo.Feeds.Generator.Core.Enums;
using System;

namespace Indigo.Feeds.Generator.Core.Execution.Contracts
{
    public interface IBuilder
    {
        void Build(RunType requestedRunType, DateTime? effectiveStartTime = null, DateTime? effectiveEndTime = null);
    }
}
