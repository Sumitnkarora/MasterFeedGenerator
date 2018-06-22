using Castle.Core.Logging;
using GoogleApiPlaFeedGenerator.Json;
using Indigo.Feeds.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace GoogleApiPlaFeedGenerator.Execution
{
    public class OutputInstructionProcessor : IOutputInstructionProcessor
    {
        private const string BadDescriptionsProductIdsFileName = "ids-for-bad-descriptions.txt";
        private object _locker = new object();
        private List<Thread> _watcherThreads = new List<Thread>();

        private volatile ConcurrentBag<FileInfo> _files = new ConcurrentBag<FileInfo>();
        private volatile bool _watchFiles = true;
        private volatile bool _hasError;

        private static readonly int OutputFileMoveMethod = int.Parse(ParameterUtils.GetParameter<string>("GoogleApiPlaFeedGenerator.OutputFileMoveMethod"));
        private static readonly int MaximumThreadsToUseForApiCalls = int.Parse(ParameterUtils.GetParameter<string>("MaximumThreadsToUseForApiCalls"));
        private static readonly int MaximumThreadsToUseForWatchers = int.Parse(ParameterUtils.GetParameter<string>("MaximumThreadsToUseForWatchers"));
        private static readonly string OutputFolderPath = ParameterUtils.GetParameter<string>("GoogleApiPlaFeedGenerator.OutputFolderPath");
        private static readonly string DoneFileFileName = ParameterUtils.GetParameter<string>("DoneFileFileName");
        private static readonly int PollingIntervalSeconds = ParameterUtils.GetParameter<int>("GoogleApiPlaFeedGenerator.FileWatcherPollingInterval.Seconds");

        private readonly ILogger _logger;
        private readonly IGoogleRequestProcessor _googleRequestProcessor;

        public OutputInstructionProcessor(IGoogleRequestProcessor googleRequestProcessor, ILogger logger)
        {
            _googleRequestProcessor = googleRequestProcessor;
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
                return !_hasError;
            }
            // If not, call a private method that will instantiate a local process that will be processing files inside _files. Return false.
            else
            {
                RemoveOldFiles();

                StartWatcherProcess();
                return false;
            }
        }

        public void RecordOutputInstruction(OutputInstruction content, string folderName, string fileName)
        {
            var file = WriteOutputInstructionFile(content, folderName, fileName);
            _files.Add(file);
        }

        public bool FinalizeProcessing(bool isSuccessfulRun)
        {
            try
            {
                // End the watcher process.
                EndWatcherProcess();

                if (!isSuccessfulRun)
                    return true;

                // Create the done file. 
                WriteDoneFile();
                _hasError = false;

                // Call the same private method that will spawn threads and send data, then delete done file. Return true if things are successful. 
                // Otherwise, return false. 
                ProcessWorkingFolder();

                // Generate an output file containing the pids that had bad descriptions
                OutputIdsForErrorneousDescriptions();

                return !_hasError;
            }
            catch (Exception exception)
            {
                _hasError = true;
                _logger.Error("Error while finalizing processing of output instructions.", exception);
                return false;
            }
        }

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

            // Next spawn threads that will process the files
            Parallel.ForEach(_files, new ParallelOptions { MaxDegreeOfParallelism = MaximumThreadsToUseForApiCalls }, ProcessAvailableInstruction);

            // Remove the done file
            if (!_hasError)
                RemoveDoneFile();

            _logger.Info("Completed processing working folder.");
        }

        private void PopulateFileInfoCollectionFromWorkingFolder()
        {
            _logger.Debug("Populating the bag will all json files inside working folder.");
            lock (_locker)
            {
                _files = new ConcurrentBag<FileInfo>();
                var workingFolder = new DirectoryInfo(Path.Combine(OutputFolderPath));
                var instructions = workingFolder.GetFiles("*.json", SearchOption.AllDirectories);
                foreach (var fileInfo in instructions)
                {
                    _files.Add(fileInfo);
                }
                _logger.InfoFormat("Completed populating the bag will all json files inside working folder. There were {0} file(s).", instructions.Length);
            }
        }

        private void ProcessAvailableInstruction(FileInfo fileInfo)
        {
            _logger.DebugFormat("Picked {0} as the file to process", fileInfo.FullName);
            try
            {
                OutputInstruction instruction = null;
                using (var reader = File.OpenText(fileInfo.FullName))
                {
                    using (var jsonStreamer = new JsonTextReader(reader))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        instruction = serializer.Deserialize<OutputInstruction>(jsonStreamer);
                    }
                }

                if (ProcessInstruction(instruction))
                    RemoveFile(fileInfo);
            }
            catch (Exception exception)
            {
                _hasError = true;
                _logger.ErrorFormat("Error occurred while processing {0}.", fileInfo.FullName + " - " + exception.Message + " - " + exception.StackTrace);
            }
        }

        private bool ProcessInstruction(OutputInstruction instruction)
        {
            var result = false;
            var itemCount = instruction.Format == OutputFormat.Delete ? instruction.Deletions.Count : instruction.Updates.Count;
            var startTime = DateTime.Now;
            _logger.DebugFormat("Starting to process an instruction with type of {0} containing {1} entries.", instruction.Format, itemCount);
            if (instruction.Format == OutputFormat.Delete)
                result = ProcessDeletionInstruction(instruction);
            else
                result = ProcessUpdateInstruction(instruction);

            if (instruction.FileCount == 1)
                _logger.InfoFormat("Completed processing of the {1} instruction with file count of {2}. Elapsed time is {0}.", (DateTime.Now - startTime).ToString(@"dd\.hh\:mm\:ss"), instruction.Format, instruction.FileCount);
            else
                _logger.DebugFormat("Completed processing of the {1} instruction with file count of {2}. Elapsed time is {0}.", (DateTime.Now - startTime).ToString(@"dd\.hh\:mm\:ss"), instruction.Format, instruction.FileCount);

            return result;
        }

        private bool ProcessDeletionInstruction(OutputInstruction instruction)
        {
            if (!_googleRequestProcessor.SendDeletionBatch(instruction.Deletions))
            {
                _logger.Info("Error sending deletion batch to Google. Moving on to next.");
                _hasError = true;
                return false;
            }

            return true;
        }

        private bool ProcessUpdateInstruction(OutputInstruction instruction)
        {
            if (!_googleRequestProcessor.SendUpdateBatch(instruction.Updates))
            {
                _logger.Info("Error sending update batch to Google. Moving on to next.");
                _hasError = true;
                return false;
            }
            return true;
        }

        private void OutputIdsForErrorneousDescriptions()
        {
            try
            {
                var identifiers = _googleRequestProcessor.GetIdentifiersWithErroneousDescriptions();
                if (identifiers.Any())
                {
                    var folderPath = Path.Combine(OutputFolderPath, "BadDescriptions");
                    EnsureFolderExists(folderPath);

                    var filePath = Path.Combine(folderPath, BadDescriptionsProductIdsFileName);
                    if (File.Exists(filePath))
                        File.Delete(filePath);

                    using (var streamWriter = new StreamWriter(filePath))
                    {
                        foreach (var identifier in identifiers)
                        {
                            streamWriter.WriteLine(identifier + ",");
                        }
                    }

                    _logger.InfoFormat("{0} ids have been recorded inside {1}", identifiers.Count, filePath);
                }
            }
            catch (Exception exception)
            {
                _logger.ErrorFormat("There was an error while outputting the ids for products with bad descriptions. {0} - {1}", exception.Message, exception.StackTrace);
            }
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

            for (var ii = 0; ii < MaximumThreadsToUseForWatchers; ii++)
            {
                var watcherThread = new Thread(Watch);
                watcherThread.Start();

                // Loop until worker thread activates.
                while (!watcherThread.IsAlive);

                _watcherThreads.Add(watcherThread);
            }            
        }

        private void EndWatcherProcess()
        {
            _watchFiles = false;

            // Use the Join method to block the current thread until the object's thread terminates
            if (_watcherThreads.Any())
            {
                foreach(var thread in _watcherThreads)
                {
                    thread.Join();
                }
            }

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
                            _logger.DebugFormat("File found. Beginning to send {0} to Google.", fileInfo.FullName);
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
        private static FileInfo WriteOutputInstructionFile(OutputInstruction content, string folderName, string fileName)
        {
            if (content == null)
                throw new ArgumentException("Null content was provided.", "content");

            if (string.IsNullOrWhiteSpace(folderName))
                throw new ArgumentException("An invalid folder name was provided.", "folderName");

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("An invalid file name was provided.", "fileName");

            var folder = Path.Combine(OutputFolderPath, folderName);
            EnsureFolderExists(folder);
            var filePath = Path.Combine(folder, fileName + ".json");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    using (JsonWriter jsonWriter = new JsonTextWriter(writer))
                    {
                        var serializer = new JsonSerializer();
                        serializer.Serialize(jsonWriter, content);
                    }
                }
            }

            return new FileInfo(filePath);
        }

        private static bool HasDoneFile()
        {
            return File.Exists(Path.Combine(OutputFolderPath, DoneFileFileName));
        }

        private static void WriteDoneFile()
        {
            EnsureFolderExists(OutputFolderPath);

            using (new FileStream(Path.Combine(OutputFolderPath, DoneFileFileName), FileMode.Create))
            {
                // Nothing to do
            }
        }

        private static void RemoveDoneFile()
        {
            File.Delete(Path.Combine(OutputFolderPath, DoneFileFileName));
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
            EnsureFolderExists(OutputFolderPath);
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }

        private static void RemoveFile(FileInfo fileInfo)
        {
            File.Delete(fileInfo.FullName);
        }
        #endregion
    }
}
