using Castle.Core.Logging;
using FeedGenerators.Core.Services.Abstract;
using GoogleApiPlaFeedGenerator.Execution;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Types;
using Indigo.Feeds.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GoogleApiPlaFeedGenerator
{
    public class Builder : IBuilder
    {
        private IFeedRun _currentRun;
        private DateTime _effectiveExecutionEndTime;

        public ILogger Log { get; set; }
        public IFeedService FeedService { get; set; }
        public IFeedRunService FeedRunService { get; set; }
        public IRunner Runner { get; set; }
        public IExecutionLogLogger ExecutionLogLogger { get; set; }
        public IOutputInstructionProcessor OutputInstructionProcessor { get; set; }

        private static readonly int FeedId = ParameterUtils.GetParameter<int>("FeedId");
        private static readonly int GooglePlaFeedId = ParameterUtils.GetParameter<int>("GooglePlaFeedId");
        private static readonly int MaximumFeedRunsToKeep = ParameterUtils.GetParameter<int>("MaximumFeedRunsToKeep");
        private static readonly bool AllowIncrementalRuns = ParameterUtils.GetParameter<bool>("GoogleApiPlaFeedGenerator.AllowIncrementalRuns");
        private static readonly string FullRunParameterName = ParameterUtils.GetParameter<string>("FullRunParameterName");
        private static readonly int IncrementalRunBufferTimeLength = ParameterUtils.GetParameter<int>("GoogleApiPlaFeedGenerator.IncrementalRunBufferTimeLength");
        private static readonly string TestIncrementalRunFromDate = ParameterUtils.GetParameter<string>("TestIncrementalRunFromDate");

        public void Build(string[] arguments)
        {
            try
            {
                //check if another instance is running - if so exit
                ExecutionLogLogger.SetExecutionStartTime(DateTime.Now);
                var isProcessRunning = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location)).Length > 1;
                if (isProcessRunning)
                {
                    Log.Info("Another instance was running, so exiting the application without any processing.");
                    return;
                }

                Log.Info("Execution started!");
                if (OutputInstructionProcessor.Initialize())
                {
                    Log.Info("Instruction processor processed instructions from the previous exection. Exiting the application without executing a new run.");
                    return;
                }

                var feed = FeedService.GetFeed(FeedId);
                if (feed.IsPaused)
                {
                    Log.InfoFormat("{0} feed is paused. Exiting application without any processing.", feed.Name);
                    return;
                }

                // First decide if a full run should be enforced. 
                var lastSuccessfulRun = FeedRunService.GetLastSuccessfulRun(FeedId);
                var enforceFullRun = ShouldEnforceFullRun(arguments, lastSuccessfulRun);
                var isPseudoFullRun = false;
                // If a full run is being enforced, but there is an existing successful run, execute an incremental run, where we still send products 
                // that haven't been modified/touched by rule changes. This is necessary because a product expires in 30 days in Google if no updates
                // about the product is sent. The solution we came up with regarding this behavior is having 2 schedules for this feed generator, where one will run
                // only once a week and enforce a "full run". If we're in such a pseudo full run, then we'll execute an incremental run, while ensuring that unmodified
                // product data still gets passed. 
                if (enforceFullRun && lastSuccessfulRun != null)
                    isPseudoFullRun = true;

                // Next, need to decide on the effective start time.
                _currentRun = FeedRunService.GetOrCreateActiveFeedRun(FeedId, enforceFullRun && !isPseudoFullRun, AllowIncrementalRuns);
                DateTime? fromTime = null;
                DateTime? effectiveStartTime = null;
                // Only set these values if we aren't in a "real" full run
                if (!(enforceFullRun && !isPseudoFullRun))
                {
                    fromTime = GetFromTime(_currentRun);
                    // Effective start time is used for callibrating the fromTime to a previous point in time, in case this is needed due to the execution
                    // sequence/timinig of other processes that impact the data that the inventory feed depends on. For example, if results of an inventory-related 
                    // process takes 2 hours to get replicated down to the catalogue/website, where as the inventory data has already been updated in Bronte at 
                    // the time of execution AND business has updated rules related to the feed in the past 15 minutes, then gathering the incremental data as 
                    // if the run started two hours ago but applying rule changes using "now" as the reference point will yield more "accurate" results.
                    effectiveStartTime = fromTime;
                    if (_currentRun.FeedRunType == FeedRunType.Incremental)
                        effectiveStartTime = fromTime.Value.AddHours(-IncrementalRunBufferTimeLength);
                }

                _effectiveExecutionEndTime = DateTime.Now;

                Runner.Initialize(ExecutionLogLogger, GooglePlaFeedId, _currentRun.FeedRunType == FeedRunType.Incremental, fromTime, effectiveStartTime, _effectiveExecutionEndTime, isPseudoFullRun);
                ExecutionLogLogger = Runner.Execute();

                // Ensure that we kill the watcher here as well
                if (ExecutionLogLogger.HasError)
                {
                    Log.Error("Execution failed!!! Exiting application without further processing.");
                    HandleExit();
                    return;
                }

                HandleSuccess();

                var elapsedTime = DateTime.Now - ExecutionLogLogger.GetExecutionStartTime();
                Log.InfoFormat("Execution completed in {0}", elapsedTime.ToString(@"dd\.hh\:mm\:ss"));
            }
            catch (Exception e)
            {
                Log.Error("Execution failed!!!", e);
                HandleExit();
            }
        }

        private void HandleSuccess()
        {
            RecordSuccessfulFeedRun(_currentRun, ExecutionLogLogger);

            if (!OutputInstructionProcessor.FinalizeProcessing(true))
                Log.Info("Sending data to Google resulted in some errors. There outputs that errored out will be retried upon next execution.");

            // Remove old jobs
            RemoveOldJobs();
        }

        private void RecordSuccessfulFeedRun(IFeedRun feedRun, IExecutionLogLogger executionLogLogger)
        {
            feedRun.DateCompleted = _effectiveExecutionEndTime;
            feedRun.LastExecutionStartDate = executionLogLogger.GetExecutionStartTime();
            feedRun.LastExecutionCompletedDate = executionLogLogger.GetExecutionEndTime();
            feedRun.ExecutionLog = executionLogLogger.GetExecutionLog();
            FeedRunService.EndFeedRun(feedRun, true);
        }

        private void HandleExit()
        {
            // Set the current run to be incomplete
            RecordFailedFeedRun(FeedId, ExecutionLogLogger);

            OutputInstructionProcessor.FinalizeProcessing(false);

            Log.Info("Executed HandleExit()!");
        }

        private void RecordFailedFeedRun(int feedId, IExecutionLogLogger executionLogLogger)
        {
            var feedRun = FeedRunService.GetLatestRun(feedId);
            feedRun.LastExecutionStartDate = executionLogLogger.GetExecutionStartTime();
            executionLogLogger.AddCustomMessage("This execution failed. Terminating execution.");
            feedRun.ExecutionLog = executionLogLogger.GetExecutionLog();
            FeedRunService.EndFeedRun(feedRun, false);
        }

        private void RemoveOldJobs()
        {
            FeedRunService.RemoveOldFeedRuns(FeedId, MaximumFeedRunsToKeep);
        }

        private DateTime GetFromTime(IFeedRun inventoryFeedRun)
        {
            DateTime effectiveStartTime = inventoryFeedRun.DateStarted;

            // If in an incremental run and a test from date value was specified, use that value
            if (inventoryFeedRun.FeedRunType == FeedRunType.Incremental && !string.IsNullOrWhiteSpace(TestIncrementalRunFromDate))
            {
                effectiveStartTime = DateTime.Parse(TestIncrementalRunFromDate);
                Log.InfoFormat("Found TestIncrementalRunFromDate. Using its value {0}", TestIncrementalRunFromDate);
            }

            return effectiveStartTime;
        }

        private static bool ShouldEnforceFullRun(IEnumerable<string> arguments, IFeedRun lastSuccessfulRun)
        {
            var enforceFullRun = arguments.ToList().Contains(FullRunParameterName, StringComparer.InvariantCultureIgnoreCase);
            if (!AllowIncrementalRuns || enforceFullRun)
                return true;

            // If there was no successful prior run
            if (lastSuccessfulRun == null)
                return true;

            return false;
        }
    }
}
