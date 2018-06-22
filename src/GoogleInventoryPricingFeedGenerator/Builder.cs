using System.Collections.Generic;
using Castle.Core.Logging;
using GoogleInventoryPricingFeedGenerator.Execution;
using FeedGenerators.Core.Services.Abstract;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Types;
using Indigo.Feeds.Utils;
using Renci.SshNet;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GoogleInventoryPricingFeedGenerator
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

        private static readonly int FeedId = ParameterUtils.GetParameter<int>("FeedId");
        private static readonly int GooglePlaFeedId = ParameterUtils.GetParameter<int>("GooglePlaFeedId");
        private static readonly int MaximumFeedRunsToKeep = ParameterUtils.GetParameter<int>("MaximumFeedRunsToKeep");
        private static readonly string OutputFolderPath = ParameterUtils.GetParameter<string>("GoogleInventoryPricingFeedGenerator.OutputFolderPath");
        private static readonly string PrimaryOutputFolderName = ParameterUtils.GetParameter<string>("PrimaryOutputFolderName");
        private static readonly string DoneFileFileName = ParameterUtils.GetParameter<string>("DoneFileFileName");
        private static readonly int OutputFileMoveMethod = int.Parse(ParameterUtils.GetParameter<string>("GoogleInventoryPricingFeedGenerator.OutputFileMoveMethod"));
        private static readonly string FtpHost = ParameterUtils.GetParameter<string>("GoogleInventoryPricingFeedGenerator.FtpHost");
        private static readonly string FtpDropFolderPath = ParameterUtils.GetParameter<string>("GoogleInventoryPricingFeedGenerator.FtpDropFolderPath");
        private static readonly string FtpUserName = ParameterUtils.GetParameter<string>("GoogleInventoryPricingFeedGenerator.FtpUserName");
        private static readonly string FtpUserPassword = ParameterUtils.GetParameter<string>("GoogleInventoryPricingFeedGenerator.FtpUserPassword");
        private static readonly int FtpBufferSize = ParameterUtils.GetParameter<int>("FtpBufferSize");
        private static readonly string TargetFolderPath = ParameterUtils.GetParameter<string>("GoogleInventoryPricingFeedGenerator.TargetFolderPath");
        private static readonly bool AllowIncrementalRuns = ParameterUtils.GetParameter<bool>("GoogleInventoryPricingFeedGenerator.AllowIncrementalRuns");
        private static readonly string FullRunParameterName = ParameterUtils.GetParameter<string>("FullRunParameterName");
        private static readonly int IncrementalRunBufferTimeLength = ParameterUtils.GetParameter<int>("GoogleInventoryPricingFeedGenerator.IncrementalRunBufferTimeLength");
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

                // First check if the "done" file is there. If so, skip job execution and move to file-related steps
                if (HasDoneFile())
                {
                    Log.Info("Done file exists at the drop folder. Skipping job execution and moving to file operations.");
                    ExecuteFileOperations();
                    return;
                }

                Log.Info("Execution started!");
                var feed = FeedService.GetFeed(FeedId);

                if (feed.IsPaused)
                {
                    Log.InfoFormat("{0} feed is paused. Exiting application without any processing.", feed.Name);
                    return;
                }

                // First clean all the files inside the output folder (in case the previous run failed and left out old files behind.)
                RemoveOldFiles();

                // First decide if a full run should be enforced. 
                var lastSuccessfulPlaFeedRun = FeedRunService.GetLastSuccessfulRun(GooglePlaFeedId);
                var enforceFullRun = ShouldEnforceFullRun(arguments, lastSuccessfulPlaFeedRun);

                // Next, need to decide on the effective start time. If the previous successful run of the inventory feed
                // was more recent than the most recent successful run of the Google PLA feed, then use its end time. 
                // Otherwise, use the end time of the most recent successful Google PLA feed run. 
                _currentRun = FeedRunService.GetOrCreateActiveFeedRun(FeedId, enforceFullRun, AllowIncrementalRuns);
                var fromTime = GetFromTime(enforceFullRun, _currentRun, lastSuccessfulPlaFeedRun);
                // Effective start time is used for callibrating the fromTime to a previous point in time, in case this is needed due to the execution
                // sequence/timinig of other processes that impact the data that the inventory feed depends on. For example, if results of an inventory-related 
                // process takes 2 hours to get replicated down to the catalogue/website, where as the inventory data has already been updated in Bronte at 
                // the time of execution AND business has updated rules related to the feed in the past 15 minutes, then gathering the incremental data as 
                // if the run started two hours ago but applying rule changes using "now" as the reference point will yield more "accurate" results.
                var effectiveStartTime = fromTime;
                if (_currentRun.FeedRunType == FeedRunType.Incremental)
                    effectiveStartTime = fromTime.AddHours(-IncrementalRunBufferTimeLength);
                
                _effectiveExecutionEndTime = DateTime.Now;

                Runner.Initialize(ExecutionLogLogger, GooglePlaFeedId, _currentRun.FeedRunType == FeedRunType.Incremental, fromTime, effectiveStartTime, _effectiveExecutionEndTime);
                ExecutionLogLogger = Runner.Execute();
                
                if (ExecutionLogLogger.HasError)
                {
                    Log.Error("Execution failed!!! Exiting application without moving any files.");
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

        private bool ShouldEnforceFullRun(IEnumerable<string> arguments, IFeedRun lastSuccessfulPlaFeedRun)
        {
            var enforceFullRun = arguments.ToList().Contains(FullRunParameterName, StringComparer.InvariantCultureIgnoreCase);
            if (!enforceFullRun && !AllowIncrementalRuns)
                enforceFullRun = true;
            
            var lastSuccessfulInventoryFeedRun = FeedRunService.GetLastSuccessfulRun(FeedId);
            // If there was no successful PLA feed or successful Inventory feed, enforce a full run
            if (lastSuccessfulPlaFeedRun == null && lastSuccessfulInventoryFeedRun == null)
                enforceFullRun = true;

            return enforceFullRun;
        }

        private DateTime GetFromTime(bool enforceFullRun, IFeedRun inventoryFeedRun, IFeedRun lastSuccessfulPlaFeedRun)
        {
            DateTime effectiveStartTime;
            if (lastSuccessfulPlaFeedRun == null || inventoryFeedRun.DateStarted > lastSuccessfulPlaFeedRun.DateStarted)
                effectiveStartTime = inventoryFeedRun.DateStarted;
            else
                effectiveStartTime = lastSuccessfulPlaFeedRun.DateStarted;

            // If in an incremental run and a test from date value was specified, use that value
            if (inventoryFeedRun.FeedRunType == FeedRunType.Incremental && !string.IsNullOrWhiteSpace(TestIncrementalRunFromDate))
            {
                effectiveStartTime = DateTime.Parse(TestIncrementalRunFromDate);
                Log.InfoFormat("Found TestIncrementalRunFromDate. Using its value {0}", TestIncrementalRunFromDate);
            }

            return effectiveStartTime;
        }

        private void HandleSuccess()
        {
            RecordSuccessfulFeedRun(_currentRun, ExecutionLogLogger);

            // Write out the new "done" file
            WriteDoneFile();

            // Execute file-related operations
            ExecuteFileOperations();

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

        private static bool HasDoneFile()
        {
            return File.Exists(Path.Combine(OutputFolderPath, DoneFileFileName));
        }

        private static void WriteDoneFile()
        {
            if (!Directory.Exists(OutputFolderPath))
                Directory.CreateDirectory(OutputFolderPath);

            using (new FileStream(Path.Combine(OutputFolderPath, DoneFileFileName), FileMode.Create))
            {
                // Nothing to do
            }
        }

        private void ExecuteFileOperations()
        {
            Log.Info("Starting executing file-related operations.");
            if (OutputFileMoveMethod == 0)
            {
                Log.Info("Moving files is disabled. Exiting file-related operations without any further action.");
                return;
            }

            if (!MoveFiles())
                return;

            // Remove the done file
            File.Delete(Path.Combine(OutputFolderPath, DoneFileFileName));
            Log.Info("Completed executing file-related operations.");
        }

        private bool MoveFiles()
        {
            // First get all files inside the drop folder that need to be moved over ordered by their creation time
            var folderPath = Path.Combine(OutputFolderPath, PrimaryOutputFolderName);
            var outputFolderInfo = new DirectoryInfo(folderPath);
            var pendingFiles = outputFolderInfo.GetFiles().Where(f =>
            {
                var name = Path.GetFileName(f.FullName);
                return !string.IsNullOrWhiteSpace(name);
            }).OrderBy(f => f.CreationTime);


            if (pendingFiles.Any())
            {
                if (OutputFileMoveMethod == 1)
                {
                    try
                    {
                        var ftpHost = FtpHost;
                        var ftpUserName = FtpUserName;
                        var ftpUserPassword = FtpUserPassword;
                        var ftpDropFolderPath = FtpDropFolderPath;

                        Log.InfoFormat("Connecting to {0} using SFTP.", FtpHost);
                        using (var sftpClient = new SftpClient(ftpHost, ftpUserName, ftpUserPassword))
                        {
                            sftpClient.Connect();
                            Log.InfoFormat("Connected to {0} using SFTP.", ftpHost);
                            if (!string.IsNullOrEmpty(ftpDropFolderPath))
                            {
                                sftpClient.ChangeDirectory(ftpDropFolderPath);
                                Log.InfoFormat("Changed working ftp directory to {0}", ftpDropFolderPath);
                            }

                            foreach (var pendingFile in pendingFiles)
                            {
                                try
                                {
                                    using (var fileStream = new FileStream(pendingFile.FullName, FileMode.Open))
                                    {
                                        var fileName = Path.GetFileName(pendingFile.FullName);
                                        Log.InfoFormat("Starting to upload {0}", fileName);
                                        sftpClient.BufferSize = (uint)FtpBufferSize;
                                        sftpClient.UploadFile(fileStream, fileName);
                                        Log.InfoFormat("Completed uploading {0}", fileName);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error("File upload failed!!! Exiting moving files.", ex);
                                    return false;
                                }

                                // Delete the file 
                                File.Delete(pendingFile.FullName);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("File upload failed!!! Exiting moving files.", ex);
                        return false;
                    }
                }
                // Copy files
                else
                {
                    try
                    {
                        var targetFolderPath = TargetFolderPath;
                        // First move all files 
                        foreach (var pendingFile in pendingFiles)
                        {
                            File.Copy(pendingFile.FullName, Path.Combine(targetFolderPath, Path.GetFileName(pendingFile.FullName)), true);
                            Log.InfoFormat("Completed moving file {0}.", Path.GetFileName(pendingFile.FullName));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Moving files to target folder failed!!! Exiting moving files.", ex);
                        return false;
                    }
                }
            }

            return true;
        }

        private void RemoveOldJobs()
        {
            FeedRunService.RemoveOldFeedRuns(FeedId, MaximumFeedRunsToKeep);
        }

        private static void RemoveOldFiles()
        {
            EnsureFolderExists(OutputFolderPath);

            // Delete all files and folders inside output folder path in the generated xml files directory
            var directoryInfo = new DirectoryInfo(OutputFolderPath);
            foreach (var file in directoryInfo.GetFiles())
            {
                file.Delete();
            }

            foreach (var folder in directoryInfo.GetDirectories())
            {
                folder.Delete(true);
            }

            // If the target folders don't exist, create them too
            var primaryFolderPath = Path.Combine(OutputFolderPath, PrimaryOutputFolderName);
            EnsureFolderExists(primaryFolderPath);
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }
    }
}
