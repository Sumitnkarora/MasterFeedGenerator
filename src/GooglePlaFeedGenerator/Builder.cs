using Castle.Core.Logging;
using FeedGenerators.Core.Services.Abstract;
using GooglePlaFeedGenerator.Execution;
using GooglePlaFeedGenerator.Types;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Utils;
using Renci.SshNet;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GooglePlaFeedGenerator
{
    public class Builder : IBuilder
    {
        private IFeedRun _currentRun;
        private IFeedRun _currentSecondaryRun;
        private DateTime _effectiveExecutionEndTime;
        public ILogger Log { get; set; }
        public IFeedService FeedService { get; set; }
        public IFeedRunService FeedRunService { get; set; }
        public IRunner Runner { get; set; }
        public IExecutionLogLogger ExecutionLogLogger { get; set; }
        public IExecutionLogLogger ExecutionLogLoggerSecondary { get; set; }

        private static readonly RunMode RunMode = (RunMode)ParameterUtils.GetParameter<int>("GooglePlaFeedGenerator.RunMode"); 
        private static readonly int FeedId = ParameterUtils.GetParameter<int>("FeedId");
        private static readonly int FeedIdSecondary = (RunMode == RunMode.PrimaryAndSecondary) ? ParameterUtils.GetParameter<int>("FeedIdSecondary") : default(int);
        private static readonly int MaximumFeedRunsToKeep = ParameterUtils.GetParameter<int>("MaximumFeedRunsToKeep");
        private static readonly string OutputFolderPath = ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.OutputFolderPath");
        private static readonly string PrimaryOutputFolderName = ParameterUtils.GetParameter<string>("PrimaryOutputFolderName");
        private static readonly string SecondaryOutputFolderName = ParameterUtils.GetParameter<string>("SecondaryOutputFolderName");
        private static readonly string DoneFileFileName = ParameterUtils.GetParameter<string>("DoneFileFileName");
        private static readonly int OutputFileMoveMethod = int.Parse(ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.OutputFileMoveMethod"));
        private static readonly string FtpHost = ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.FtpHost");
        private static readonly string FtpDropFolderPath = ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.FtpDropFolderPath");
        private static readonly string FtpUserName = ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.FtpUserName");
        private static readonly string FtpUserPassword = ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.FtpUserPassword");
        private static readonly string FtpHostSecondary = ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.FtpHostSecondary");
        private static readonly string FtpDropFolderPathSecondary = ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.FtpDropFolderPathSecondary");
        private static readonly string FtpUserNameSecondary = ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.FtpUserNameSecondary");
        private static readonly string FtpUserPasswordSecondary = ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.FtpUserPasswordSecondary");
        private static readonly int FtpBufferSize = ParameterUtils.GetParameter<int>("FtpBufferSize");
        private static readonly string TargetFolderPath = ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.TargetFolderPath");
        private static readonly string TargetFolderPathSecondary = ParameterUtils.GetParameter<string>("GooglePlaFeedGenerator.TargetFolderPathSecondary");

        public void Build(string[] args)
        {
            try
            {
                //check if another instance is running - if so exit
                ExecutionLogLogger.SetExecutionStartTime(DateTime.Now);
                ExecutionLogLoggerSecondary.SetExecutionStartTime(DateTime.Now);
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
                IFeed secondaryFeed = null;
                if (RunMode == RunMode.PrimaryAndSecondary)
                    secondaryFeed = FeedService.GetFeed(FeedIdSecondary);

                var allowPrimary = !feed.IsPaused;
                var allowSecondary = secondaryFeed != null && !secondaryFeed.IsPaused;
                if (!allowPrimary && !allowSecondary)
                {
                    Log.InfoFormat("Both primary {0} and secondary {1} feeds are paused. Exiting application without any processing.", feed.Name, secondaryFeed.Name);
                    return;
                }

                // If one of the feeds is paused, notify the user and move on
                if (!allowPrimary)
                    Log.InfoFormat("The primary feed {0} is in paused state. Skipping primary feed.", feed.Name);
                
                if (RunMode == RunMode.PrimaryAndSecondary && !allowSecondary)
                    Log.InfoFormat("The secondary feed {0} is in paused state. Skipping secondary feed.", secondaryFeed.Name);

                // First clean all the files inside the output folder (in case the previous run failed and left out old files behind.)
                RemoveOldFiles();

                _currentRun = FeedRunService.GetOrCreateActiveFeedRun(feed.FeedId, true, false);
                int? secondaryRunId = null;
                if (RunMode == RunMode.PrimaryAndSecondary)
                {
                    _currentSecondaryRun = FeedRunService.GetOrCreateActiveFeedRun(secondaryFeed.FeedId, true, false);
                    secondaryRunId = _currentSecondaryRun.FeedId;
                }

                _effectiveExecutionEndTime = DateTime.Now;
                Runner.Initialize(ExecutionLogLogger, ExecutionLogLoggerSecondary, _currentRun.FeedId, secondaryRunId);
                var results = Runner.Execute().ToList();
                ExecutionLogLogger = results[0];
                if (RunMode == RunMode.PrimaryAndSecondary)
                    ExecutionLogLoggerSecondary = results[1];

                if (ExecutionLogLogger.HasError || ExecutionLogLoggerSecondary.HasError)
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

        private void HandleSuccess()
        {
            RecordSuccessfulFeedRun(_currentRun, ExecutionLogLogger);
            if (RunMode == RunMode.PrimaryAndSecondary)
                RecordSuccessfulFeedRun(_currentSecondaryRun, ExecutionLogLoggerSecondary);

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
            if (RunMode == RunMode.PrimaryAndSecondary) 
                RecordFailedFeedRun(FeedIdSecondary, ExecutionLogLoggerSecondary);

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

            var hasMovedFiles = MoveFiles(PrimaryOutputFolderName, false);
            if (hasMovedFiles && RunMode == RunMode.PrimaryAndSecondary)
                hasMovedFiles = MoveFiles(SecondaryOutputFolderName, true);

            if (!hasMovedFiles) 
                return;

            // Remove the done file
            File.Delete(Path.Combine(OutputFolderPath, DoneFileFileName));
            Log.Info("Completed executing file-related operations.");
        }

        private bool MoveFiles(string targetFolderName, bool isSecondaryMove)
        {
            // First get all files inside the drop folder that need to be moved over ordered by their creation time
            var folderPath = Path.Combine(OutputFolderPath, targetFolderName);
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
                        var ftpHost = (isSecondaryMove) ? FtpHostSecondary : FtpHost;
                        var ftpUserName = (isSecondaryMove) ? FtpUserNameSecondary : FtpUserName;
                        var ftpUserPassword = (isSecondaryMove) ? FtpUserPasswordSecondary: FtpUserPassword;
                        var ftpDropFolderPath = (isSecondaryMove) ? FtpDropFolderPathSecondary : FtpDropFolderPath;

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
                        var targetFolderPath = (isSecondaryMove) ? TargetFolderPathSecondary : TargetFolderPath;
                        // First remove all files 
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
            if (RunMode == RunMode.PrimaryAndSecondary)
                FeedRunService.RemoveOldFeedRuns(FeedIdSecondary, MaximumFeedRunsToKeep);
        }

        private void RemoveOldFiles()
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
            // Create the secondary target folder if necessary 
            if (RunMode == RunMode.PrimaryAndSecondary)
            {
                var secondaryFolderPath = Path.Combine(OutputFolderPath, SecondaryOutputFolderName);
                EnsureFolderExists(secondaryFolderPath);
            }
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }
    }
}
