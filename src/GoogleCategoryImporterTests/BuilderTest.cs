using System;
using System.IO;
using Castle.Core.Logging;
using CommonUnitTestLibrary;
using FeedGenerators.Core;
using GoogleCategoryImporter;
using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Services.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace GoogleCategoryImporterTests
{
    [TestClass]
    public class BuilderTest
    {
        private Builder builder;

        private Mock<IGoogleCategoryService> googleCategoryServiceMock;
        private Mock<ILogger> loggerMock;

        private MockGoogleCategory googleCategory0;
        private MockGoogleCategory googleCategory1;

        [TestInitialize]
        public void Initialize()
        {
            googleCategoryServiceMock = new Mock<IGoogleCategoryService>();
            this.loggerMock = new Mock<ILogger>();

            this.loggerMock = CommonLibrary.SetupLogger();

            this.InitMockCategories();

            string input =
@"# Google_Product_Taxonomy_Version: 2015-02-19
1 - Animals & Pet Supplies
3237 - Animals & Pet Supplies > Live Animals
";
            this.BuilderSetup(input);
        }

        private void BuilderSetup(string input)
        {
            var reader = new StringReader(input);
            var pathResolver = new Mock<PathResolver>();
            pathResolver.Setup(m => m.HasInputFile()).Returns(true);

            this.builder = new Builder(this.googleCategoryServiceMock.Object, pathResolver.Object, () => reader);
            this.builder.Log = this.loggerMock.Object;
        }

        private void SetupVerifyUpdate()
        {
            this.googleCategoryServiceMock.Setup(m => m.Update(It.Is<IGoogleCategory>(c =>
                c.BreadcrumbPath == this.googleCategory0.BreadcrumbPath &&
                c.ChildrenExist == this.googleCategory0.ChildrenExist &&
                c.DateModified == this.googleCategory0.DateModified &&
                c.GoogleNativeCategoryId == this.googleCategory0.GoogleNativeCategoryId &&
                c.Name == this.googleCategory0.Name &&
                c.ParentId == null &&
                c.GoogleCategoryId == this.googleCategory0.GoogleCategoryId

                ))).Returns(this.googleCategory0).Verifiable();

            this.googleCategoryServiceMock.Setup(m => m.Update(It.Is<IGoogleCategory>(c =>
                c.BreadcrumbPath == this.googleCategory1.BreadcrumbPath &&
                c.ChildrenExist == this.googleCategory1.ChildrenExist &&
                c.DateModified == this.googleCategory1.DateModified &&
                c.GoogleNativeCategoryId == this.googleCategory1.GoogleNativeCategoryId &&
                c.Name == this.googleCategory1.Name &&
                c.ParentId == this.googleCategory1.ParentId &&
                c.GoogleCategoryId == this.googleCategory1.GoogleCategoryId

                ))).Returns(this.googleCategory1).Verifiable();            
        }

        private void InitMockCategories()
        {
            var dateModified = DateTime.Parse("2015-02-19");

            var googleCategoryId0 = 2;
            var googleNativeCategoryId0 = 1;

            var googleCategoryId1 = 3;
            var googleNativeCategoryId1 = 3237;

            this.googleCategory0 = new MockGoogleCategory
            {
                DateModified = dateModified,
                GoogleNativeCategoryId = googleNativeCategoryId0,
                Name = "Animals & Pet Supplies",
                ParentId = null,
                GoogleCategoryId = googleCategoryId0,
                BreadcrumbPath = "Animals & Pet Supplies",
                ChildrenExist = false,
            };

            this.googleCategory1 = new MockGoogleCategory
            {
                DateModified = dateModified,
                GoogleNativeCategoryId = googleNativeCategoryId1,
                Name = "Live Animals",
                ParentId = googleCategory0.GoogleCategoryId,
                GoogleCategoryId = googleCategoryId1,
                BreadcrumbPath = googleCategory0.BreadcrumbPath + " > " + "Live Animals",
                ChildrenExist = true,
            };
        }

        [TestMethod]
        public void Builder_Update_Test()
        {
            // Arrange

            this.SetupVerifyUpdate();

            this.googleCategoryServiceMock.Setup(m => m.GetAllGoogleCategories())
                .Returns(new[]
                {
                    this.googleCategory0,
                    this.googleCategory1
                });
            
            // Act

            this.builder.Build(null);

            // Assert

            this.googleCategoryServiceMock.Verify();
        }

        [TestMethod]
        public void Builder_UpdateDelete_Test()
        {
            // Assert

            this.SetupVerifyUpdate();

            var categoryToDelete = new MockGoogleCategory
            {
                GoogleCategoryId = 1,
                BreadcrumbPath = "Category to delete",
            };

            this.googleCategoryServiceMock.Setup(m => m.GetAllGoogleCategories())
                .Returns(new[]
                {
                    this.googleCategory0,
                    categoryToDelete,
                    this.googleCategory1
                });
            
            // Act

            this.builder.Build(null);

            // Assert

            this.googleCategoryServiceMock.Verify(
                m =>
                    m.Delete(It.Is<int>(id => id == categoryToDelete.GoogleCategoryId)),
                Times.Once());

            this.googleCategoryServiceMock.Verify(
                m =>
                    m.Delete(It.Is<int>(id => id == googleCategory0.GoogleCategoryId)),
                Times.Never());

            this.googleCategoryServiceMock.Verify(
                m =>
                    m.Delete(It.Is<int>(id => id == googleCategory1.GoogleCategoryId)),
                Times.Never());
        }

        [TestMethod]
        public void Builder_Insert()
        {
            // Arrange
            
            this.googleCategoryServiceMock.Setup(m => m.GetAllGoogleCategories())
                .Returns(new IGoogleCategory[] {});

            this.googleCategoryServiceMock.Setup(m => m.Insert(It.Is<IGoogleCategory>(c =>
                c.BreadcrumbPath == this.googleCategory0.BreadcrumbPath &&
                c.ChildrenExist == this.googleCategory0.ChildrenExist &&
                c.DateModified == this.googleCategory0.DateModified &&
                c.GoogleNativeCategoryId == this.googleCategory0.GoogleNativeCategoryId &&
                c.Name == this.googleCategory0.Name &&
                c.ParentId == null 

                ))).Returns(this.googleCategory0).Verifiable();

            this.googleCategoryServiceMock.Setup(m => m.Insert(It.Is<IGoogleCategory>(c =>
                c.BreadcrumbPath == this.googleCategory1.BreadcrumbPath &&
                c.ChildrenExist == this.googleCategory1.ChildrenExist &&
                c.DateModified == this.googleCategory1.DateModified &&
                c.GoogleNativeCategoryId == this.googleCategory1.GoogleNativeCategoryId &&
                c.Name == this.googleCategory1.Name &&
                c.ParentId == this.googleCategory1.ParentId 

                ))).Returns(this.googleCategory1).Verifiable();

            // Act

            this.builder.Build(null);

            // Assert

            this.googleCategoryServiceMock.Verify();
            this.googleCategoryServiceMock.Verify(m => m.Update(It.IsAny<IGoogleCategory>()), Times.Never());
            this.googleCategoryServiceMock.Verify(m => m.Delete(It.IsAny<int>()), Times.Never());
        }
    }
}
