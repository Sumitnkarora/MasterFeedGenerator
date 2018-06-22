using Castle.Core.Logging;
using Indigo.Feeds.Generator.Core.Processors.Contracts;
using Indigo.Feeds.Generator.Core.Services;
using System;
using System.IO;
using System.Collections.Specialized;
using System.Data;
using System.Xml;
using Indigo.Feeds.Generator.Core.Utils;
using Indigo.Feeds.Generator.Core.Models;
using System.IO.Compression;
using System.Xml.Serialization;
using Indigo.Feeds.Generator.Core.Enums;

namespace Indigo.Feeds.Generator.Core.Processors
{
    /// <summary>
    /// Output processor supporting xml data.
    /// </summary>
    public class OutputXmlProcessor : BaseOutputProcessor
    {
        private readonly IDestinationProcessor<FileInfo> _destinationProcessor;
        private readonly IXmlFileContentProcessor _xmlFileProcessor;

        public OutputXmlProcessor(
            IXmlFileContentProcessor fileContentProcessor,
            IDestinationProcessor<FileInfo> destinationProcessor,
            IDataService dataService,
            ILogger logger)
            : base(fileContentProcessor, dataService, logger)
        {
            _destinationProcessor = destinationProcessor;
            _xmlFileProcessor = fileContentProcessor;
        }

        public override ProcessingCounters CreateOutput(IDataReader reader, StringDictionary dict, string catalog, string identifier, RunType runType)
        {
            var counter = new ProcessingCounters() { AllowErrors = AllowItemErrorsInFiles, Identifier = identifier };
            var batchCount = 1;
            XmlWriter xmlWriter = null;
            GZipStream gZipStream = null;
            XmlSerializer serializer = null;
            string rootName = _dataService.GetXmlRoot(runType);
            if (UseSerialization)
            {
                serializer = _xmlFileProcessor.GetSerializer(_dataService.GetDataType(), string.Empty);
            }

            int numberInBatch = 0;
            int recordsRetrieved = 0;
            FileInfo currentFile = null;
            ExportData previousRecord = null;
            OutputFormat previousRecordOutput = OutputFormat.Update;
            while (reader.Read())
            {
                recordsRetrieved++;
                object sourceId = null;
                if (dict["sourceId"] != null)
                {
                    sourceId = reader[dict["sourceId"]];
                }
                _logger.DebugFormat("{0}::Processing record [{1}]: {2}", identifier, recordsRetrieved, sourceId?.ToString());
                try
                {
                    if (xmlWriter == null)
                    {
                        numberInBatch = 0;
                        currentFile = StartBatchFile(identifier, runType, batchCount, rootName, ref gZipStream, ref xmlWriter);
                    }

                    var record = _dataService.GetData(dict, reader, catalog, runType);
                    if (record == null || record.ExportData == null)
                    {
                        var message = string.Format("[{1}]Record {0} wasn't found, so is being treated as an erroneous item.", sourceId, identifier);
                        _logger.Debug(message);
                        counter.NumberOfErrored++;
                        counter.AddCustomMessage(message);
                        continue;
                    }

                    if (previousRecord != null)
                    {
                        if (!string.IsNullOrEmpty(record.ExportData.SourceId) &&
                            !string.IsNullOrEmpty(previousRecord.SourceId) &&
                            string.Equals(record.ExportData.SourceId, previousRecord.SourceId, StringComparison.InvariantCultureIgnoreCase))
                        {
                            _logger.DebugFormat("Record {0} matches previous. Merge them.", record.ExportData.SourceId);
                            previousRecord = _dataService.MergeData(previousRecord, record.ExportData);
                        }
                        else
                        {
                            WriteToBatch(xmlWriter, serializer, previousRecord);
                            UpdateCounters(ref counter, previousRecordOutput);
                            numberInBatch++;
                            previousRecord = record.ExportData;
                            previousRecordOutput = record.IsNew ? OutputFormat.Insert : record.IsDeleted ? OutputFormat.Delete : OutputFormat.Update;
                        }
                    }
                    else
                    {
                        previousRecordOutput = record.IsNew ? OutputFormat.Insert : record.IsDeleted ? OutputFormat.Delete : OutputFormat.Update;
                        previousRecord = record.ExportData;
                    }

                    if (numberInBatch >= NumberOfRecordsPerBatch)
                    {
                        xmlWriter = FinishBatchFile(xmlWriter, gZipStream, rootName);
                        batchCount++;
                        if (currentFile != null)
                        {
                            _files.Add(currentFile);
                            counter.FilesCount++;
                            currentFile = null;
                        }
                    }
                }
                catch (Exception exception)
                {
                    counter.NumberOfErrored++;
                    var errorMessage = $"An error was encountered while retrieving data for item {sourceId} " +
                                            previousRecord != null ? $"and adding xmlelement for the item {previousRecord.SourceId}" : $";catalog:{catalog},Message:{exception.Message}";
                    counter.AddCustomMessage(errorMessage);
                    _logger.Error(errorMessage);
                    _logger.DebugFormat("Error stack trace: {0}", exception);
                }
            }

            if (previousRecord != null)
            {
                try
                {
                    if (xmlWriter == null)
                    {
                        numberInBatch = 0;
                        currentFile = StartBatchFile(identifier, runType, batchCount, rootName, ref gZipStream, ref xmlWriter);
                    }

                    WriteToBatch(xmlWriter, serializer, previousRecord);
                    UpdateCounters(ref counter, previousRecordOutput);
                    numberInBatch++;
                }
                catch (Exception exception)
                {
                    counter.NumberOfErrored++;
                    var errorMessage = string.Format("Cannot process the item. Id:{0};catalog:{1},Message:{2}", previousRecord.SourceId, catalog, exception.Message);
                    counter.AddCustomMessage(errorMessage);
                    _logger.Error(errorMessage);
                    _logger.DebugFormat("Error stack trace: {0}", exception);
                }
            }

            if (xmlWriter != null)
            {
                xmlWriter = FinishBatchFile(xmlWriter, gZipStream, rootName);

                if (currentFile != null)
                {
                    _files.Add(currentFile);
                    currentFile = null;
                    counter.FilesCount++;
                }
            }

            _logger.InfoFormat(
                "[ExecuteFeedUpdate] {0} completed new record count: {1}, error record count: {2}, changed record count: {3}, deleted record count: {4}.",
                identifier,
                counter.NumberOfNew,
                counter.NumberOfErrored,
                counter.NumberOfModified,
                counter.NumberOfDeleted);

            if (!AllowItemErrorsInFiles && counter.NumberOfErrored > 0)
            {
                _hasError = true;
            }

            return counter;
        }

