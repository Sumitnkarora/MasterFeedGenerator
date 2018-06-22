using Castle.Core.Logging;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using FeedGenerators.Core;
using FeedGenerators.Core.Models;
using Indigo.Feeds.Utils;

namespace GoogleCategoryImporter
{
    public class Builder : IBuilder
    {
        private readonly IGoogleCategoryService _googleCategoryService;
        private readonly PathResolver _pathResolver;

        private Func<TextReader> _createReader;
        private Func<TextReader> CreateReader
        {
            get
            {
                return _createReader ??
                       (_createReader = () => File.OpenText(_pathResolver.GetInputFilePath()));
            }
        }

        private static readonly string GoogleDateModifier = ParameterUtils.GetParameter<string>("GoogleDateModifier");

        public ILogger Log { get; set; }

        // Mock constructor
        public Builder(IGoogleCategoryService googleCategoryService, PathResolver pathResolver, Func<TextReader> createReader)
            : this(googleCategoryService, pathResolver)
        {
            _createReader = createReader;
        }

        public Builder(IGoogleCategoryService googleCategoryService, PathResolver pathResolver)
        {
            _googleCategoryService = googleCategoryService;
            _pathResolver = pathResolver;
        }

        private Dictionary<string, GoogleCategoryWrapper> _googleCategoryDictionary;
        public Dictionary<string, GoogleCategoryWrapper> GoogleCategoryDictionary
        {
            get
            {
                var result = _googleCategoryDictionary ??
                             
                    (_googleCategoryDictionary = 
                             
                             _googleCategoryService.GetAllGoogleCategories()
                                 .Select(googleCategory => new GoogleCategoryWrapper(googleCategory))
                                 .ToDictionary(p => p.BreadcrumbPath.Trim())
                    );

                return result;
            }
        }

        public void Build(string[] args)
        {
            Log.Info("Execution started.");

            try
            {
                // Check if the input file exists. If not, exit.
                if (!_pathResolver.HasInputFile())
                {
                    Log.Info("No input file was found. Exiting execution.");
                    return;
                }

                var isUpdateMode = this.GoogleCategoryDictionary.Any();

                var lineCount = this.UpdateExistingGoogleCategoryOrInsert();

                Log.InfoFormat("{0} entries were updated or added to the database", lineCount);

                if (isUpdateMode)
                {
                    lineCount = this.DeleteNonProcessedGoogleCategories();
                }

                Log.InfoFormat("{0} entries were deleted from the database", lineCount);
            }
            catch (Exception ex)
            {
                Log.Error("There was an issue during execution. Exiting the application!.", ex);
                throw;
            }

            Log.Info("Completed successfully.");
        }

        private int DeleteNonProcessedGoogleCategories()
        {
            var googleCategories =
                this.GoogleCategoryDictionary.Where(entry => !entry.Value.IsModified)
                    .Select(entry => entry.Value)
                    .ToList();

            int workingCategoryId = 0;
            try
            {
                foreach (var googleCategory in googleCategories)
                {
                    workingCategoryId = googleCategory.GoogleCategoryId;
                    _googleCategoryService.Delete(googleCategory.GoogleCategoryId);
                }
            }
            catch (SqlException ex)
            {
                throw new ApplicationException(
                    "Sql Exception calling Delete on the Google Category service. GoogleCategoryId: " +
                    workingCategoryId, ex);
            }

            return googleCategories.Count;
        }

        private IGoogleCategory UpdateOrInsertGoogleCategory(IGoogleCategory googleCategory)
        {
            GoogleCategoryWrapper googleCategoryWrapper;
            IGoogleCategory result;

            // Works for Update or Insert mode. If there's nothing in the dictionary the insert branch is taken anyway.
            if (this.GoogleCategoryDictionary.TryGetValue(googleCategory.BreadcrumbPath.Trim(), out googleCategoryWrapper))
            {
                googleCategory.GoogleCategoryId = googleCategoryWrapper.GoogleCategoryId;

                result = _googleCategoryService.Update(googleCategory);

                googleCategoryWrapper.IsModified = true;
            }
            else
            {
                result = _googleCategoryService.Insert(googleCategory);
            }

            return result;
        }

        private int UpdateExistingGoogleCategoryOrInsert()
        {
            var modifiedDate = DateTime.Now;
            var lineCount = 0;

            // Open the file and start looping through its content
            using (var reader = this.CreateReader())
            {
                string line;
                var childTreeMappings = new Dictionary<string, IGoogleCategory>();
                while ((line = reader.ReadLine()) != null)
                {
                    // On first line, check if we have the modified text from Google and set the last modified value accordingly
                    if (lineCount == 0)
                    {
                        // Increment the line count and never worry about it again
                        lineCount++;
                        if (!line.ToLowerInvariant().Contains(GoogleDateModifier.ToLowerInvariant())) continue;
                        var parts = line.Split(new[] { GoogleDateModifier }, StringSplitOptions.None);
                        if (parts.Length == 2)
                        {
                            DateTime dummy;
                            if (DateTime.TryParse(parts[1], out dummy))
                                modifiedDate = dummy;
                        }

                        continue;
                    }

                    var newCategory = new Category(line.Trim(), modifiedDate);

                    // Check if we're at the beginning of a new childTree
                    if (newCategory.Level == 1)
                        childTreeMappings = new Dictionary<string, IGoogleCategory>();
                    else
                        newCategory.ParentId = childTreeMappings[newCategory.BreadcrumbTrail].GoogleCategoryId;

                    childTreeMappings.Add(newCategory.BreadcrumbPath, UpdateOrInsertGoogleCategory(newCategory));

                    lineCount++;
                }
            }

            return lineCount;
        }
    }
}
