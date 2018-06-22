using FeedGenerators.Core.Services.Abstract;
using System;
using System.Collections.Concurrent;
using System.Text;

namespace FeedGenerators.Core.Services.Concrete
{
    public class ExecutionLogLogger : IExecutionLogLogger
    {
        private readonly ConcurrentBag<string> _successfulFileNames = new ConcurrentBag<string>();
        private readonly ConcurrentBag<string> _failedFileNames = new ConcurrentBag<string>();
        private readonly ConcurrentBag<string> _customMessages = new ConcurrentBag<string>(); 
        private DateTime _startTime;
        private DateTime? _endTime;

        public bool HasError { get; set; }
 
        public void SetExecutionStartTime(DateTime startTime)
        {
            _startTime = startTime;
        }

        public void SetExecutionEndTime(DateTime? endTime)
        {
            _endTime = endTime;
        }

        public DateTime GetExecutionStartTime()
        {
            return _startTime;
        }

        public DateTime? GetExecutionEndTime()
        {
            return _endTime;
        }

        public void AddFileGenerationUpdate(string fileName, bool isSuccessful)
        {
            if (isSuccessful) 
                _successfulFileNames.Add(fileName);
            else 
                _failedFileNames.Add(fileName);
        }

        public void AddCustomMessage(string message)
        {
            _customMessages.Add(message);
        }

        public string GetExecutionLog()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(string.Format("Execution Started: {0}", _startTime));
            var endTimeText = (_endTime.HasValue) ? _endTime.ToString() : "NA";
            stringBuilder.AppendLine(string.Format("Execution Ended: {0}", endTimeText));
            if (_endTime.HasValue)
            {
                var executionTime = _endTime.Value - _startTime;
                stringBuilder.AppendLine(string.Format("Execution Length: {0} days, {1} hours, {2} minutes and {3} seconds", executionTime.Days, executionTime.Hours, executionTime.Minutes, executionTime.Seconds));    
            }

            stringBuilder.AppendLine(string.Format("Number of Files Created Successfully: {0}", _successfulFileNames.Count));
            stringBuilder.AppendLine(string.Format("Number of Files Failed: {0}", _failedFileNames.Count));
            if (_failedFileNames.Count > 0)
            {
                stringBuilder.AppendLine("Failed File Names:");
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
