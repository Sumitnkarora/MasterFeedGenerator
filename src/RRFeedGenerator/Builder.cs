using Castle.Core.Logging;
using FeedGenerators.Core.Services.Abstract;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Types;
using Indigo.Feeds.Utils;
using RrFeedGenerator.Execution;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using RRFeedGenerator.Execution.FileFeedWriter;

namespace RrFeedGenerator
{
    internal class Builder : IBuilder
    {
        private IFeedRun _currentRun;
        // This value governs the effective execution end time (e.g. for incremental runs, it dictates when the next incremental run will have to start from)
        private DateTime _effectiveExecutionEndTime;
        public ILogger Log { get; set; }
        public IFeedService FeedService { get; set; }
        public IFeedRunService FeedRunService{ get; set; }
        public IRunner Runner { get; set; }
        public IExecutionLogLogger ExecutionLogLogger { get; set; }

        private static readonly int FeedId = ParameterUtils.GetParameter<int>("FeedId");
        private static readonly int MaximumFeedRunsToKeep = ParameterUtils.GetParameter<int>("MaximumFeedRunsToKeep");
        private static readonly string FullRunParamName = ParameterUtils.GetParameter<string>("FullRunParameterName");
        private static readonly string OutputFolderPath = ParameterUtils.GetParameter<string>("RRFeedGenerator.OutputFolderPath");
        private static readonly string DoneFileFileName = ParameterUtils.GetParameter<string>("DoneFileFileName");
        //private static readonly bool EnableFtpingFiles = ParameterUtils.GetParameter<bool>("EnableFtpingFiles");
        private static readonly int OutputFileMoveMethod = ParameterUtils.GetParameter<int>("RRFeedGenerator.OutputFileMoveMethod");
        private static readonly string FtpHost = ParameterUtils.GetParameter<string>("RRFeedGenerator.FtpHost");
        private static readonly string FtpDropFolderPath = ParameterUtils.GetParameter<string>("RRFeedGenerator.FtpDropFolderPath");
        private static readonly string FtpUserName = ParameterUtils.GetParameter<string>("RRFeedGenerator.FtpUserName");
        private static readonly string FtpUserPassword = ParameterUtils.GetParameter<string>("RRFeedGenerator.FtpUserPassword");
        private static readonly int FtpBufferSize = ParameterUtils.GetParameter<int>("FtpBufferSize");
        private static readonly string TargetFolderPath = ParameterUtils.GetParameter<string>("RRFeedGenerator.TargetFolderPath");
        private static readonly bool AllowIncrementalRuns = ParameterUtils.GetParameter<bool>("AllowIncrementalRuns");
        private static readonly int IncrementalRunBufferTimeLength = ParameterUtils.GetParameter<int>("IncrementalRunBufferTimeLength");
        private static readonly string TestIncrementalRunFromDate = ParameterUtils.GetParameter<string>("TestIncrementalRunFromDate");
        private static readonly string ArchiveDirName = ParameterUtils.GetParameter<string>("ArchiveDirName");
        private static readonly bool EnableTargetDestinationCheck = ParameterUtils.GetParameter<bool>("EnableTargetDestinationCheck");

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

                // First check if the "done" file is there. If so, skip job execution and move to file-related steps
                if (HasDoneFile())
                {
                    Log.Info("Done file exists at the drop folder. Skipping job execution and moving to file operations.");
                    ExecuteFileOperations();

                    return;
                }

