using System.Threading;
using Castle.Core.Internal;
using Castle.Core.Logging;
using FeedGenerators.Core.SectionHanlderEntities;
using FeedGenerators.Core.Services.Abstract;
using FeedGenerators.Core.Services.Concrete;
using ICSharpCode.SharpZipLib.Zip;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Services.Interfaces;
using Indigo.Feeds.Utils;
using RRFeedGenerator.Execution.FileFeedWriter;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FeedGenerators.Core.Enums;

namespace RrFeedGenerator.Execution
{

    public class Runner : IRunner
    {
        private IExecutionLogLogger _executionLogLogger;
        private bool _isIncrementalRun;
        private DateTime? _fromTime;
        private DateTime? _effectiveFromTime;
        private int _feedId;
        private IFeedGeneratorIndigoCategoryService _feedGeneratorCategoryService;
        private readonly IFeedRuleService _feedRuleService;
        private readonly IFeedCmsProductArchiveEntryService _feedCmsProductArciveEntryService;
        private readonly IIndigoCategoryService _indigoCategoryService;
        private readonly IGoogleCategoryService _googleCategoryService;
        private RunnerHelper _runnerHelper;

        private static readonly bool LimitTo100Products = ParameterUtils.GetParameter<bool>("RRFeedGenerator.LimitTo100Products");

        private static readonly string OdysseyCommerceConnectionString = ConfigurationManager.ConnectionStrings["OdysseyCommerceDB"].ConnectionString;
        private static readonly string OutputFolderPath = ParameterUtils.GetParameter<string>("RRFeedGenerator.OutputFolderPath");
        private static readonly int SearchDataCommandTimeout = ParameterUtils.GetParameter<int>("SearchDataCommandTimeout");
        private static readonly bool AllowItemErrorsInFiles = ParameterUtils.GetParameter<bool>("AllowItemErrorsInFiles");
        private static readonly bool ZipFiles = ParameterUtils.GetParameter<bool>("ZipFiles");
        private static readonly int MaximumThreadsToUse = ParameterUtils.GetParameter<int>("MaximumThreadsToUse");
        public static readonly ParallelOptions ParallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaximumThreadsToUse
        };

        private static readonly FileNameComparer FileNameComparerObject = new FileNameComparer();

        public ILogger Log { get; set; }

        public Runner(IFeedCmsProductArchiveEntryService feedCmsProductArciveEntryService,
            IFeedRuleService feedRuleService, IIndigoCategoryService indigoCategoryService,
            IGoogleCategoryService googleCategoryService)
        {
            _feedCmsProductArciveEntryService = feedCmsProductArciveEntryService;
            _feedRuleService = feedRuleService;
            _indigoCategoryService = indigoCategoryService;
            _googleCategoryService = googleCategoryService;
        }

        public void Initialize(IExecutionLogLogger executionLogLogger, int feedId, bool isIncremental,
            DateTime? fromTime, DateTime? effectiveFromTime, DateTime executionTime)
        {
            _executionLogLogger = executionLogLogger;
            _isIncrementalRun = isIncremental;
            _fromTime = fromTime;
            _effectiveFromTime = effectiveFromTime;
            _feedId = feedId;


            // Instantiate the IFeedGeneratorIndigoCategoryService 
            _feedGeneratorCategoryService = new FeedGeneratorIndigoCategoryService(_indigoCategoryService,
                _googleCategoryService, Log);
            _runnerHelper = new RunnerHelper(_feedRuleService, _feedGeneratorCategoryService,
                _feedCmsProductArciveEntryService, Log);
            _runnerHelper.Initialize(feedId, _isIncrementalRun, _fromTime, executionTime);
        }

        public IExecutionLogLogger Execute()
        {
            if (_isIncrementalRun)
                throw new NotImplementedException("Incremental runs are not implemented.");

            try
            {
                GenerateFeedFiles();
            }
            finally
            {
                RemoveExecutionFiles();
            }

            _executionLogLogger.HasError = _hasError;
            _executionLogLogger.SetExecutionEndTime(DateTime.Now);
            return _executionLogLogger;
        }

        private void RemoveExecutionFiles()
        {
            try
            {
                if (!ParameterUtils.GetParameter<bool>("DeleteWorkingFiles"))
                    return;

                Directory.Delete(ParameterUtils.GetParameter<string>("WorkingDirectory"), recursive: true);

                if (!ZipFiles)
                    return;

                IEnumerable<string> outputTextFiles = Directory.EnumerateFileSystemEntries(OutputFolderPath,
                    "*.txt");
                outputTextFiles.ForEach(File.Delete);
            }
            catch (Exception ex)
            {
                const string message = "Error during cleanup working file deletion.";
                Log.Error(message, ex);
                _executionLogLogger.AddCustomMessage(message);
            }
        }

