using System;
using System.Configuration;
using System.IO;
using CommonUnitTestLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NewGoogleCategoriesForIndigoCategoriesImporter.Input;

namespace NewGoogleCategoriesForIndigoCategoriesImporterTests
{
    [TestClass]
    public class InputDataProviderTest
    {
        [TestMethod]
        public void GetInputSet_Test()
        {
            // Arrange

            string separator = ConfigurationManager.AppSettings["Separator"];

            string lines = string.Format(
@"5{0} indigobreadcrumb1{0} currentGoogleBreadCrumb1{0} newGoogleBreadCrumb1
  7{0} indigobreadcrumb2{0} currentGoogleBreadCrumb2{0} newGoogleBreadCrumb2"
                , separator);

            var inputDataProvider = new InputDataProvider(() => new StringReader(lines),
                CommonLibrary.SetupLogger().Object);

            // Act

            var result = inputDataProvider.GetInputSet();

            // Assert

            Assert.AreEqual(2, result.Count);

            var record1 = result[0];

            Assert.AreEqual(5, record1.IndigoCategoryId);
            Assert.AreEqual("indigobreadcrumb1", record1.IndigoBreadcrumb);
            Assert.AreEqual("currentGoogleBreadCrumb1", record1.CurrentGoogleBreadcrumb);
            Assert.AreEqual("newGoogleBreadCrumb1", record1.NewGoogleBreadcrumb);

            var record2 = result[1];

            Assert.AreEqual(7, record2.IndigoCategoryId);
            Assert.AreEqual("indigobreadcrumb2", record2.IndigoBreadcrumb);
            Assert.AreEqual("currentGoogleBreadCrumb2", record2.CurrentGoogleBreadcrumb);
            Assert.AreEqual("newGoogleBreadCrumb2", record2.NewGoogleBreadcrumb);
        }
    }
}
