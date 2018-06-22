using Castle.Core.Logging;
using Indigo.Feeds.Generator.Core.Enums;
using Indigo.Feeds.Generator.Core.Models;
using Indigo.Feeds.Generator.Core.Processors;
using Indigo.Feeds.Generator.Core.Processors.Contracts;
using Indigo.Feeds.Generator.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Linq;

namespace Indigo.Feeds.Generator.Core.Execution
{
    public class OutputInstructionProcessor : BaseOutputProcessor
    {
        private readonly IDestinationProcessor<OutputInstruction> _destinationProcessor;

        public OutputInstructionProcessor(
            IFileContentProcessor fileContentProcessor, 
            IDestinationProcessor<OutputInstruction> destinationProcessor,
            IDataService dataService,
            ILogger logger) 
            : base(fileContentProcessor, dataService, logger)
        {
            _destinationProcessor = destinationProcessor;
        }
        
        public override ProcessingCounters CreateOutput(IDataReader reader, StringDictionary dict, string catalog, string identifier, RunType runType)
        {
            var counter = new ProcessingCounters() { AllowErrors = AllowItemErrorsInFiles, Identifier = identifier };
            var newBatch = new List<ExportData>();
            var updatedBatch = new List<ExportData>();
            var batchCount = 1;
            while (reader.Read())
            {
                var sourceId = reader[dict["sourceId"]].ToString();
                _logger.DebugFormat("{0}::Processing record [{1}]: {2}", identifier, (counter.GetTotalProcessed()), sourceId);
                var title = reader[dict["title"]].ToString();
                try
                {
                    // First get the data 
                    var data = _dataService.GetData(dict, reader, catalog, runType);
                    if (data == null)
                    {
                        var message = string.Format("[{2}]Record {0} - {1} wasn't found, so is being treated as an erroneous item.", sourceId, title, identifier);
                        _logger.Debug(message);
                        counter.NumberOfErrored++;
                        counter.AddCustomMessage(message);
                        continue;
                    }

                    if (data.IsNew)
                    {
                        _logger.DebugFormat("Record {0} was found and was new, so adding its data to the insert batch.", sourceId);
                        newBatch.Add(data.ExportData);
                        counter.NumberOfNew++;
                        continue;
                    }

                    _logger.DebugFormat("Record {0} was found and was modified, so adding its data to the update batch.", sourceId);
                    updatedBatch.Add(data.ExportData);
                    counter.NumberOfModified++;
                    continue;
                }
                catch (Exception exception)
                {
                    counter.NumberOfErrored++;
                    var errorMessage = string.Format("Cannot process the item. Id:{0};title:{1},catalog:{2},Message:{3}", sourceId, title, catalog, exception.Message);
                    counter.AddCustomMessage(errorMessage);
                    _logger.Error(errorMessage);
                    _logger.DebugFormat("Error stack trace: {0}", exception);
                }

                if (newBatch.Count >= NumberOfRecordsPerBatch)
                {
                    OutputBatch(OutputFormat.Insert, newBatch, batchCount, identifier, catalog);
                    batchCount++;
                    newBatch = new List<ExportData>();
                }

                if (updatedBatch.Count >= NumberOfRecordsPerBatch)
                {
                    OutputBatch(OutputFormat.Update, updatedBatch, batchCount, identifier, catalog);
                    batchCount++;
                    updatedBatch = new List<ExportData>();
                }
            }

            if (newBatch.Any())
            {
                OutputBatch(OutputFormat.Insert, newBatch, batchCount, identifier, catalog);
                batchCount++;
                newBatch = new List<ExportData>();
            }

            if (updatedBatch.Any())
            {
                OutputBatch(OutputFormat.Update, updatedBatch, batchCount, identifier, catalog);
                batchCount++;
                updatedBatch = new List<ExportData>();
            }

            _logger.InfoFormat("[ExecuteFeedUpdate] {0} completed new record count: {1}, error record count: {2}, changed record count: {3}.",
                identifier, counter.NumberOfNew, counter.NumberOfErrored, counter.NumberOfModified);

            // TODO: Do we want to halt execution if we ran into an error here which will stop other unprocessed ranges from being sent?
            if (!AllowItemErrorsInFiles && counter.NumberOfErrored > 0)
            {
                _hasError = true;
            }

            return counter;
        }
        
        protected override void ProcessAvailableInstruction(FileInfo fileInfo)
        {
            _logger.DebugFormat("Picked {0} as the file to process", fileInfo.FullName);
            try
            {
                OutputInstruction instruction = _fileProcessor.FileRead(fileInfo);
                if (ProcessInstruction(instruction))
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

        private bool ProcessInstruction(OutputInstruction instruction)
        {
            var result = false;
            var itemCount = GetItemCount(instruction); 
            var startTime = DateTime.Now;
            _logger.DebugFormat("Starting to process an instruction with type of {0} containing {1} entries of catalog {2}.", instruction.Format, itemCount, instruction.CatalogName);

            result = SendToDestination(instruction);

            var message = string.Format("Completed processing of the {1} instruction with file count of {2}. Elapsed time is {0}.", (DateTime.Now - startTime).ToString(@"dd\.hh\:mm\:ss"), instruction.Format, instruction.Count);
            if (instruction.Count == 1)
            {
                _logger.InfoFormat(message);
            }
            else
            {
                _logger.DebugFormat(message);
            }

            return result;
        }

        private bool SendToDestination(OutputInstruction instruction)
        {
            if (!_destinationProcessor.Process(instruction))
            {
                _logger.Info("Error sending data to destination. Moving on to next.");
                _hasError = true;
                return false;
            }

            return true;
        }

        private void OutputBatch(OutputFormat batchOutput, IEnumerable<BaseExportData> batchData, int batchCounter, string identifier, string catalogName = null)
        {
            var data = batchData.ToList();
            var message = string.Format("Starting batch {0} {1} for identifier {2} containing {3} records.", batchOutput.ToString(), batchCounter, identifier, data.Count);
            if (batchCounter == 1)
            {
                _logger.InfoFormat(message);
            }
            else
            {
                _logger.DebugFormat(message);
            }

            var instruction = new OutputInstruction
            {
                Format = batchOutput,
                CatalogName = catalogName,
                Count = batchCounter,
                OutputLocation = identifier,
                OutputName = string.Format("{0}_{1}", batchOutput.ToString(), batchCounter),
                Data = data
            };

            RecordOutput(instruction);
            _logger.DebugFormat("Completed batch {0}.", batchOutput.ToString());
        }

        protected override void PrepareDestination()
        {
            if (!_destinationProcessor.IsOpen)
            {
                _destinationProcessor.Open();
            }
        }

        protected override void CleanupDestination()
        {
            _destinationProcessor.Close();
        }
    }
}
