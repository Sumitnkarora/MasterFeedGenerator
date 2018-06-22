using BVRatingImporter.Execution;
using Castle.Core.Logging;
using FeedGenerators.Core.Services.Abstract;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
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

namespace BVRatingImporter
{
    internal class Builder : IBuilder
    {
        private IFeedRun _currentRun;
        // This value governs the effective execution end time (e.g. for incremental runs, it dictates when the next incremental run will have to start from)
        private DateTime _effectiveExecutionEndTime;
        public ILogger Log { get; set; }
        public IFeedService FeedService { get; set; }
        public IFeedRunService FeedRunService { get; set; }
        public IRunner Runner { get; set; }
        public IExecutionLogLogger ExecutionLogLogger { get; set; }

        private static readonly int FeedId = ParameterUtils.GetParameter<int>("FeedId");
        private static readonly int MaximumFeedRunsToKeep = ParameterUtils.GetParameter<int>("MaximumFeedRunsToKeep");
        private static readonly string FullRunParamName = ParameterUtils.GetParameter<string>("FullRunParameterName");
        private static readonly string DownloadFolderPath = ParameterUtils.GetParameter<string>("BVRatingImporter.DownloadFolderPath");
        private static readonly string FtpHost = ParameterUtils.GetParameter<string>("BVRatingImporter.FtpHost");
        private static readonly string FtpDropFolderPath = ParameterUtils.GetParameter<string>("BVRatingImporter.FtpDropFolderPath");
        private static readonly string FtpUserName = ParameterUtils.GetParameter<string>("BVRatingImporter.FtpUserName");
        private static readonly string FtpUserPassword = ParameterUtils.GetParameter<string>("BVRatingImporter.FtpUserPassword");
        private static readonly int FtpBufferSize = ParameterUtils.GetParameter<int>("FtpBufferSize");
        private static readonly bool AllowIncrementalRuns = ParameterUtils.GetParameter<bool>("AllowIncrementalRuns");
        private static readonly int IncrementalRunBufferTimeLength = ParameterUtils.GetParameter<int>("IncrementalRunBufferTimeLength");
        private static readonly string SourceFtpZipFileName = ParameterUtils.GetParameter<string>("SourceFtpZipFileName");
        private static readonly string RatingsXmlFileName = ParameterUtils.GetParameter<string>("RatingsXmlFileName");
        private static readonly string TestIncrementalRunFromDate = ParameterUtils.GetParameter<string>("TestIncrementalRunFromDate");
        private static readonly bool NoFtpDownload = ParameterUtils.GetParameter<bool>("BVRatingImporter.NoFtpDownload");

        public void Build(string[] args)
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

                var enforceFullRun = args.ToList().Contains(FullRunParamName, StringComparer.InvariantCultureIgnoreCase);

                Log.Info("Execution started!");
                var feed = FeedService.GetFeed(FeedId);

                // If the feed is paused, terminate execution
                if (feed.IsPaused)
                {
                    Log.Info("This feed is in paused state. Exiting application without any processing");
                    return;
                }

                if (!enforceFullRun && !AllowIncrementalRuns)
                {
                    enforceFullRun = true;
                }

                _currentRun = FeedRunService.GetOrCreateActiveFeedRun(feed.FeedId, enforceFullRun, AllowIncrementalRuns);

                // First clean all the files inside the download folder (in case the previous run failed)
                RemoveOldFiles();

                // Download the file
                GetRatingsFileByFtp();
                // Unzip the file 
                UnzipRatingsFile();

                _effectiveExecutionEndTime = DateTime.Now;
                var fromTime = _currentRun.DateStarted;
                var effectiveStartTime = fromTime.AddHours(-IncrementalRunBufferTimeLength);
                if (_currentRun.FeedRunType == FeedRunType.Incremental && !string.IsNullOrWhiteSpace(TestIncrementalRunFromDate))
                {
                    var time = DateTime.Parse(TestIncrementalRunFromDate);
                    fromTime = time;
                    effectiveStartTime = time.AddHours(-IncrementalRunBufferTimeLength);
                    Log.InfoFormat("Found TestIncrementalRunFromDate. Using its value {0}", TestIncrementalRunFromDate);
                }

                if (_currentRun.FeedRunType == FeedRunType.Incremental)
                {
                    Log.InfoFormat("Executing an incremental run with an effective start date of {0}",
                        effectiveStartTime);
                }