        private void UpdateCounters(ref ProcessingCounters counter, OutputFormat recordOutput)
        {
            if (recordOutput == OutputFormat.Insert)
            {
                counter.NumberOfNew++;
            }
            else if (recordOutput == OutputFormat.Delete)
            {
                counter.NumberOfDeleted++;
            }
            else
            {
                counter.NumberOfModified++;
            }
        }

        private void WriteToBatch(XmlWriter xmlWriter, XmlSerializer serializer, ExportData previousRecord)
        {
            _logger.DebugFormat("Record {0} write data to the batch.", previousRecord.SourceId);
            if (UseSerialization)
            {
                serializer.Serialize(xmlWriter, previousRecord, previousRecord.XmlNamespaces);
            }
            else
            {
                var data = _dataService.ConvertToXml(previousRecord);
                data.WriteTo(xmlWriter);
            }
        }

        private static XmlWriter FinishBatchFile(XmlWriter xmlWriter, GZipStream gZipStream, string rootName)
        {
            EndXmlDocument(xmlWriter, rootName);
            xmlWriter.Close();
            if (gZipStream != null)
            {
                gZipStream.Close();
            }
            xmlWriter.Dispose();
            xmlWriter = null;
            return xmlWriter;
        }

        private FileInfo StartBatchFile(string identifier, RunType runType, int batchCount, string rootName, ref GZipStream gZipStream, ref XmlWriter xmlWriter)
        {
            var filePath = GeneratorHelper.GetFilePath(identifier, batchCount, OutputFolderPath, FileNameFormat);
            if (GzipFiles)
            {
                filePath = filePath + ".gz";
                gZipStream = new GZipStream(_xmlFileProcessor.FileCreate(filePath), CompressionMode.Compress);
                xmlWriter = _xmlFileProcessor.FileWriterCreate(gZipStream);
            }
            else
            {
                xmlWriter = _xmlFileProcessor.FileWriterCreate(filePath);
            }

            StartXml(runType, xmlWriter, rootName);

            return new FileInfo(filePath);
        }

        protected override void CleanupDestination()
        {
            _destinationProcessor.Close();
        }

        protected override void PrepareDestination()
        {
            _destinationProcessor.Open();
        }

        protected override void ProcessAvailableInstruction(FileInfo fileInfo)
        {
            _logger.DebugFormat("Picked {0} as the file to process", fileInfo.FullName);
            try
            {
                var batchSuccess = ProcessInstruction(fileInfo);

                if (batchSuccess)
                {
                    _fileProcessor.FileRemove(fileInfo);
                }
            }
            catch (Exception exception)
            {
                _hasError = true;
                _logger.ErrorFormat("Error occurred while processing {0}.", fileInfo.FullName + " - " + exception.Message + " - " + exception.StackTrace);
            }
        }

        protected string StartXml(RunType runType, XmlWriter xmlWriter, string rootName)
        {
            xmlWriter.WriteStartDocument();
            if (!string.IsNullOrEmpty(rootName))
            {
                xmlWriter.WriteStartElement(rootName);
            }

            return rootName;
        }

        protected static void EndXmlDocument(XmlWriter xmlWriter, string rootName)
        {
            if (!string.IsNullOrEmpty(rootName))
            {
                xmlWriter.WriteEndElement();
            }
            xmlWriter.WriteEndDocument();
        }

        private bool ProcessInstruction(FileInfo fileInfo)
        {
            var startTime = DateTime.Now;
            _logger.DebugFormat("Starting to process instruction {0}.", fileInfo.FullName);

            var processingResult = false;
            var trialsCount = 0;
            while (trialsCount < NumberOfTrialsPerSendRequest)
            {
                trialsCount++;
                processingResult = _destinationProcessor.Process(fileInfo);
                if (processingResult)
                {
                    break;
                }
                else
                {
                    _logger.Warn($"Error sending data to destination. Trial {trialsCount} out of {NumberOfTrialsPerSendRequest}");
                }
            }

            if (!processingResult)
            {
                _hasError = true;
                _logger.Error("Error sending data to destination. Moving on to next.");
                return false;
            }

            _logger.DebugFormat("Completed processing of the {1} Elapsed time is {0}.", (DateTime.Now - startTime).ToString(@"dd\.hh\:mm\:ss"), fileInfo.FullName);

            return true;
        }
    }
}