        private bool _hasError;

        private void GenerateFeedFiles()
        {
            Log.Debug("GenerateFeedFile start");

            var configSection =
                (FeedGenerationFileInstructionsConfigurationSection)
                    ConfigurationManager.GetSection("feedGenerationFileInstructions");

            Dictionary<FeedGenerationFileLineItem, FeedGenerationFileItemRange[]> lineItemDictionary =

                configSection.FeedGenerationFileInstructions

                    .SelectMany(feedGenerationFileInstruction => feedGenerationFileInstruction.LineItems)

                    .ToDictionary(lineItem => lineItem, lineItem => lineItem.GetRanges());

            lineItemDictionary.ForEach(WriteLineItem);
            
            if (!AllowItemErrorsInFiles && _hasError)
            {
                return;
            }

            // Merge working files and zip

            var fileInstructionsKey = configSection.FeedGenerationFileInstructions.First().Key;

            Parallel.ForEach(new[]
            {
                Language.English,
                Language.French
            },
                ParallelOptions,

                language =>
                {
                    var fileNameList = new ConcurrentBag<string>();

                    Log.Info("Stitching " + language);

                    MergeWorkingFiles(fileInstructionsKey, language).ForEach(fileNameList.Add);
                    fileNameList.Add(CreateAllCategoriesFile(fileInstructionsKey, language));

                    if (ZipFiles)
                    {
                        Log.Info("Zipping " + language);
                        ZipOutputFiles(language, fileInstructionsKey, fileNameList);
                    }
                });
        }

        private void WriteLineItem(KeyValuePair<FeedGenerationFileLineItem, FeedGenerationFileItemRange[]> lineItemEntry)
        {
            _hasError = AbstractFileFeedWriter.Write(lineItemEntry, WriteRangeFile, Log, _executionLogLogger,
                _feedGeneratorCategoryService, _isIncrementalRun);
        }