                Runner.Initialize(ExecutionLogLogger, _currentRun.FeedId, _currentRun.FeedRunType == FeedRunType.Incremental, fromTime, effectiveStartTime, _effectiveExecutionEndTime);
                ExecutionLogLogger = Runner.Execute();
                if (ExecutionLogLogger.HasError)
                {
                    Log.Error("Execution failed!!!");
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

        private void UnzipRatingsFile()
        {
            if (NoFtpDownload)
            {
                return;
            }

            // Use a 4K buffer. Any larger is a waste.    
            byte[] dataBuffer = new byte[4096];

            using (System.IO.Stream fs = new FileStream(Path.Combine(DownloadFolderPath, SourceFtpZipFileName), FileMode.Open, FileAccess.Read))
            {
                using (GZipInputStream gzipStream = new GZipInputStream(fs))
                {

                    // Change this to your needs
                    string fnOut = Path.Combine(Path.Combine(DownloadFolderPath, RatingsXmlFileName));

                    using (FileStream fsOut = File.Create(fnOut))
                    {
                        StreamUtils.Copy(gzipStream, fsOut, dataBuffer);
                    }
                }
            }
        }

        private void HandleSuccess()
        {
            _currentRun.DateCompleted = _effectiveExecutionEndTime;
            _currentRun.LastExecutionStartDate = ExecutionLogLogger.GetExecutionStartTime();
            _currentRun.LastExecutionCompletedDate = ExecutionLogLogger.GetExecutionEndTime();
            _currentRun.ExecutionLog = ExecutionLogLogger.GetExecutionLog();
            FeedRunService.EndFeedRun(_currentRun, true);

            // Remove old jobs
            RemoveOldJobs();
        }

        private void HandleExit()
        {
            // Set the current run to be incomplete
            var feedRun = FeedRunService.GetLatestRun(FeedId);
            feedRun.LastExecutionStartDate = ExecutionLogLogger.GetExecutionStartTime();
            ExecutionLogLogger.AddCustomMessage("This execution failed. Terminating execution.");
            feedRun.ExecutionLog = ExecutionLogLogger.GetExecutionLog();
            FeedRunService.EndFeedRun(feedRun, false);

            Log.Info("Executed HandleExit()!");
        }


        private void GetRatingsFileByFtp()
        {
            if (NoFtpDownload)
            {
                return;
            }

            Log.Info("Starting GetRatingsFileByFtp.");
            try
            {
                Log.InfoFormat("Starting to download {0}", SourceFtpZipFileName);
                Log.InfoFormat("Connecting to {0} using SFTP.", FtpHost);
                using (var sftpClient = new SftpClient(FtpHost, FtpUserName, FtpUserPassword))
                {
                    sftpClient.Connect();
                    Log.InfoFormat("Connected to {0} using SFTP.", FtpHost);
                    if (!string.IsNullOrEmpty(FtpDropFolderPath))
                    {
                        sftpClient.ChangeDirectory(FtpDropFolderPath);
                        Log.InfoFormat("Changed working ftp directory to {0}", FtpDropFolderPath);
                    }
                    // Ensure that the connection is kept alive...
                    sftpClient.KeepAliveInterval = new TimeSpan(1, 24, 0, 0);
                    sftpClient.BufferSize = Convert.ToUInt32(FtpBufferSize);

                    var fullDownloadFilePath = Path.Combine(DownloadFolderPath, SourceFtpZipFileName);
                    Log.InfoFormat("Starting downloading {0} to {1}.", SourceFtpZipFileName, fullDownloadFilePath);
                    using (var outputStream = new FileStream(fullDownloadFilePath, FileMode.Create))
                    {
                        sftpClient.DownloadFile(SourceFtpZipFileName, outputStream);
                    }
                    Log.InfoFormat("Completed downloading {0}.", SourceFtpZipFileName);
                }
            }
            catch (Exception ex)
            {
                Log.Error("File download failed!!! Exiting application.", ex);
                throw;
            }
        }

        private void RemoveOldJobs()
        {
            FeedRunService.RemoveOldFeedRuns(FeedId, MaximumFeedRunsToKeep);
        }

        private void RemoveOldFiles()
        {
            if (!Directory.Exists(DownloadFolderPath))
            {
                Directory.CreateDirectory(DownloadFolderPath);
            }
                

            if (NoFtpDownload)
            {
                return;
            }

            // Delete all files in the generated xml files directory
            var directoryInfo = new DirectoryInfo(DownloadFolderPath);
            foreach (var file in directoryInfo.GetFiles())
            {
                file.Delete();
            }
        }
    }
}