                // Check if the target destination contains any files, if so exit.
                if (!IsTargetDestinationClean())
                {
                    Log.Info("Target destination has files. Exiting application without any processing.");
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

                // First clean all the files inside the output folder (in case the previous run failed)
                RemoveOldFiles();
                if (!enforceFullRun && !AllowIncrementalRuns)
                    enforceFullRun = true;

                _currentRun = FeedRunService.GetOrCreateActiveFeedRun(feed.FeedId, enforceFullRun, AllowIncrementalRuns);
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
                    Log.InfoFormat("Executing an incremental run with an effective start date of {0}", effectiveStartTime);

                Runner.Initialize(ExecutionLogLogger, _currentRun.FeedId, _currentRun.FeedRunType == FeedRunType.Incremental, fromTime, effectiveStartTime, _effectiveExecutionEndTime);
                ExecutionLogLogger = Runner.Execute();
                if (ExecutionLogLogger.HasError && (_currentRun.FeedRunType == FeedRunType.Incremental || !RRFeedGenerator.Execution.FileFeedWriter.Constants.AllowItemErrorsInFiles))
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

        private void HandleSuccess()
        {
            _currentRun.DateCompleted = _effectiveExecutionEndTime;
            _currentRun.LastExecutionStartDate = ExecutionLogLogger.GetExecutionStartTime();
            _currentRun.LastExecutionCompletedDate = ExecutionLogLogger.GetExecutionEndTime();
            _currentRun.ExecutionLog = ExecutionLogLogger.GetExecutionLog();
            FeedRunService.EndFeedRun(_currentRun, true);

            // Write out the new "done" file
            WriteDoneFile();

            // Execute file-related operations
            ExecuteFileOperations();

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

            // First get all files inside the drop folder that need to be moved over ordered by their creation time
            var outputFolderInfo = new DirectoryInfo(OutputFolderPath);
            var pendingFiles = outputFolderInfo.GetFiles().Where(f =>
            {
                var name = Path.GetFileName(f.FullName);
                return !string.IsNullOrWhiteSpace(name) && !name.Equals(DoneFileFileName);
            }).OrderBy(f => f.CreationTime);


            if (pendingFiles.Any())
            {
                if (OutputFileMoveMethod == 1)
                {
                    try
                    {
                        foreach (var pendingFile in pendingFiles)
                        {
                            try
                            {
                                var fileName = Path.GetFileName(pendingFile.FullName);
                                Log.InfoFormat("Starting to upload {0}", fileName);
                                using (var fs = File.OpenRead(pendingFile.FullName))
                                {
                                    var ftpClient =
                                    (FtpWebRequest)
                                        WebRequest.Create(FtpHost + "/" + CleanFtpFolderPath(FtpDropFolderPath) +
                                                          fileName);
                                    ftpClient.Credentials = new NetworkCredential(FtpUserName, FtpUserPassword);
                                    ftpClient.Method = WebRequestMethods.Ftp.UploadFile;
                                    ftpClient.KeepAlive = true;
                                    ftpClient.Timeout = -1;

                                    var rs = ftpClient.GetRequestStream();
                                    fs.CopyTo(rs);
                                    rs.Close();

                                    //ftpClient.ContentLength = pendingFile.Length;
                                    //var buffer = new byte[FtpBufferSize];
                                    //var totalBytes = (int)pendingFile.Length;
                                    //var fs = pendingFile.OpenRead();
                                    
                                    //while (totalBytes > 0)
                                    //{
                                    //    var bytes = fs.Read(buffer, 0, buffer.Length);
                                    //    rs.Write(buffer, 0, bytes);
                                    //    totalBytes = totalBytes - bytes;
                                    //}

                                    //fs.Close();
                                    //rs.Close();
                                    var uploadResponse = (FtpWebResponse)ftpClient.GetResponse();
                                    var statusDescription = uploadResponse.StatusDescription;
                                    uploadResponse.Close();
                                    Log.InfoFormat("Completed uploading {0} with status code of {1}", fileName, statusDescription);
                                }
                                
                            }
                            catch (Exception ex)
                            {
                                Log.Error("File upload failed!!! Exiting application.", ex);
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("File upload failed!!! Exiting application.", ex);
                        return;
                    }
                }
                // Copy files
                else
                {
                    foreach (var pendingFile in pendingFiles)
                    {
                        File.Copy(pendingFile.FullName, Path.Combine(TargetFolderPath, Path.GetFileName(pendingFile.FullName)));
                    	Log.InfoFormat("Completed moving file {0}.", Path.GetFileName(pendingFile.FullName));
					}
                }
            }

            // Remove the done file
            File.Delete(Path.Combine(OutputFolderPath, DoneFileFileName));
            Log.Info("Completed executing file-related operations.");
        }

        private static string CleanFtpFolderPath(string ftpDropFolderPath)
        {
            if (string.IsNullOrWhiteSpace(ftpDropFolderPath))
            {
                return string.Empty;
            }

            var path = ftpDropFolderPath.Trim();

            if (path.Substring(0, 1).Equals("/", StringComparison.Ordinal))
            {
                path = path.Substring(1);
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (!path.Substring(path.Length - 1, 1).Equals("/", StringComparison.Ordinal))
            {
                path += "/";
            }

            return path;
        }

        private void RemoveOldJobs()
        {
            FeedRunService.RemoveOldFeedRuns(FeedId, MaximumFeedRunsToKeep);
        }

        private bool IsTargetDestinationClean()
        {
            if (!EnableTargetDestinationCheck || OutputFileMoveMethod == 0)
                return true;

            if (OutputFileMoveMethod == 1)
            {
                try
                {
                    Log.InfoFormat("Connecting to {0} using FTP.", FtpHost);
                    var request = (FtpWebRequest)WebRequest.Create(FtpHost);
                    request.Credentials = new NetworkCredential(FtpUserName, FtpUserPassword);
                    request.Method = WebRequestMethods.Ftp.ListDirectory;

                    using (var response = (FtpWebResponse)request.GetResponse())
                    using (var streamReader = new StreamReader(response.GetResponseStream()))
                    {
                        var content = new List<string>();

                        var line = streamReader.ReadLine();
                        while (!string.IsNullOrEmpty(line))
                        {
                            content.Add(line);
                            line = streamReader.ReadLine();
                        }

                        streamReader.Close();

                        if (content.Count == 0)
                            return true;

                        if (content.Count > 1)
                            return false;

                        // In the ftp directory listing, ensure there is (at most) one entry that is a 
                        // directory with the name in the given AppSetting.
                        
                        var result = content[0].Substring(0, 1).Equals("d", StringComparison.Ordinal)
                                      &&
                                      content[0].TrimEnd().EndsWith(ArchiveDirName);

                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("There was an issue checking for existing files on the FTP location.", ex);
                    return false;
                }
            }

            if (OutputFileMoveMethod == 2)
            {
                try
                {
                    var targetFolderInfo = new DirectoryInfo(TargetFolderPath);
                    return !targetFolderInfo.GetFiles().Any();
                }
                catch (Exception ex)
                {
                    Log.Error("There was an issue checking for existing files on the target folder.", ex);
                    return false;
                }
            }

            return true;
        }

        private void RemoveOldFiles()
        {
            if (!Directory.Exists(OutputFolderPath))
                Directory.CreateDirectory(OutputFolderPath);

            // Delete all files in the generated xml files directory
            var directoryInfo = new DirectoryInfo(OutputFolderPath);
            foreach (var file in directoryInfo.GetFiles())
            {
                file.Delete();
            }
        }
    }
}
