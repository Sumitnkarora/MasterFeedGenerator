using Castle.Core.Internal;
using Castle.Core.Logging;
using FeedGenerators.Core.SectionHanlderEntities;
using FeedGenerators.Core.Services.Abstract;
using FeedGenerators.Core.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Linq;
using Indigo.Feeds.Utils;
using FeedGenerators.Core.Enums;

namespace RRFeedGenerator.Execution.FileFeedWriter
{
    internal abstract class AbstractFileFeedWriter : IDisposable
    {
        protected AbstractFileFeedWriter(InputContext inputContext)
        {
            var languageContext = ContextLanguageMap[inputContext.Language];

            Context = new FeedWriterContext(languageContext.LanguageWriter, inputContext.Language,
                languageContext.Abbreviation, FileFeedPathBase, inputContext.CommonContext);
        }

        #region Inline Classes

        public class InputContext
        {
            public FeedWriterContext.CommonContext CommonContext;
            public Language Language;
        }

        public class LanguageContext
        {
            public Func<IDictionary<Language, string>, string> LanguageWriter;
            public string Abbreviation;
        }

        #endregion Inline Classes

        #region Private Fields

        private Dictionary<Language, LanguageContext> _contextLanguageMap;
        private int _processedRecordCount = 0, _errorRecordCount = 0, _skippedRecordCount = 0;

        #endregion Private Fields

        #region Public Properties

        public string Identifier
        {
            get
            {
                return string.Format("{0}_{1}_{2}_{3}_{4}", GetType().Name, Context.Language, Context.LineItem.Catalog, Context.FileItemRange.Begin, Context.FileItemRange.End);
            }
        }

        #endregion Public Properties

        #region Protected Members

        #region Protected Fields

        static protected bool _hasError = false;
        
        #endregion Protected Fields

        #region Protected Properties

        protected Dictionary<Language, LanguageContext> ContextLanguageMap
        {
            get
            {
                return _contextLanguageMap ?? (_contextLanguageMap = new Dictionary<Language, LanguageContext>
                {
                    {Language.English, new LanguageContext {LanguageWriter = WriteEnglish, Abbreviation = "en"}},
                    {Language.French, new LanguageContext {LanguageWriter = WriteFrench, Abbreviation = "fr"}}
                });
            }
        }

        protected abstract string FileFeedPathBase { get; }

        #endregion Protected Properties

        #region Protected Methods

        protected abstract bool DoWrite(IDataReader dataReader);

        protected string WriteEnglish(IDictionary<Language, string> languageAttributes)
        {
            if (string.IsNullOrWhiteSpace(languageAttributes[Language.English]))
                return null;

            return languageAttributes[Language.English];
        }

        protected string WriteFrench(IDictionary<Language, string> languageAttributes)
        {
            if (string.IsNullOrWhiteSpace(languageAttributes[Language.French]))
                return null;

            if (!Context.AttributesDictionary.ContainsKey(languageAttributes[Language.French]))
                return languageAttributes[Language.English];

            return languageAttributes[Language.French];
        }

        protected FeedWriterContext Context;

        #endregion Protected Methods

        #endregion Protected Members

        #region Public Methods

        public static bool Write(KeyValuePair<FeedGenerationFileLineItem, FeedGenerationFileItemRange[]> lineItemEntry,
            Action<FeedGenerationFileLineItem, FeedGenerationFileItemRange, IEnumerable<AbstractFileFeedWriter>> writeRangeFile,
            ILogger log, IExecutionLogLogger executionLogLogger, IFeedGeneratorIndigoCategoryService feedGeneratorIndigoCategoryService,
            bool isIncrementalRun)
        {
            Parallel.ForEach(lineItemEntry.Value, Constants.ParallelOptions, fileItemRange =>
            {
                var commonContext = new FeedWriterContext.CommonContext
                {
                    FeedGeneratorIndigoCategoryService = feedGeneratorIndigoCategoryService,
                    FileItemRange = fileItemRange,
                    LineItem = lineItemEntry.Key,
                    Log = log,
                    ExecutionLogLogger = executionLogLogger,
                    IsIncrementalRun = isIncrementalRun
                };

                var writerList = new List<AbstractFileFeedWriter>();

                ((Language[]) Enum.GetValues(typeof (Language))).ForEach(
                    language =>
                    {
                        var inputContext = new InputContext {CommonContext = commonContext, Language = language};

                        new AbstractFileFeedWriter[]
                        {
                            // Add new Writers here.

                            new ProductFileFeedWriter(inputContext),
                            new CustomAttributeFileFeedWriter(inputContext),
                            new ProductToCategoryFileFeedWriter(inputContext)
                        }

                        .ForEach(writerList.Add);
                    });

                try
                {
                    writeRangeFile(lineItemEntry.Key, fileItemRange, writerList);
                    writerList.ForEach(writer => executionLogLogger.AddFileGenerationUpdate(writer.Context.Path, true));
                }
                catch (Exception ex)
                {
                    log.Error(string.Format("static AbstractFileFeedWriter.Write"), ex);
                    _hasError = true;

                    writerList.ForEach(writer => executionLogLogger.AddFileGenerationUpdate(writer.Context.Path, false));

                    if (isIncrementalRun || !Constants.AllowItemErrorsInFiles)
                        throw;
                }
                finally
                {
                    writerList.ForEach(writer => writer.Dispose());

                    writerList.ForEach(writer => writer.Context.Log.InfoFormat(
                        "{0} completed. Processed record count {1}. Error record count {2}, deleted/skipped record count {3}",
                        writer.Identifier, writer._processedRecordCount, writer._errorRecordCount,
                        writer._skippedRecordCount));
                }
            });

            return _hasError;
        }

