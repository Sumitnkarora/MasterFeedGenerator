using Castle.Core.Logging;
using Indigo.Feeds.Generator.Azure.Models;
using Indigo.Feeds.Generator.Core.Processors.Contracts;
using Microsoft.ServiceBus.Messaging;
using System.IO;

namespace Indigo.Feeds.Generator.Azure.Processors
{
    public class ServiceBusProcessor : IDestinationProcessor<FileInfo>
    {
        private readonly ServiceBusConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly IFileContentProcessor _fileProcessor;
        private MessagingFactory _factory;
        private QueueClient _client;

        public ServiceBusProcessor(ServiceBusConfiguration configuration, IFileContentProcessor fileProcessor, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
            _fileProcessor = fileProcessor;
        }

        public bool IsOpen
        {
            get
            {
                return _client != null;
            }
        }

        public void Close()
        {
            if (_client != null)
            {
                _client.Close();
                _client = null;
            }
        }

        public void Open()
        {
            if (_factory == null)
            {
                _factory = MessagingFactory.CreateFromConnectionString(_configuration.ConnectionString);
            }
            if (_client == null)
            {
                _client = _factory.CreateQueueClient(_configuration.DestinationName);
            }
        }

        public bool Process(FileInfo fileInfo)
        {
            using (var reader = _fileProcessor.FileStream(fileInfo))
            {
                var message = new BrokeredMessage(reader)
                {
                    ContentType = _configuration.ContentType
                };
                _client.Send(message);
            }

            _logger.DebugFormat("Sent file: {0} to service bus: {1}", Path.GetFileName(fileInfo.FullName), _configuration.DestinationName);

            return true;
        }
    }
}
