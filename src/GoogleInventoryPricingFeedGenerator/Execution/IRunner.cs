﻿using FeedGenerators.Core.Services.Abstract;
using System;

namespace GoogleInventoryPricingFeedGenerator.Execution
{
    public interface IRunner
    {
        void Initialize(IExecutionLogLogger executionLogLogger, int feedId, bool isIncremental, DateTime? fromTime, DateTime? effectiveFromTime, DateTime executionTime);
        IExecutionLogLogger Execute();
    }
}
