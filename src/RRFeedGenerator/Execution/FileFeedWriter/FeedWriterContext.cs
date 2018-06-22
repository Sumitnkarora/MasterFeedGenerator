using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.Core.Logging;
using FeedGenerators.Core.SectionHanlderEntities;
using FeedGenerators.Core.Services.Abstract;
using FeedGenerators.Core.Enums;

namespace RRFeedGenerator.Execution.FileFeedWriter
{
    public class FeedWriterContext
    {
        public FeedWriterContext(Func<IDictionary<Language, string>, string> writeLanguage, Language language, string languageString,
            string pathBase, CommonContext commonContext)
        {
            WriteLanguage = writeLanguage;
            LineItem = commonContext.LineItem;
            FileItemRange = commonContext.FileItemRange;
            FeedGeneratorIndigoCategoryService = commonContext.FeedGeneratorIndigoCategoryService;
            Language = language;

            Path = GetFeedFilePath(FileItemRange, LineItem, languageString, pathBase);
            AttributesDictionary = (StringDictionary)ConfigurationManager.GetSection(LineItem.Catalogattributesection);
            Log = commonContext.Log;
            ExecutionLogLogger = commonContext.ExecutionLogLogger;
            IsIncrementalRun = commonContext.IsIncrementalRun;

            EnsureWorkingFeedFileFolder(pathBase, languageString);
            StreamWriter = new StreamWriter(Path);
        }

        private static string GetFeedFilePath(FeedGenerationFileItemRange fileItemRange, FeedGenerationFileLineItem feedGenerationFileLineItem,
            string languageString, string pathBase)
        {
            return string.Format("{0}/{1}/{2}-{3}-{4}.txt", pathBase, languageString, feedGenerationFileLineItem.Catalog,
                fileItemRange.Begin, fileItemRange.End);
        }

        private readonly static ConcurrentDictionary<string, object> FeedFileFolderLockDictionary =
            new ConcurrentDictionary<string, object>();

        private void EnsureWorkingFeedFileFolder(string pathBase, string languageString)
        {
            var pathToEnsure = pathBase + "/" + languageString;

            FeedFileFolderLockDictionary.GetOrAdd(pathToEnsure, key => Directory.CreateDirectory(pathToEnsure));
        }

        public class CommonContext
        {
            public FeedGenerationFileLineItem LineItem;

            public FeedGenerationFileItemRange FileItemRange;

            public IFeedGeneratorIndigoCategoryService FeedGeneratorIndigoCategoryService;

            public ILogger Log;

            public IExecutionLogLogger ExecutionLogLogger;

            public bool IsIncrementalRun;
        }

        public string Path
        {
            get;
            private set;
        }

        public Func<IDictionary<Language, string>, string> WriteLanguage
        {
            get;
            private set;
        }

        public FeedGenerationFileLineItem LineItem
        {
            get;
            private set;
        }

        public FeedGenerationFileItemRange FileItemRange
        {
            get;
            private set;
        }

        public IFeedGeneratorIndigoCategoryService FeedGeneratorIndigoCategoryService
        {
            get;
            private set;
        }

        public Language Language
        {
            get;
            private set;
        }

        public StringDictionary AttributesDictionary
        {
            get;
            private set;
        }

        public ILogger Log
        {
            get;
            private set;
        }

        public StreamWriter StreamWriter
        {
            get;
            private set;
        }

        public IExecutionLogLogger ExecutionLogLogger
        {
            get;
            private set;
        }

        public bool IsIncrementalRun
        {
            get;
            private set;
        }
    }

}
