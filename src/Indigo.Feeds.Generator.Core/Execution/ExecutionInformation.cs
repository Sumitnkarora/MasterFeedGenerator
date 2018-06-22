using Indigo.Feeds.Generator.Core.Models;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace Indigo.Feeds.Generator.Core.Execution
{
    public class ExecutionInformation
    {
        private readonly ConcurrentBag<string> _successfulFileNames = new ConcurrentBag<string>();
        private readonly ConcurrentBag<string> _failedFileNames = new ConcurrentBag<string>();
        private readonly ConcurrentBag<string> _customMessages = new ConcurrentBag<string>();
        private volatile bool _hasError;
        private volatile int _recordsPerFile;
        private int _recordsNew;
        private int _recordsDeleted;
        private int _recordsModified;
        private int _recordsErrored;
        private int _filesCount;

        public int RecordsPerFile
        {
            get
            {
                return _recordsPerFile;
            }
            set
            {
                _recordsPerFile = value;
            }
        }

        public bool HasError
        {
            get
            {
                return _hasError;
            }
            set
            {
                _hasError = value;
            }
        }

        public int TotalRecordsNew
        {
            get
            {
                return _recordsNew;
            }
        }

        public int TotalRecordsModified
        {
            get
            {
                return _recordsModified;
            }
        }

        public int TotalRecordsDeleted
        {
            get
            {
                return _recordsDeleted;
            }
        }

        public int TotalRecordsErrored
        {
            get
            {
                return _recordsErrored;
            }
        }

        public int TotalNumberOfSuccessFiles
        {
            get
            {
                if (!HasError && _filesCount > 0)
                {
                    return _filesCount;
                }
                return _successfulFileNames.Count;
            }
        }

        public int TotalNumberOfFailedFiles
        {
            get
            {
                if (HasError && _filesCount > 0)
                {
                    return _filesCount;
                }
                return _failedFileNames.Count;
            }
        }

        public void AddFileGenerationUpdate(ProcessingCounters processingCounters)
        {
            if (!processingCounters.AllowErrors && processingCounters.NumberOfErrored > 0)
            {
                _failedFileNames.Add(processingCounters.Identifier);
            }
            else
            {
                _successfulFileNames.Add(processingCounters.Identifier);
            }
            AddNewRecords(processingCounters.NumberOfNew);
            AddModifiedRecords(processingCounters.NumberOfModified);
            AddDeletedRecords(processingCounters.NumberOfDeleted);
            AddErroredRecords(processingCounters.NumberOfErrored);
            AddFileCount(processingCounters.FilesCount);
            if (processingCounters.CustomMessages != null)
            {
                foreach (var customMessage in processingCounters.CustomMessages)
                {
                    AddCustomMessage(customMessage);
                }
            }
        }

        public void AddFileGenerationUpdate(string fileName, bool isSuccessful)
        {
            AddFileCount(1);
            if (isSuccessful)
                _successfulFileNames.Add(fileName);
            else
                _failedFileNames.Add(fileName);
        }

        public void AddCustomMessage(string message)
        {
            _customMessages.Add(message);
        }

        public string[] GetCustomMessages()
        {
            return _customMessages.ToArray();
        }

        /// <summary>
        /// Adds <paramref name="count"/> to new records.
        /// </summary>
        /// <param name="count">Count to add.</param>
        /// <returns>Current count for new records.</returns>
        public int AddNewRecords(int count)
        {
            return Interlocked.Add(ref _recordsNew, count);
        }

        /// <summary>
        /// Adds <paramref name="count"/> to modified records.
        /// </summary>
        /// <param name="count">Count to add.</param>
        /// <returns>Current count for modified records.</returns>
        public int AddModifiedRecords(int count)
        {
            return Interlocked.Add(ref _recordsModified, count);
        }

        /// <summary>
        /// Adds <paramref name="count"/> to deleted records.
        /// </summary>
        /// <param name="count">Count to add.</param>
        /// <returns>Current count for deleted records.</returns>
        public int AddDeletedRecords(int count)
        {
            return Interlocked.Add(ref _recordsDeleted, count);
        }

        /// <summary>
        /// Adds <paramref name="count"/> to errored records.
        /// </summary>
        /// <param name="count">Count to add.</param>
        /// <returns>Current count for errored records.</returns>
        public int AddErroredRecords(int count)
        {
            return Interlocked.Add(ref _recordsErrored, count);
        }

        /// <summary>
        /// Adds <paramref name="count"/> to file count.
        /// </summary>
        /// <param name="count">Count to add.</param>
        /// <returns>Current count for files.</returns>
        public int AddFileCount(int count)
        {
            return Interlocked.Add(ref _filesCount, count);
        }

        public string GetExecutionLog()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine(string.Format("Number of Files Created Successfully: {0}", TotalNumberOfSuccessFiles));
            stringBuilder.AppendLine(string.Format("Number of Files Failed: {0}", TotalNumberOfFailedFiles));
            stringBuilder.AppendFormat(
                "New/Modified record count: {0}, Error record count: {1}, Deleted record count: {2}.",
                TotalRecordsNew + TotalRecordsModified,
                TotalRecordsErrored,
                TotalRecordsDeleted).AppendLine();
            if (_failedFileNames.Count > 0)
            {
                stringBuilder.AppendLine("Failed:");
                foreach (var fileName in _failedFileNames)
                {
                    stringBuilder.AppendLine(fileName);
                }
            }

            if (_customMessages.Count > 0)
            {
                stringBuilder.AppendLine("Messages:");
                foreach (var fileName in _customMessages)
                {
                    stringBuilder.AppendLine(fileName);
                }
            }

            return stringBuilder.ToString();
        }
    }
}
