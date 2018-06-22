using Castle.Core.Logging;
using Indigo.Feeds.Generator.Core.Models;
using Indigo.Feeds.Generator.Core.Processors.Contracts;
using System.IO;

namespace Indigo.Feeds.Generator.Core.Processors
{
    public class NetworkFolderProcessor : IDestinationProcessor<FileInfo>
    {
        private readonly NetworkConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly IFileContentProcessor _fileProcessor;

        public NetworkFolderProcessor(NetworkConfiguration configuration, IFileContentProcessor fileProcessor, ILogger logger)
        {
            _configuration = configuration;
            _fileProcessor = fileProcessor;
            _logger = logger;
        }

        public bool IsOpen
        {
            get
            {
                return true;
            }
        }

        public void Close()
        {
        }
        
        public void Open()
        {
        }

        public bool Process(FileInfo fileInfo)
        {
            var destination = Path.Combine(_configuration.Destination, Path.GetFileName(fileInfo.FullName));
            _fileProcessor.FileCopy(
                fileInfo.FullName,
                destination,
                _configuration.AllowOverwrite);
            
            _logger.DebugFormat("Completed copy file {0} to {1}", Path.GetFileName(fileInfo.FullName), destination);
            return true;
        }
    }
}
