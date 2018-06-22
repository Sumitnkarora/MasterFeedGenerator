using Castle.Core.Logging;
using Indigo.Feeds.Generator.Core.Models;
using Indigo.Feeds.Generator.Core.Processors.Contracts;
using Renci.SshNet;
using System;
using System.IO;

namespace Indigo.Feeds.Generator.Core.Processors
{
    public class SftpProcessor : IDestinationProcessor<FileInfo>, IDisposable
    {
        private readonly FtpConfiguration _ftpConfiguration;
        private readonly ILogger _logger;
        private readonly IFileContentProcessor _fileProcessor;
        private SftpClient _sftpClient;

        public SftpProcessor(FtpConfiguration configuration, IFileContentProcessor fileProcessor, ILogger logger)
        {
            _ftpConfiguration = configuration;
            _fileProcessor = fileProcessor;
            _logger = logger;
        }

        public bool IsOpen
        {
            get
            {
                if (_sftpClient == null)
                {
                    return false;
                }
                return _sftpClient.IsConnected;
            }
        }

        public void Close()
        {
            if (_sftpClient != null)
            {
                if (_sftpClient.IsConnected)
                {
                    _sftpClient.Disconnect();
                }
                _sftpClient.Dispose();
            }
        }

        public void Dispose()
        {
            Close();
        }

        public void Open()
        {
            if (_sftpClient == null)
            {
                _sftpClient = new SftpClient(_ftpConfiguration.Host, _ftpConfiguration.UserName, _ftpConfiguration.UserPassword);
                _sftpClient.BufferSize = _ftpConfiguration.BufferSize;
            }
            _sftpClient.Connect();
            _logger.InfoFormat("Connected to {0} using SFTP.", _ftpConfiguration.Host);
            if (!string.IsNullOrEmpty(_ftpConfiguration.DropFolderPath))
            {
                _sftpClient.ChangeDirectory(_ftpConfiguration.DropFolderPath);
                _logger.InfoFormat("Changed working ftp directory to {0}", _ftpConfiguration.DropFolderPath);
            }
        }

        public bool Process(FileInfo fileInfo)
        {
            try
            {
                if (!_sftpClient.IsConnected)
                {
                    _sftpClient.Connect();
                }

                using (var fileStream = _fileProcessor.FileStream(fileInfo))
                {
                    var fileName = Path.GetFileName(fileInfo.FullName);
                    _logger.DebugFormat("Starting to upload {0}", fileName);
                    _sftpClient.UploadFile(fileStream, fileName);
                    _logger.DebugFormat("Completed uploading {0}", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("File upload failed!!!", ex);
                return false;
            }

            return true;
        }
    }
}
