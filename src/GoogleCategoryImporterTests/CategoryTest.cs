using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GoogleCategoryImporter;

namespace GoogleCategoryImporterTests
{
    [TestClass]
    public class CategoryTest
    {
        [TestMethod]
        public void Category_NativeCategoryIdParseException_FormatException_Test()
        {
            // Arrange
            const string breadcrumb = "xxx - Animals & Pet Supplies";

            // Act
            var innerException = this.Category_NativeCategoryIdParseException_Test(breadcrumb);

            // Assert

            Assert.AreEqual(typeof(FormatException), innerException.GetType());
        }

        [TestMethod]
        public void Category_NativeCategoryIdParseException_Overflow_Test()
        {
            // Arrange
            var breadcrumb = string.Format("{0} - Animals & Pet Supplies", int.MaxValue + "0");

            // Act
            var innerException = this.Category_NativeCategoryIdParseException_Test(breadcrumb);

            // Assert

            Assert.AreEqual(typeof(OverflowException), innerException.GetType());
        }

        private Exception Category_NativeCategoryIdParseException_Test(string breadcrumb)
        {
            // Arrange
            
            Category.NativeCategoryIdParseException exception = null;

            // Act

            try
            {
                new Category(breadcrumb, DateTime.Now);
            }
            catch (Category.NativeCategoryIdParseException ex)
            {
                exception = ex;
            }

            // Assert

            Assert.IsNotNull(exception);
            Assert.AreEqual(typeof (Category.NativeCategoryIdParseException), exception.GetType());

            return exception.InnerException;
        }
    }
}