        public void Write(IDataReader dataReader, string identifier)
        {
            var productAvailability = false;
            var pid = "{unavailable}";
            try
            {
                GetPID(dataReader, ref pid);

                if (CheckHasImage(dataReader) && CheckProductAvailability(dataReader))
                {
                    Context.Log.DebugFormat(
                        "Product available. Proceeding to write to output. Catalog = {0}, SP = {1}, gId = {2}",
                        Context.LineItem.Catalog, Context.LineItem.StoredProcedureName,
                        pid);

                    productAvailability = true;
                    if (DoWrite(dataReader))
                        _processedRecordCount++;
                    else
                        _errorRecordCount++;
                }
                else
                {
                    Context.Log.DebugFormat(
                        "Product not available. Not writing to output. Catalog = {0}, SP = {1}, gId = {2}",
                        Context.LineItem.Catalog, Context.LineItem.StoredProcedureName,
                        pid);
                    _skippedRecordCount++;
                }
            }
            catch (Exception ex)
            {
                var message =
                    string.Format(
                        "AbstractFeedWriter.Write(IDataRecord dataReader, string identifier). {0}: [Feed] {1} errored out. PID: {2}",
                        identifier, GetType().Name, pid);

                Context.Log.Error(message , ex);
                Context.ExecutionLogLogger.AddCustomMessage(message);
                _hasError = true;

                if (productAvailability)
                    _errorRecordCount++;

                if (Context.IsIncrementalRun || !Constants.AllowItemErrorsInFiles)
                    throw;
            }
        }

        private void GetPID(IDataReader dataReader, ref string result)
        {
            var pidObject = dataReader[Context.AttributesDictionary["gId"]];

            if (pidObject != null)
            {
                result = pidObject.ToString();
            }
        }

        public void Dispose()
        {
            if (Context.StreamWriter != null)
                Context.StreamWriter.Dispose();
        }

        #endregion Public Methods

        #region Private Methods

        private bool CheckProductAvailability(IDataRecord dataReader)
        {
            int availabilityId = (int) dataReader[Context.AttributesDictionary["gAvailability"]];
            string result = FeedUtils.GetGoogleAvailability(availabilityId);

            return result != null;
        }

        private bool CheckHasImage(IDataRecord dataReader)
        {
            const string hasImageKey = "hasImage";

            if (Constants.SkipHasImageCheck || !Context.AttributesDictionary.ContainsKey(hasImageKey))
                return true;

            var hasImage = (int)dataReader[Context.AttributesDictionary[hasImageKey]];

            var result = hasImage != 0;

            return result;
        }

        #endregion Private Methods
    }

    #region Static Classes

    static class Constants
    {
        public static readonly string ProductFilesPath = ParameterUtils.GetParameter<string>("RRFeedGenerator.ProductFilesPath");
        public static readonly string AttributeFilesPath = ParameterUtils.GetParameter<string>("RRFeedGenerator.AttributeFilesPath");
        public static readonly string ProductCategoryFilesPath = ParameterUtils.GetParameter<string>("ProductCategoryFilesPath");
        public static readonly bool AllowItemErrorsInFiles = ParameterUtils.GetParameter<bool>("AllowItemErrorsInFiles");
        public static readonly bool SkipHasImageCheck = ParameterUtils.GetParameter<bool>("RRFeedGenerator.SkipHasImageCheck");
        public static readonly bool ExecutionLogBreadCrumbErrors = ParameterUtils.GetParameter<bool>("ExecutionLogBreadCrumbErrors");
        
        private static readonly int MaximumThreadsToUse = ParameterUtils.GetParameter<int>("MaximumThreadsToUse");

        public static readonly ParallelOptions ParallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaximumThreadsToUse
        };
    }

    #endregion Static Classes
}