        private void WriteRangeFile(FeedGenerationFileLineItem fileComponent, FeedGenerationFileItemRange range,
            IEnumerable<AbstractFileFeedWriter> feedWriters)
        {
            using (var sqlConnection = new SqlConnection(OdysseyCommerceConnectionString))
            {
                sqlConnection.Open();

                var catalog = fileComponent.Catalog;
                var sqlParameters = new SqlParameter[2];
                if (!fileComponent.IsIncluded)
                {
                    Log.InfoFormat("FeedGenerationFileLineItem [{0}-{1}] was excluded from feed generation.", catalog,
                        fileComponent.RangeDatas);
                    return;
                }

                var identifier = string.Format("{0}_{1}", catalog, fileComponent.RangeDatas);
                try
                {
                    sqlParameters[0] = new SqlParameter("@PIDRangeStart", range.Begin);
                    sqlParameters[1] = new SqlParameter("@PIDRangeEnd ", range.End);
                    identifier = string.Format("{0}_{1}_{2}", catalog, range.Begin, range.End);

                    var startDt = DateTime.Now;
                    Log.DebugFormat("[{0}] start", identifier);
                    using (var sqlCommand = new SqlCommand(fileComponent.StoredProcedureName, sqlConnection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = SearchDataCommandTimeout
                    })
                    {
                        if (!(sqlParameters[0].Value.ToString() == "0" && sqlParameters[1].Value.ToString() == "99"))
                            sqlCommand.Parameters.AddRange(sqlParameters);

                        if (LimitTo100Products)
                            sqlCommand.Parameters.AddWithValue("@GetTop100", 1);

                        if (_isIncrementalRun)
                        {
                            sqlCommand.Parameters.AddWithValue("@IsIncremental", 1);
                            sqlCommand.Parameters.AddWithValue("@DateChanged", _effectiveFromTime);
                        }

                        using (var sqlDataReader = sqlCommand.ExecuteReader())
                        {
                            if (!sqlDataReader.HasRows)
                                Log.InfoFormat("Runner.WriteRangeFile. sqlDataReader has no rows. catalog: {0}", catalog);

                            while (sqlDataReader.Read())
                            {
                                foreach (var writer in feedWriters)
                                    try
                                    {
                                        writer.Write(sqlDataReader, identifier);
                                    }
                                    catch (Exception ex)
                                    {
                                        if (_isIncrementalRun || !AllowItemErrorsInFiles)
                                            throw;

                                        Log.Error(string.Format("[Feed] {0} errored out.", identifier), ex);
                                        _hasError = true;
                                    }
                            }
                        } //using sqldatareader
                    } //using sqlCommand
                    var endDt = DateTime.Now;
                    var execTime = endDt - startDt;
                    Log.InfoFormat("[{0}] completed. Execution time in seconds: {1}", identifier, execTime.TotalSeconds);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("Runner.WriteRangeFile(). [Feed] {0} errored out.", identifier), ex);
                    _hasError = true;

                    if (_isIncrementalRun || !AllowItemErrorsInFiles)
                        throw;
                }
            }
        }

        private class MergeData
        {
            public string WorkingFilePathKey;
            public string OutputFileBaseNameKey;
            public string FeedTypeFileHeader;
        }

        private List<string> MergeWorkingFiles(string fileInstructionsKey, Language language)
        {
            var outputFileList = new List<string>();

            var mergeDataArray = new MergeData[]
            {
                new MergeData
                {
                    WorkingFilePathKey = "ProductFilesPath",
                    OutputFileBaseNameKey = "ProductFileBaseName",
                    FeedTypeFileHeader =
                        "product_id|name|product_parent_id|price|recommendable|image_url|link_url|rating|num_reviews|brand|sale_price_min|sale_price_max|list_price_min|list_price_max"
                },

                new MergeData
                {
                    WorkingFilePathKey = "AttributeFilesPath",
                    OutputFileBaseNameKey = "AttributeFileBaseName",
                    FeedTypeFileHeader = "product_id|attr_name|attr_value"                        
                },

                new MergeData
                {
                    WorkingFilePathKey = "ProductCategoryFilesPath",
                    OutputFileBaseNameKey = "CategoryFileBaseName",
                    FeedTypeFileHeader = "category_id|product_id"                        
                }
            };

            foreach (var mergeData in mergeDataArray)
            {
                outputFileList.Add(MergeWorkingFiles(mergeData, fileInstructionsKey, language));
            }
                

            return outputFileList;
        }

        private string MergeWorkingFiles(MergeData mergeData, string fileInstructionsKey, Language language)
        {
            var workingPath = ParameterUtils.GetParameter<string>(mergeData.WorkingFilePathKey) + "/" + GetWorkingPathLanguageString(language);

            Log.Info("MergeWorkingFiles working path: " + workingPath);

            var fileList = Directory.EnumerateFiles(workingPath);

            fileList = fileList.OrderBy(fileName => fileName, FileNameComparerObject);

            string languageString = GetLanguageString(language);

            var outputPath = OutputFolderPath + "/" + ParameterUtils.GetParameter<string>(mergeData.OutputFileBaseNameKey) + "_" + fileInstructionsKey + "_" + languageString +
                             DateTime.Now.ToString("yyyy_MM_dd") + ".txt";

            using (var writer = new StreamWriter(outputPath))
            {
                writer.WriteLine(mergeData.FeedTypeFileHeader);

                foreach (string filePath in fileList)
                {
                    using (var reader = new StreamReader(filePath))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                            writer.WriteLine(line);
                    }
                }
            }

            return outputPath;
        }

        private string CreateAllCategoriesFile(string fileInstructionsKey, Language language)
        {
            var languageString = GetLanguageString(language);

            var outputFileName = OutputFolderPath + "/" + "category_full_" + fileInstructionsKey + "_" + languageString +
                                 DateTime.Now.ToString("yyyy_MM_dd") + ".txt";

            var allCategories = _feedGeneratorCategoryService.GetAllIndigoCategories();

            using (var writer = new StreamWriter(outputFileName))
            {
                var line = "category_id|parent_id|name";
                writer.WriteLine(line);

                var localizedNameGetMethod = GetLocalizedNameGetMethod(language);

                foreach (var category in allCategories)
                {
                    line = category.IndigoCategoryId + "|" +
                            (category.ParentId.HasValue ? category.ParentId.Value.ToString() : string.Empty) + "|" +
                            localizedNameGetMethod(category);

                    writer.WriteLine(line);
                }
            }

            return outputFileName;
        }

        private Func<IIndigoCategory, string> GetLocalizedNameGetMethod(Language language)
        {

            Func<IIndigoCategory, string> result;

            switch (language)
            {
                case Language.French:
                    result = GetFrenchName;
                    break;
                
                default:
                    result = GetEnglishName;
                    break;
            }

            return result;
        }

        private string GetEnglishName(IIndigoCategory category)
        {
            return category.Name;
        }

        private string GetFrenchName(IIndigoCategory category)
        {
            if (string.IsNullOrWhiteSpace(category.NameFr))
                return GetEnglishName(category);
            
            return category.NameFr;
        }

        private static void ZipOutputFiles(Language language, string fileInstructionsKey,
            IEnumerable<string> outputFileList)
        {
            var languageString = GetLanguageString(language);

            var zipFileName = OutputFolderPath + "/" + "catalog_full_" + fileInstructionsKey + "_" + languageString +
                              DateTime.Now.ToString("yyyy_MM_dd") + ".zip";

            using (var zipFile = ZipFile.Create(zipFileName))
            {
                zipFile.BeginUpdate();

                outputFileList.ForEach(fullFilePath =>
                {
                    var entryNameIndex = fullFilePath.LastIndexOfAny(new[] {'/', '\\'}) + 1;

                    var entryName = fullFilePath.Substring(entryNameIndex);

                    zipFile.Add(fullFilePath, entryName);
                });

                zipFile.CommitUpdate();
            }
        }

        private static readonly ConcurrentDictionary<Language, string> LanguageDictionary = new ConcurrentDictionary
            <Language, string>(
            new Dictionary<Language, string>
            {
                {Language.English, string.Empty},
                {Language.French, "fr"}
            });

        private static string GetWorkingPathLanguageString(Language value)
        {
            if (!LanguageDictionary.ContainsKey(value))
                throw new ArgumentException("Key not found");

            if (value == Language.English)
                return "en";

            string result = LanguageDictionary[value];
            return result;
        }

        private static string GetLanguageString(Language value)
        {
            if (!LanguageDictionary.ContainsKey(value))
                throw new ArgumentException("Key not found");

            string language = LanguageDictionary[value];

            if (!string.IsNullOrWhiteSpace(language))
                language += "_";

            return language;
        }
    }

    #region Internal Classes

    class FileNameComparer : IComparer<string>
    {
        private log4net.ILog Log = log4net.LogManager.GetLogger(typeof (FileNameComparer));

        public int Compare(string inputFile1, string inputFile2)
        {
            const int expectedNumberOfSections = 4;

            var file1Sections = GetSections(inputFile1);
            var file2Sections = GetSections(inputFile2);

            if (file1Sections.Length != expectedNumberOfSections || file2Sections.Length != expectedNumberOfSections)
                throw new ApplicationException(
                    string.Format(
                        "Unexpected number of sections in input path. inputFile1 = {0}, inputFile2 = {1}",
                        inputFile1, inputFile2));

            var compareResult = String.Compare(file1Sections[0] + file1Sections[1], file2Sections[0] + file2Sections[1],
                StringComparison.Ordinal);

            if (compareResult != 0)
                return compareResult;

            for (var i = 2; i < 4; i++)
            {
                int file1RangeNumber, file2RangeNumber;

                try
                {
                    file1RangeNumber = int.Parse(file1Sections[i]);
                    file2RangeNumber = int.Parse(file2Sections[i]);
                }
                catch (Exception ex)
                {
                    throw new ApplicationException(
                        string.Format(
                            "Failed attempt to int.Parse: file1 = {1}, file2 = {2}, file1Sections[{0}] = {3}, file2Sections[{0}] = {4}",
                            i, inputFile1, inputFile2, file1Sections[i], file2Sections[i]), ex);
                }

                compareResult = file1RangeNumber.CompareTo(file2RangeNumber);
                if (compareResult != 0)
                    return compareResult;
            }

            return compareResult;
        }

        // Expected format in input path is "{directory-path}/catalog-rangeStart-rangeEnd.txt" 
        // where the range values are non-negative integers.
        // Output is string array with { "{dirsctor-path}, catalog, rangeStart, rangeEnd }.
        private string[] GetSections(string inputPath)
        {
            try
            {
                var path = inputPath.Replace('\\', '/');
                var lastIndexOfSlash = path.LastIndexOf('/');
                var directoryPath = path.Substring(0, lastIndexOfSlash);

                var fileName = path.Substring(lastIndexOfSlash + 1);
                fileName = TrimExtension(fileName);

                var fileNameSections = fileName.Split('-');

                var result = new string[4];
                result[0] = directoryPath;
                fileNameSections.CopyTo(result, 1);

                Log.Debug("GetSections result = " + string.Join(",", result));

                return result;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("FileNameComparer.GetSections Failed. Input path = " + inputPath, ex);
            }
        }

        private static string TrimExtension(string fileName)
        {
            var indexOfPeriod = fileName.LastIndexOf('.');
            if (indexOfPeriod < 0)
                return fileName;

            fileName = fileName.Substring(0, indexOfPeriod);
            return fileName;
        }
    }

    #endregion Internal Classes
}