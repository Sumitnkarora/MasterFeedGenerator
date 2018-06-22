using System;
using System.Collections.Generic;
using Castle.Core.Logging;
using CommonUnitTestLibrary;
using FeedGenerators.Core;
using FeedGenerators.Core.Models;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Services.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NewGoogleCategoriesForIndigoCategoriesImporter.Input;
using Moq;
using NewGoogleCategoriesForIndigoCategoriesImporter;

namespace NewGoogleCategoriesForIndigoCategoriesImporterTests
{
    [TestClass]
    public class BuilderTest
    {
        private Mock<IGoogleCategoryService> googleCategoryService;
        private Mock<IIndigoCategoryService> indigoCategoryService;
        private Mock<IInputDataProvider> inputDataProvider;
        private Mock<ILogger> logger;
        private Mock<PathResolver> pathResolver;

        [TestInitialize]
        public void Initialize()
        {
            this.googleCategoryService = new Mock<IGoogleCategoryService>();
            this.indigoCategoryService = new Mock<IIndigoCategoryService>();
            this.inputDataProvider = new Mock<IInputDataProvider>();
            this.logger = CommonLibrary.SetupLogger();
            this.pathResolver = new Mock<PathResolver>();
        }

        [TestMethod]
        public void Builder_Test()
        {
            // Arrange

            var i = 1;

            var inputRecords = new List<InputRecord>
            {
                new InputRecord
                {
                    NewGoogleBreadcrumb = CommonLibrary.NewGuid,
                    IndigoCategoryId = i++
                },
                new InputRecord
                {
                    NewGoogleBreadcrumb = CommonLibrary.NewGuid,
                    IndigoCategoryId = i++
                },
                new InputRecord
                {
                    NewGoogleBreadcrumb = CommonLibrary.NewGuid,
                    IndigoCategoryId = i++
                },
                new InputRecord
                {
                    NewGoogleBreadcrumb = string.Empty,
                    IndigoCategoryId = i++
                },
                new InputRecord
                {
                    NewGoogleBreadcrumb = CommonLibrary.NewGuid,
                    IndigoCategoryId = i++
                },
            };

            this.inputDataProvider.Setup(m => m.GetInputSet()).Returns(inputRecords);

            // Source of data for target within the builder
            var googleCategories = new List<IGoogleCategory>
            {
                new MockGoogleCategory
                {
                    BreadcrumbPath = inputRecords[0].NewGoogleBreadcrumb,
                    GoogleCategoryId = i++
                },
                new MockGoogleCategory
                {
                    BreadcrumbPath = inputRecords[2].NewGoogleBreadcrumb,
                    GoogleCategoryId = i++
                },
                new MockGoogleCategory
                {
                    BreadcrumbPath = inputRecords[4].NewGoogleBreadcrumb,
                    GoogleCategoryId = i++
                },
            };

            var indigoCategories = new List<MockIndigoCategory>();
            foreach (var input in inputRecords)
            {
                indigoCategories.Add(new MockIndigoCategory
                {
                     IndigoCategoryId = input.IndigoCategoryId,
                });
            }

            indigoCategories[4].GoogleCategoryId = googleCategories[2].GoogleCategoryId;

            this.indigoCategoryService.Setup(m => m.GetAllIndigoCategories()).Returns(indigoCategories);

            this.googleCategoryService.Setup(m => m.GetAllGoogleCategories()).Returns(googleCategories);

            var builder = new Builder(this.googleCategoryService.Object, this.indigoCategoryService.Object,
                this.inputDataProvider.Object, this.pathResolver.Object);

            builder.Log = this.logger.Object;

            this.pathResolver.Setup(m => m.HasInputFile()).Returns(true);
            
            // Act

            builder.Build(null);

            // Assert

            this.indigoCategoryService.Verify(
                m =>
                    m.UpdateMapping(inputRecords[0].IndigoCategoryId, googleCategories[0].GoogleCategoryId,
                        "NewGoogleCategoriesForIndigoCategoriesImporter"), Times.Once());

            this.indigoCategoryService.Verify(
                m =>
                    m.UpdateMapping(inputRecords[1].IndigoCategoryId, It.IsAny<int?>(),
                        It.IsAny<string>()), Times.Never());

            this.indigoCategoryService.Verify(
                m =>
                    m.UpdateMapping(inputRecords[2].IndigoCategoryId, googleCategories[1].GoogleCategoryId,
                        "NewGoogleCategoriesForIndigoCategoriesImporter"), Times.Once());

            this.indigoCategoryService.Verify(
                m =>
                    m.UpdateMapping(inputRecords[3].IndigoCategoryId, It.IsAny<int?>(),
                        It.IsAny<string>()), Times.Never());

            Assert.AreEqual(2, builder.Counters.ChangedCategoryCount);
            Assert.AreEqual(1, builder.Counters.ErrorCount);
            Assert.AreEqual(2, builder.Counters.UnchangedCategoryCount);

            this.logger.Verify(m => m.InfoFormat(Builder.CountersLogMessage, 2, 1, 2));
        }
    }
}
