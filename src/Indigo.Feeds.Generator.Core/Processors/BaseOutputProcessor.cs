using Castle.Core.Logging;
using Indigo.Feeds.Generator.Core.Enums;
using Indigo.Feeds.Generator.Core.Models;
using Indigo.Feeds.Generator.Core.Processors.Contracts;
using Indigo.Feeds.Generator.Core.Services;
using Indigo.Feeds.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Indigo.Feeds.Generator.Core.Processors
{
    public abstract class BaseOutputProcessor : IOutputProcessor
    {
        private object _locker = new object();
        private List<Thread> _watcherThreads = new List<Thread>();

        protected volatile ConcurrentBag<FileInfo> _files = new ConcurrentBag<FileInfo>();
        protected volatile bool _hasError;
        private volatile bool _watchFiles = true;

        // TODO: should be injected to make it testable
        protected static readonly bool AllowItemErrorsInFiles = ParameterUtils.GetParameter<bool>("AllowItemErrorsInFiles");
        protected static readonly int OutputFileMoveMethod = int.Parse(ParameterUtils.GetParameter<string>("OutputProcessor.OutputFileMoveMethod"));
        protected static readonly int MaximumThreadsToUseForSendingData = int.Parse(ParameterUtils.GetParameter<string>("MaximumThreadsToUseForSendingData"));
        protected static readonly int MaximumThreadsToUseForWatchers = int.Parse(ParameterUtils.GetParameter<string>("MaximumThreadsToUseForWatchers"));
        protected static readonly int NumberOfRecordsPerBatch = ParameterUtils.GetParameter<int>("OutputProcessor.NumberOfRecordsPerBatch");
        protected static readonly string OutputFolderPath = ParameterUtils.GetParameter<string>("OutputProcessor.OutputFolderPath");
        protected static readonly string DoneFileFileName = ParameterUtils.GetParameter<string>("DoneFileFileName");
        protected static readonly bool FileWatcherEnabled = ParameterUtils.GetParameter<bool>("OutputProcessor.FileWatcherEnabled");
        protected static readonly string FileNameFormat = ParameterUtils.GetParameter<string>("OutputProcessor.FileNameFormat");
        protected static readonly int PollingIntervalSeconds = ParameterUtils.GetParameter<int>("OutputProcessor.FileWatcherPollingInterval.Seconds");
        protected static readonly bool GzipFiles = ParameterUtils.GetParameter<bool>("OutputProcessor.GzipFiles");
        protected static readonly bool UseSerialization = ParameterUtils.GetParameter<bool>("OutputProcessor.UseSerialization");
        protected static readonly bool MoveDoneFile = ParameterUtils.GetParameter<bool>("OutputProcessor.MoveDoneFile");
        protected static readonly int NumberOfTrialsPerSendRequest = ParameterUtils.GetParameter<int>("OutputProcessor.NumberOfTrialsPerSendRequest");

        protected readonly IDataService _dataService;
        protected readonly ILogger _logger;
        protected readonly IFileContentProcessor _fileProcessor;
        
        public BaseOutputProcessor(
            IFileContentProcessor fileContentProcessor,
            IDataService dataService,
            ILogger logger)
        {
            _fileProcessor = fileContentProcessor;
            _dataService = dataService;
            _logger = logger;
        }

        public bool Initialize()
        {
            EnsureFolderExists(OutputFolderPath);

            // First check if done file exists
            // If so call a private method that will process all files inside output folder
            // using up to X threads, if successful, delete done file and return true. If unsuccessful, throw an exception.
            if (HasDoneFile())
            {
                ProcessWorkingFolder();
                if (_hasError)
                {
                    _logger.Error("Instruction processor encountered an error while processing instructions from the previous execution.");
                }
                else
                {
                    _logger.Info("Instruction processor processed instructions from the previous execution.");
                }
                return true;
            }
            // If not, call a private method that will instantiate a local process that will be processing files inside _files. Return false.
            else
            {
                RemoveOldFiles();

                if (FileWatcherEnabled)
                {
                    StartWatcherProcess();
                }
                return false;
            }
        }

        public void RecordOutput(OutputInstruction instruction)
        {
            var file = WriteOutputInstructionFile(instruction);
            if (file != null)
            {
                _files.Add(file);
            }
        }

        public bool FinalizeProcessing(bool isSuccessfulRun)
        {
            try
            {
                if (FileWatcherEnabled)
                {
                    // End the watcher process.
                    EndWatcherProcess();
                }

                if (!isSuccessfulRun)
                {
                    return true;
                }

                // Create the done file. 
                WriteDoneFile();
                _hasError = false;

                // Call the same private method that will spawn threads and send data, then delete done file. Return true if things are successful. 
                // Otherwise, return false. 
                ProcessWorkingFolder();

                return !_hasError;
            }
            catch (Exception exception)
            {
                _hasError = true;
                _logger.Error("Error while finalizing processing of output instructions.", exception);
                return false;
            }
        }

        public abstract ProcessingCounters CreateOutput(IDataReader reader, StringDictionary dict, string catalog, string identifier, RunType runType);

        protected abstract void ProcessAvailableInstruction(FileInfo fileInfo);

        protected abstract void PrepareDestination();

        protected abstract void CleanupDestination();

        private void ProcessWorkingFolder()
        {
            _logger.Info("Starting processing working folder.");
            if (OutputFileMoveMethod == 0)
            {
                _logger.Info("Moving files is disabled. Exiting file-related operations without any further action.");
                return;
            }

            // First clear the collection and re-populate it
            PopulateFileInfoCollectionFromWorkingFolder();
                        
            try
            {
                // Prepare the destination.
                PrepareDestination();

                // Next spawn threads that will process the files
                ProcessAvailableInstructions();

                if (MoveDoneFile && !_hasError)
                {
                    string filePath = Path.Combine(OutputFolderPath, DoneFileFileName);
                    FileInfo doneFileInstruction = new FileInfo(filePath);
                    ProcessAvailableInstruction(doneFileInstruction);
                }
            }
            catch (AggregateException ae)
            {
                _hasError = true;
                _logger.Error("Processing working folder failed", ae.Flatten());
            }
            finally
            {
                // Clean up the destination.
                CleanupDestination();
            }

            // Remove the done file
            if (!_hasError)
            {
                RemoveDoneFile();
            }

            _logger.Info("Completed processing working folder.");
        }

        private void PopulateFileInfoCollectionFromWorkingFolder()
        {
            _logger.Debug("Populating the bag will all files inside working folder.");
            lock (_locker)
            {
                _files = new ConcurrentBag<FileInfo>();
                var instructions = _fileProcessor.GetContentFiles(OutputFolderPath);
                foreach (var fileInfo in instructions)
                {
                    _files.Add(fileInfo);
                }
                _logger.InfoFormat("Completed populating the bag will all files inside working folder. There were {0} file(s).", instructions.Length);
            }
        }

        private void ProcessAvailableInstructions()
        {
            var exceptions = new ConcurrentQueue<Exception>();

            Parallel.ForEach(
                _files,
                new ParallelOptions { MaxDegreeOfParallelism = MaximumThreadsToUseForSendingData }, 
                file =>
                {
                    try
                    {
                        ProcessAvailableInstruction(file);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Enqueue(ex);
                    }
                });

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        }

        protected static int GetItemCount(OutputInstruction instruction)
        {
            return instruction.Data.Count;
        }

        #region Output Instruction file watching

        private void StartWatcherProcess()
        {
            _logger.Info("Starting file watcher process");
            if (OutputFileMoveMethod == 0)
            {
                _logger.Info("Moving files is disabled. Exiting StartWatcherProcess() without any further action.");
                return;
            }

            PrepareDestination();

            for (var ii = 0; ii < MaximumThreadsToUseForWatchers; ii++)
            {
                var watcherThread = new Thread(Watch);
                watcherThread.Start();

                // Loop until worker thread activates.
                while (!watcherThread.IsAlive) ;

                _watcherThreads.Add(watcherThread);
            }
        }

        private void EndWatcherProcess()
        {
            _watchFiles = false;

            // Use the Join method to block the current thread until the object's thread terminates
            if (_watcherThreads.Any())
            {
                foreach (var thread in _watcherThreads)
                {
                    thread.Join();
                }
            }

            CleanupDestination();

            _logger.Info("Terminated the file watchers.");
        }

        private void Watch()
        {
            while (_watchFiles)
            {
                if (!_files.IsEmpty)
                {                    
                    FileInfo fileInfo;
                    if (_files.TryTake(out fileInfo))
                    {
                        try
                        {
                            PrepareDestination();
                            _logger.DebugFormat("File found. Beginning to send {0}.", fileInfo.FullName);
                            ProcessAvailableInstruction(fileInfo);
                        }
                        catch (Exception ex)
                        {
                            // Something went wrong while processing the file, so let's put it back into the bag
                            _files.Add(fileInfo);
                            _logger.ErrorFormat(ex, "Something went wrong processing {0}. Putting it back in the queue.", fileInfo.FullName);
                        }
                    }
                }
                else
                {
                    _logger.DebugFormat("No files found - will look again in {0} seconds.", PollingIntervalSeconds);
                    Thread.Sleep(new TimeSpan(0, 0, PollingIntervalSeconds));
                }
            }

            _logger.Info("Ending file watcher process");
        }

        #endregion

        #region File/folder operations

        private FileInfo WriteOutputInstructionFile(OutputInstruction instruction)
        {
            if (instruction == null)
                throw new ArgumentException("Null instruction was provided.", "instruction");

            if (string.IsNullOrWhiteSpace(instruction.OutputLocation))
                throw new ArgumentException("An invalid output location was provided.", "OutputLocation");

            if (string.IsNullOrWhiteSpace(instruction.OutputName))
                throw new ArgumentException("An invalid output name was provided.", "OutputName");

            instruction.OutputLocation = Path.Combine(OutputFolderPath, instruction.OutputLocation);
            EnsureFolderExists(instruction.OutputLocation);

            return _fileProcessor.FileWrite(instruction);
        }

        private bool HasDoneFile()
        {
            return _fileProcessor.FileExists(Path.Combine(OutputFolderPath, DoneFileFileName));
        }

        private void WriteDoneFile()
        {
            EnsureFolderExists(OutputFolderPath);

            string filePath = Path.Combine(OutputFolderPath, DoneFileFileName);
            _fileProcessor.FileCreateEmpty(filePath);
        }

        private void RemoveDoneFile()
        {
            _fileProcessor.FileRemove(Path.Combine(OutputFolderPath, DoneFileFileName));
        }

        private void RemoveOldFiles()
        {
            EnsureFolderExists(OutputFolderPath);

            // Delete all files and folders inside output folder path in the generated xml files directory
            _fileProcessor.DirectoryDelete(OutputFolderPath);

            // If the target folders don't exist, create them too
            EnsureFolderExists(OutputFolderPath);
        }

        private void EnsureFolderExists(string folderPath)
        {
            if (!_fileProcessor.DirectoryExists(folderPath))
            {
                _fileProcessor.DirectoryCreate(folderPath);
            }
        }

        #endregion
    }
}
