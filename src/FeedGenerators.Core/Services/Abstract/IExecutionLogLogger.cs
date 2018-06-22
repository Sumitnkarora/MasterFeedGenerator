using System;

namespace FeedGenerators.Core.Services.Abstract
{
    public interface IExecutionLogLogger
    {
        bool HasError { get; set; } 

        void SetExecutionStartTime(DateTime startTime);
        void SetExecutionEndTime(DateTime? endTime);
        void AddFileGenerationUpdate(string fileName, bool isSuccessful);
        void AddCustomMessage(string message);
        string GetExecutionLog();
        DateTime GetExecutionStartTime();
        DateTime? GetExecutionEndTime(); 
    }
}
