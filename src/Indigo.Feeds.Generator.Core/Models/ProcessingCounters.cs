using System.Collections.Generic;

namespace Indigo.Feeds.Generator.Core.Models
{
    public class ProcessingCounters
    {
        public ProcessingCounters()
        {
            CustomMessages = new List<string>();
        }

        public string Identifier { get; set; }
        public int FilesCount { get; set; }
        public int NumberOfNew { get; set; }
        public int NumberOfDeleted { get; set; }
        public int NumberOfModified { get; set; }
        public int NumberOfErrored { get; set; }
        public bool AllowErrors { get; set; }
        public List<string> CustomMessages { get; }

        public int GetTotalProcessed()
        {
            return NumberOfNew + NumberOfDeleted + NumberOfModified + NumberOfErrored;
        }

        public void AddCustomMessage(string message)
        {
            CustomMessages.Add(message);
        }
    }
}
