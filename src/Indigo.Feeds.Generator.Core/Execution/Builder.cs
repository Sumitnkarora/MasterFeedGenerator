using Castle.Core.Logging;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Generator.Core.Enums;
using Indigo.Feeds.Generator.Core.Execution.Contracts;
using Indigo.Feeds.Generator.Core.Models;
using Indigo.Feeds.Generator.Core.Processors.Contracts;
using Indigo.Feeds.Generator.Core.Utils;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Types;
using Indigo.Feeds.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Indigo.Feeds.Generator.Core.Execution
{
    /// <summary>
    /// The builder is the main entrance point for feed generation.
    /// </summary>
    public class Builder : IBuilder
    {
        protected ILogger Log { get; set; }
        protected IFeedService FeedService { get; set; }
        protected IFeedRunService FeedRunService { get; set; }
        protected IRunner Runner { get; set; }

        private IFileContentProcessor _fileProcessor;
        private IFeedRun _currentRun;
        private DateTime _effectiveExecutionEndTime;
        private ExecutionInformation _executionInformation;
        private ReportInformation _reportInformation;

        private static readonly int FeedId = ParameterUtils.GetParameter<int>("FeedId");
        private static readonly int MaximumFeedRunsToKeep = ParameterUtils.GetParameter<int>("MaximumFeedRunsToKeep");
        private static readonly bool AllowIncrementalRuns = ParameterUtils.GetParameter<bool>("Builder.AllowIncrementalRuns");
        private static readonly int IncrementalRunBufferTimeLength = ParameterUtils.GetParameter<int>("Builder.IncrementalRunBufferTimeLength");
        private static readonly string TestIncrementalRunFromDate = ParameterUtils.GetParameter<string>("TestIncrementalRunFromDate");
        private static readonly string ReportFolderPath = ParameterUtils.GetParameter<string>("Builder.ReportFolder");
        private static readonly string ReportFileNameFormat = ParameterUtils.GetParameter<string>("Builder.ReportFileNameFormat");
        private static readonly int ReportMaxNumberOfMessages = ParameterUtils.GetParameter<int>("Builder.ReportMaxNumberOfMessages");

        public Builder(IFeedService feedService, IFeedRunService feedRunService, IRunner runner, IFileContentProcessor fileProcessor, ILogger logger)
        {
            FeedRunService = feedRunService;
            FeedService = feedService;
            Runner = runner;
            _fileProcessor = fileProcessor;
            Log = logger;
        }

        public void Build(RunType requestedRunType, DateTime? effectiveStartTime = null, DateTime? effectiveEndTime = null)
        {
            try
            {
                NewRelic.Api.Agent.NewRelic.AddCustomParameter("FeedId", FeedId);
                // start report creation
                _reportInformation = new ReportInformation(FeedId)
                {
                    ExecutionStartTime = DateTime.Now,
                    RunType = requestedRunType,
                    EffectiveEndTime = effectiveEndTime
                };
                
                //check if another instance is running - if so exit
                var isProcessRunning = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location)).Length > 1;
                if (isProcessRunning)
                {
                    Log.Info("Another instance was running, so exiting the application without any processing.");
                    return;
                }

                Log.Info("Execution started!");
                if (!Runner.IsReady())
                {
                    Log.Info("Runner is not ready for a new run. Exiting the application without executing a new run.");
                    return;
                }

                var feed = FeedService.GetFeed(FeedId);
                _reportInformation.FeedName = feed.Name;
                if (feed.IsPaused)
                {
                    Log.InfoFormat("{0} feed is paused. Exiting application without any processing.", feed.Name);
                    _reportInformation.CustomMessages.Add("Feed is paused. No processing done.");
                    return;
                }

                // First decide if a full run should be enforced. 
                var lastSuccessfulRun = FeedRunService.GetLastSuccessfulRun(FeedId);
                var enforceFullRun = ShouldEnforceFullRun(requestedRunType, lastSuccessfulRun);

                // If incremental run requested, but full run required first (unless forced)
                // then abort as the incremental might be configured for different output
                if (requestedRunType == RunType.Incremental && enforceFullRun)
                {
                    Log.InfoFormat("{0} feed must execute a full run first. Exiting application without any processing.", feed.Name);
                    _reportInformation.CustomMessages.Add("Must execute a full run first. No processing done.");
                    return;
                }

                // Next, need to decide on the effective start time.
                _currentRun = FeedRunService.GetOrCreateActiveFeedRun(FeedId, enforceFullRun, AllowIncrementalRuns);
                _reportInformation.FeedRunId = _currentRun.FeedRunId;
                DateTime? fromTime = null;
                // Only set these values if we aren't in a "real" full run
                if (!enforceFullRun && requestedRunType != RunType.OnDemand)
                {
                    fromTime = GetFromTime(_currentRun);
                    // Effective start time is used for callibrating the fromTime to a previous point in time, in case this is needed due to the execution
                    // sequence/timinig of other processes that impact the data that the inventory feed depends on. For example, if results of an inventory-related 
                    // process takes 2 hours to get replicated down to the catalogue/website, where as the inventory data has already been updated in Bronte at 
                    // the time of execution AND business has updated rules related to the feed in the past 15 minutes, then gathering the incremental data as 
                    // if the run started two hours ago but applying rule changes using "now" as the reference point will yield more "accurate" results.
                    effectiveStartTime = fromTime;
                    if (_currentRun.FeedRunType == FeedRunType.Incremental)
                        effectiveStartTime = fromTime.Value.AddMinutes(-IncrementalRunBufferTimeLength);
                }

                _reportInformation.EffectiveStartTime = effectiveStartTime;
                _effectiveExecutionEndTime = DateTime.Now;

                var runType = _currentRun.FeedRunType == FeedRunType.Incremental ? RunType.Incremental : requestedRunType;
                Runner.Initialize(
                    runType, 
                    effectiveStartTime,
                    effectiveEndTime);
                _executionInformation = Runner.Execute();
                UpdateCountersInReport(_executionInformation, runType);
                
                // Ensure that we kill the watcher here as well
                if (_executionInformation.HasError)
                {
                    _reportInformation.HasErrors = true;
                    LoggingHelper.Error("Execution failed!!! Exiting application without further processing.", Log);
                    HandleExit();
                    return;
                }

                _reportInformation.ExecutionEndTime = DateTime.Now;
                HandleSuccess();

                var elapsedTime = _reportInformation.ExecutionEndTime.Value - _reportInformation.ExecutionStartTime;
                Log.InfoFormat("Execution completed in {0}", elapsedTime.ToString(@"dd\.hh\:mm\:ss"));
            }
            catch (Exception e)
            {
                LoggingHelper.Error(e, "Execution failed!!!", Log);
                _reportInformation.CustomMessages.Add("Exception during execution. See logs for details.");
                HandleExit();
            }
        }

        private void UpdateCountersInReport(ExecutionInformation executionInformation, RunType runType)
        {
            _reportInformation.NumberOfRecordsPerFile = _executionInformation.RecordsPerFile;
            if (runType == RunType.Full)
            {
                _reportInformation.NumberOfNewRecords = _executionInformation.TotalRecordsNew + _executionInformation.TotalRecordsModified;
            }
            else
            {
                _reportInformation.NumberOfNewRecords = _executionInformation.TotalRecordsNew;
                _reportInformation.NumberOfModifiedRecords = _executionInformation.TotalRecordsModified;
            }
            _reportInformation.NumberOfDeletedRecords = _executionInformation.TotalRecordsDeleted;
            _reportInformation.NumberOfErrorRecords = _executionInformation.TotalRecordsErrored;
            var messages = _executionInformation.GetCustomMessages();
            for (int i = 0; i < messages.Length && i < ReportMaxNumberOfMessages; i++)
            {
                _reportInformation.CustomMessages.Add(messages[i]);
                if (i + 1 == ReportMaxNumberOfMessages)
                {
                    _reportInformation.CustomMessages.Add("See logs for more messages.");
                }
            }
        }

        private void HandleSuccess()
        {
            try
            {
                RecordSuccessfulFeedRun(_currentRun);

                if (!Runner.Finalize(true))
                {
                    Log.Warn("Finalize processing resulted in error. The outputs that errored out will be retried upon next execution.");
                    LoggingHelper.ErrorToNewRelic(
                        "PartiallyFailedRun",
                        new Dictionary<string, string> { { "Message", "Please view log4net logs for details" } });
                }

                // Remove old jobs
                RemoveOldJobs();
            }
            catch
            {
                throw;
            }
            finally
            {
                Exit();
            }
        }

        private void RecordSuccessfulFeedRun(IFeedRun feedRun)
        {
            feedRun.DateCompleted = _effectiveExecutionEndTime;
            feedRun.LastExecutionStartDate = _reportInformation.ExecutionStartTime;
            feedRun.LastExecutionCompletedDate = _reportInformation.ExecutionEndTime.Value;
            feedRun.ExecutionLog = _executionInformation.GetExecutionLog();
            FeedRunService.EndFeedRun(feedRun, true);
        }

        private void HandleExit()
        {
            try
            {
                _reportInformation.HasErrors = true;
                // Set the current run to be incomplete
                RecordFailedFeedRun(FeedId);
                Runner.Finalize(false);
            }
            catch
            {
                throw;
            }
            finally
            {
                LoggingHelper.ErrorToNewRelic(
                    "FailedRun",
                    new Dictionary<string, string> { { "Message", "Please view log4net logs for details" } });

                Log.Info("Executed HandleExit()!");

                Exit();
            }
        }

        private void Exit()
        {
            // add additional metrics
            if (_reportInformation.NumberOfNewRecords.HasValue)
            {
                NewRelic.Api.Agent.NewRelic.AddCustomParameter("NumberOfNewRecords", _reportInformation.NumberOfNewRecords.Value);
            }
            if (_reportInformation.NumberOfModifiedRecords.HasValue)
            {
                NewRelic.Api.Agent.NewRelic.AddCustomParameter("NumberOfUpdatedRecords", _reportInformation.NumberOfModifiedRecords.Value);
            }
            if (_reportInformation.NumberOfDeletedRecords.HasValue)
            {
                NewRelic.Api.Agent.NewRelic.AddCustomParameter("NumberOfDeletedRecords", _reportInformation.NumberOfDeletedRecords.Value);
            }
            if (_reportInformation.NumberOfErrorRecords.HasValue)
            {
                NewRelic.Api.Agent.NewRelic.AddCustomParameter("NumberOfErroredOutRecords", _reportInformation.NumberOfErrorRecords.Value);
            }
            if (_reportInformation.NumberOfRecordsPerFile.HasValue)
            {
                NewRelic.Api.Agent.NewRelic.AddCustomParameter("NumberOfRecordsPerFile", _reportInformation.NumberOfRecordsPerFile.Value);
            }

            _reportInformation.GenerationTimeUtc = DateTime.UtcNow;

            CreateReport(_reportInformation);
        }

        private void CreateReport(ReportInformation reportInformation)
        {
            string filePath = null;
            try
            {
                if (!_fileProcessor.DirectoryExists(ReportFolderPath))
                {
                    _fileProcessor.DirectoryCreate(ReportFolderPath);
                }

                filePath = GeneratorHelper.GetFilePathFormatted(
                    ReportFolderPath,
                    ReportFileNameFormat,
                    reportInformation.FeedId.ToString(),
                    reportInformation.FeedRunId.HasValue ? reportInformation.FeedRunId.Value.ToString() : "0",
                    reportInformation.ExecutionStartTime.ToString("yyyy-MM-dd_hh-mm-ss-fff"));

                using (var fileStream = _fileProcessor.FileCreate(filePath))
                {
                    using (StreamWriter writer = new StreamWriter(fileStream))
                    {
                        using (JsonWriter jsonWriter = new JsonTextWriter(writer))
                        {
                            var serializer = new JsonSerializer();
                            serializer.NullValueHandling = NullValueHandling.Ignore;
                            serializer.Serialize(jsonWriter, reportInformation);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.Error(ex, string.Format("Failed to create a report for {0}", filePath));
            }
        }

        private void RecordFailedFeedRun(int feedId)
        {
            var feedRun = FeedRunService.GetLatestRun(feedId);
            feedRun.LastExecutionStartDate = _reportInformation.ExecutionStartTime;
            feedRun.LastExecutionCompletedDate = DateTime.Now;
            _executionInformation.AddCustomMessage("This execution failed. Terminating execution.");
            feedRun.ExecutionLog = _executionInformation.GetExecutionLog();
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

        private static bool ShouldEnforceFullRun(RunType forceRunType, IFeedRun lastSuccessfulRun)
        {
            var enforceFullRun = forceRunType == RunType.Full;
            if (!AllowIncrementalRuns || enforceFullRun)
            {
                return true;
            }

            // If there was no successful prior run
            if (lastSuccessfulRun == null)
            {
                return true;
            }

            return false;
        }
    }
}
