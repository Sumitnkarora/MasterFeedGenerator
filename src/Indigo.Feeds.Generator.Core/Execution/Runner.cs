using Castle.Core.Logging;
using Indigo.Feeds.Generator.Core.Enums;
using Indigo.Feeds.Generator.Core.Execution.Contracts;
using Indigo.Feeds.Generator.Core.Models;
using Indigo.Feeds.Generator.Core.Processors.Contracts;
using Indigo.Feeds.Generator.Core.Services;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Indigo.Feeds.Generator.Core.Execution
{
    public class Runner : IRunner
    {
        private ExecutionInformation _executionInformation;
        private RunType _runType;
        private DateTime? _effectiveFromTime;
        private DateTime? _effectiveToTime;
        private bool _hasError;

        private readonly IOutputProcessor _outputInstructionProcessor;
        private readonly IFeedRuleService _feedRuleService;
        private readonly IDataService _dataService;

        // TODO: configs should be injected for better testing
        private static readonly bool LimitTo100Products = ParameterUtils.GetParameter<bool>("OutputProcessor.LimitTo100Products");
        private static readonly Dictionary<string, Dictionary<string, string>> FeedGenerationInstructionsDictionary = ConfigurationManager.GetSection("feedgenerationinstructiondict") as Dictionary<string, Dictionary<string, string>>;
        private static readonly int MaximumThreadsToUse = ParameterUtils.GetParameter<int>("MaximumThreadsToUse");
        protected static readonly string OutputFolderPath = ParameterUtils.GetParameter<string>("OutputProcessor.OutputFolderPath");
        protected static readonly int NumberOfRecordsPerBatch = ParameterUtils.GetParameter<int>("OutputProcessor.NumberOfRecordsPerBatch");
        private static readonly string DataConnectionString = ConfigurationManager.ConnectionStrings["Data"].ConnectionString;
        private static readonly int DataCommandTimeout = ParameterUtils.GetParameter<int>("DataCommandTimeout");
        private static readonly string DeletedStoredProcedureName = ParameterUtils.GetParameter<string>("DeletedProductsStoredProcedureName");

        protected ILogger Log { get; set; }
        
        public Runner(IFeedRuleService feedRuleService, IOutputProcessor outputInstructionProcessor, IDataService dataService, ILogger logger)
        {
            _feedRuleService = feedRuleService;
            _outputInstructionProcessor = outputInstructionProcessor;
            _dataService = dataService;
            Log = logger;
        }

        public void Initialize(RunType runType, DateTime? effectiveFromTime, DateTime? effectiveToTime)
        {
            _executionInformation = new ExecutionInformation
            {
                RecordsPerFile = NumberOfRecordsPerBatch
            };
            _runType = runType;
            _effectiveFromTime = effectiveFromTime;
            _effectiveToTime = effectiveToTime;
        }
        
        public bool IsReady()
        {
            return !_outputInstructionProcessor.Initialize();
        }

        public ExecutionInformation Execute()
        {
            var startMessage = string.Format("Executing {0} run{1}{2}",
                _runType,
                _effectiveFromTime.HasValue ? string.Format(" with an effective from time of {0}", _effectiveFromTime.Value) : string.Empty,
                _effectiveToTime.HasValue ? string.Format(" and to time of {0}", _effectiveToTime.Value) : string.Empty);
            Log.InfoFormat(startMessage);
            
            if (!string.IsNullOrEmpty(DeletedStoredProcedureName) && 
                (_runType == RunType.Incremental || _runType == RunType.OnDemand) && 
                _effectiveFromTime.HasValue)
            {
                ProcessDeletedRecords(_effectiveFromTime.Value, _effectiveToTime);
            }

            // Build the range collection
            var ranges = GetRanges();
            if (ranges.Any())
            {
                Parallel.ForEach(ranges, new ParallelOptions { MaxDegreeOfParallelism = MaximumThreadsToUse }, ProcessRange);
            }

            _executionInformation.HasError = _hasError;

            return _executionInformation;
        }

        public bool Finalize(bool isSuccess)
        {
            return _outputInstructionProcessor.FinalizeProcessing(isSuccess);
        } 

        private List<ProcessingInstruction> GetRanges()
        {
            var ranges = new List<ProcessingInstruction>();
            foreach (var instructionEntry in FeedGenerationInstructionsDictionary)
            {
                var isIncluded = bool.Parse(instructionEntry.Value["isincluded"]);
                var catalog = instructionEntry.Value["catalog"];
                if (!isIncluded)
                {
                    Log.InfoFormat("Catalog [{0}] excluded from feed generation", catalog);
                    continue;
                }

                var dbcmd = instructionEntry.Value["dbcmd"];
                var catalogattributesection = instructionEntry.Value["catalogattributesection"];
                var feedSplitter = instructionEntry.Value["splitter"];

                var baseProcessingInstruction = new ProcessingInstruction
                {
                    Catalog = catalog,
                    CatalogAttributesSection = catalogattributesection,
                    Dbcmd = dbcmd
                };

                foreach (var range in feedSplitter.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                {
                    baseProcessingInstruction.Range = range;
                    ranges.Add(baseProcessingInstruction);
                }
            }

            return ranges;
        }

        private void ProcessRange(ProcessingInstruction processingInstruction)
        {
            var sqlParameters = new SqlParameter[2];
            var pair = processingInstruction.Range.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            sqlParameters[0] = new SqlParameter("@RangeStart", Convert.ToInt32(pair[0]));
            sqlParameters[1] = new SqlParameter("@RangeEnd", Convert.ToInt32(pair[1]));
            var identifier = string.Format("{0}_{1}_{2}", processingInstruction.Catalog, sqlParameters[0].Value, sqlParameters[1].Value);
            try
            {
                var startDt = DateTime.Now;
                Log.DebugFormat("[{0}] start", identifier);
                var counters = ProcessFeed(processingInstruction.Catalog, processingInstruction.Dbcmd, sqlParameters, identifier, processingInstruction.CatalogAttributesSection);
                var hasError = counters.NumberOfErrored > 0 && !counters.AllowErrors;
                if (hasError)
                {
                    _hasError = true;
                }
                var endDt = DateTime.Now;
                var execTime = endDt - startDt;
                Log.InfoFormat("[{0}] completed with result {2}. Execution time in seconds: {1}", identifier, execTime.TotalSeconds, !hasError);
                _executionInformation.AddFileGenerationUpdate(counters);
            }
            catch (Exception ex)
            {
                Log.InfoFormat("[Feed] {0}; error {1}", processingInstruction.Catalog + "-" + pair[0] + pair[1], ex);
                _executionInformation.AddFileGenerationUpdate(identifier, false);
                _hasError = true;
            }
        }

        /// <summary>
        /// Generates feed file
        /// </summary>
        /// <param name="catalog">catalog: data source</param>
        /// <param name="commandText">stored procedure name</param>
        /// <param name="sqlParameters">parameter for stored procedure (null if none)</param>
        /// <param name="identifier">feed identifier (used for feed files names)</param>
        /// <param name="configSection">name of the configuration section containing catalog attributes</param>
        /// <returns>True if success; false otherwise.</returns>
        private ProcessingCounters ProcessFeed(string catalog, string commandText, SqlParameter[] sqlParameters, string identifier, string configSection)
        {
            // TODO: move to repository to be injected
            using (var sqlConnection = new SqlConnection(DataConnectionString))
            {
                sqlConnection.Open();
                using (var sqlCommand = new SqlCommand(commandText, sqlConnection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = DataCommandTimeout
                })
                {
                    if (sqlParameters != null && sqlParameters.Length == 2 && !(sqlParameters[0].Value.ToString() == "0" && sqlParameters[1].Value.ToString() == "99"))
                        sqlCommand.Parameters.AddRange(sqlParameters);

                    if (LimitTo100Products)
                    {
                        sqlCommand.Parameters.AddWithValue("@GetTop100", 1);
                    }

                    if (_runType == RunType.Incremental)
                    {
                        //sqlCommand.Parameters.AddWithValue("@IsIncremental", 1);
                        sqlCommand.Parameters.AddWithValue("@StartDate", _effectiveFromTime);
                    }

                    if (_runType == RunType.OnDemand)
                    {
                        sqlCommand.Parameters.AddWithValue("@StartDate", _effectiveFromTime);
                        sqlCommand.Parameters.AddWithValue("@EndDate", _effectiveToTime);
                    }

                    using (var sqlDataReader = sqlCommand.ExecuteReader())
                    {
                        if (sqlDataReader.HasRows)
                        {
                            return ExecuteFeedUpdates(catalog, sqlDataReader, identifier, configSection);
                        }
                    }
                }
            }
            return new ProcessingCounters();
        }

        /// <summary>
        /// Creates output with the processor.
        /// </summary>
        /// <param name="catalog">catalog from which data is retrieved</param>
        /// <param name="reader">datareader</param>
        /// <param name="identifier">feed identifier used for file name</param>
        /// <param name="configSection">name of the configuration section containing catalog attributes</param>
        /// <returns>Processing counters.</returns>
        private ProcessingCounters ExecuteFeedUpdates(string catalog, IDataReader reader, string identifier, string configSection)
        {
            var dict = ConfigurationManager.GetSection(configSection) as StringDictionary;

            Log.DebugFormat("[ExecuteFeedUpdates] {0} start", identifier);
            var result = _outputInstructionProcessor.CreateOutput(reader, dict, catalog, identifier, _runType);
            Log.DebugFormat("[ExecuteFeedUpdates] {0} completed with result {1}", identifier, result);
            return result;
        }       
        
        #region Update-related API code

        private void OutputBatch(OutputFormat batchOutput, IEnumerable<BaseExportData> batchData, int batchCounter, string identifier, string catalogName = null)
        {
            var data = batchData.ToList();
            LogStartBatch(batchOutput, batchCounter, identifier, data.Count);

            var instruction = new OutputInstruction
            {
                Format = batchOutput,
                CatalogName = catalogName,
                Count = batchCounter,
                OutputLocation = OutputFolderPath,
                OutputName = batchOutput.ToString() + identifier + batchCounter,
                Data = data
            };

            _outputInstructionProcessor.RecordOutput(instruction);
            Log.DebugFormat("Completed batch {0}{1}{2}.", batchOutput.ToString(), identifier, batchCounter);
        }

        private void LogStartBatch(OutputFormat batchOutput, int batchCounter, string identifier, int numberOfRecords)
        {
            var message = string.Format("Starting batch {0} {1} for identifier {2} containing {3} records.", batchOutput.ToString(), batchCounter, identifier, numberOfRecords);
            if (batchCounter == 1)
            {
                Log.InfoFormat(message);
            }
            else
            {
                Log.DebugFormat(message);
            }
        }
        
        #endregion

        #region Processing of deleted records

        private void ProcessDeletedRecords(DateTime fromTime, DateTime? toTime)
        {
            var result = _dataService.GetDeletedData(fromTime, toTime);
            Log.InfoFormat("There were {0} deleted records retrieved", result.Count);
            ProcessDeletedInformation(result);
        }

        private void ProcessDeletedInformation(IEnumerable<DataResult> deletedData)
        {
            Log.Debug("Entered ProcessDeletedInformation");
            var batchCount = 1;
            var batch = new List<BaseExportData>();

            foreach (var data in deletedData)
            {
                batch.Add(data.ExportData);

                if (batch.Count >= NumberOfRecordsPerBatch)
                {
                    OutputBatch(OutputFormat.Delete, batch, batchCount, "deletions");
                    batchCount++;
                    batch = new List<BaseExportData>();
                }
            }

            if (batch.Count > 0)
            {
                OutputBatch(OutputFormat.Delete, batch, batchCount, "deletions");
                batchCount++;
                batch = new List<BaseExportData>();
            }

            Log.Debug("Exiting ProcessDeletedInformation.");
        }

        #endregion

        #region Helper structs

        private struct ProcessingInstruction
        {
            public string Range { get; set; }
            public string Catalog { get; set; }
            public string Dbcmd { get; set; }
            public string CatalogAttributesSection { get; set; }
        }

        #endregion Helper structs
    }
}
