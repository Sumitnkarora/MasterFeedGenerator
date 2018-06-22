using Castle.Core.Logging;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using FeedGenerators.Core;
using Indigo.Feeds.Exceptions;
using NewGoogleCategoriesForIndigoCategoriesImporter.Counters;
using NewGoogleCategoriesForIndigoCategoriesImporter.Input;

namespace NewGoogleCategoriesForIndigoCategoriesImporter
{
    public class Builder : IBuilder
    {
        private readonly IGoogleCategoryService _googleCategoryService;
        private readonly IIndigoCategoryService _indigoCategoryService;
        private readonly IInputDataProvider _inputDataProvider;
        private readonly PathResolver _pathResolver;

        public ILogger Log { get; set; }

        public Builder(IGoogleCategoryService googleCategoryService, IIndigoCategoryService indigoCategoryService,
            IInputDataProvider inputDataProvider, PathResolver pathResolver)
        {
            _googleCategoryService = googleCategoryService;
            _indigoCategoryService = indigoCategoryService;
            _inputDataProvider = inputDataProvider;
            _pathResolver = pathResolver;

            _counters = new Counters.Counters();
        }

        private Dictionary<string, IGoogleCategory> _googleCategoryDictionary;
        public Dictionary<string, IGoogleCategory> GoogleCategoryDictionary
        {
            get
            {
                var result = _googleCategoryDictionary ??

                    (_googleCategoryDictionary =

                             _googleCategoryService.GetAllGoogleCategories()
                                 .ToDictionary(p => p.BreadcrumbPath.Trim())
                    );

                return result;
            }
        }

        private Dictionary<int, IIndigoCategory> _indigoCategoryDictionary;
        public Dictionary<int, IIndigoCategory> IndigoCategoryDictionary
        {
            get
            {
                var result = _indigoCategoryDictionary ??

                    (_indigoCategoryDictionary =

                             _indigoCategoryService.GetAllIndigoCategories()
                                 .ToDictionary(p => p.IndigoCategoryId)
                    );

                return result;
            }
        }

        private readonly Counters.Counters _counters;
        public ICounterResults Counters
        {
            get { return _counters; }
        }

        public void Build(string[] args)
        {
            var processContext = new ProcessContext();

            try
            {
                Log.Info("Execution started.");
                // Check if the input file exists. If not, exit.
                if (!_pathResolver.HasInputFile())
                {
                    Log.Info("No input file was found. Exiting execution.");
                    return;
                }

                var inputs = _inputDataProvider.GetInputSet();
                this.ProcessInputs(inputs, processContext);
            }
            catch (Exception ex)
            {
                Log.Error(
                    string.Format("There was an issue during execution. Exiting the application!\n" +
                                  GetRecordErrorMessage(processContext)), ex);
                
                throw;
            }

            this.Log.Info("Processing complete");

            this.LogCounters();
        }

        private class ProcessContext
        {
            public int CurrentLineCount;
            public InputRecord CurrentRecord;
        }

        private static string GetRecordErrorMessage(ProcessContext processContext)
        {
            var result = string.Format("Current record number: {0}. Current record: {1}",
                processContext.CurrentLineCount,
                processContext.CurrentRecord != null ? processContext.CurrentRecord.ToString() : "<null value>");

            return result;
        }

        private void ProcessInputs(IList<InputRecord> inputs, ProcessContext context)
        {
            context.CurrentLineCount = 0;
            foreach (var inputRecord in inputs)
            {
                context.CurrentRecord = inputRecord;
                context.CurrentLineCount++;

                if (!this.HasNewGoogleBreadcrumb(context))
                {
                    _counters.UnchangedCategoryCount++;
                    continue;
                }

                var googleCategory = this.FindGoogleCategory(context);

                if (googleCategory == null)
                {
                    _counters.ErrorCount++;
                    continue;
                }

                var indigoCategory = this.FindIndigoCategory(context);

                if (indigoCategory == null)
                {
                    _counters.ErrorCount++;
                    continue;
                }

                if (this.IsUnchangedRecord(googleCategory.GoogleCategoryId, indigoCategory.GoogleCategoryId, context))
                {
                    _counters.UnchangedCategoryCount++;

                    if (!this.UpdateMapping(googleCategory, context))
                    {
                        _counters.ErrorCount++;
                    }

                    continue;
                }

                if (this.UpdateMapping(googleCategory, context))
                {
                    _counters.ChangedCategoryCount++;
                }
                else
                {
                    _counters.ErrorCount++;
                }
            }
        }

        private bool UpdateMapping(IGoogleCategory googleCategory, ProcessContext context)
        {
            bool isSuccess = true;

            try
            {
                this.Log.DebugFormat("Updating");

                this._indigoCategoryService.UpdateMapping(context.CurrentRecord.IndigoCategoryId,
                    googleCategory.GoogleCategoryId,
                    "NewGoogleCategoriesForIndigoCategoriesImporter");
            }
            catch (UpdateException ex)
            {
                this.Log.Error("UpdateException during UpdateMapping call.\n" + GetRecordErrorMessage(context), ex);
                isSuccess = false;
            }

            return isSuccess;
        }

        // An Error means that either the GoogleCategory or IndigoCategory could not be resolved by the resolution logic.
        public const string CountersLogMessage =
            "{0} updated mapping(s). {1} Error(s) occurred when processing input mapping file entries. {2} unchanged mapping(s) (or no breadcrumb provided in input).";

        private void LogCounters()
        {
            this.Log.InfoFormat(CountersLogMessage, 

                _counters.ChangedCategoryCount, _counters.ErrorCount, _counters.UnchangedCategoryCount);
        }

        private bool HasNewGoogleBreadcrumb(ProcessContext context)
        {
            if (string.IsNullOrWhiteSpace(context.CurrentRecord.NewGoogleBreadcrumb))
            {
                this.Log.Info("New Google Breadcrumb not provided. " + GetRecordErrorMessage(context));
                return false;
            }

            return true;
        }

        private bool IsUnchangedRecord(int matchedGoogleCategoryId, int? googleCategoryIdFromIndigoCategory, ProcessContext context)
        {
            if (matchedGoogleCategoryId == googleCategoryIdFromIndigoCategory)
            {
                this.Log.Info("Current Google category already mapped to input google category. " +
                              GetRecordErrorMessage(context));

                return true;
            }

            this.Log.DebugFormat("New Google Breadcrumb found");
            return false;
        }

        private IIndigoCategory FindIndigoCategory(ProcessContext context)
        {
            IIndigoCategory indigoCategory;
            if (!this.IndigoCategoryDictionary.TryGetValue(context.CurrentRecord.IndigoCategoryId, out indigoCategory))
            {
                this.Log.Error("Indigo category ID from input file not found in internal DB cache.\n" +
                               GetRecordErrorMessage(context));

                return null;
            }

            return indigoCategory;
        }

        private IGoogleCategory FindGoogleCategory(ProcessContext context)
        {
            IGoogleCategory result;

            if (this.GoogleCategoryDictionary.TryGetValue(context.CurrentRecord.NewGoogleBreadcrumb, out result))
            {
                this.Log.Debug("Google category found");
                return result;
            }

            this.Log.Error("Breadcrumb not found in set of Google Category entities. " + GetRecordErrorMessage(context));

            return null;
        }
    }
}
