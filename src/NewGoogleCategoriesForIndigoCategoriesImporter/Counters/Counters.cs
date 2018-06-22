using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGoogleCategoriesForIndigoCategoriesImporter.Counters
{
    internal class Counters : ICounterResults
    {
        public int ErrorCount { get; set; }

        public int ChangedCategoryCount { get; set; }

        public int UnchangedCategoryCount { get; set; }
    }
}
