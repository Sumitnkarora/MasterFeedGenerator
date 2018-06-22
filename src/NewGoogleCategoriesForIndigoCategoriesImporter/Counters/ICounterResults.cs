using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGoogleCategoriesForIndigoCategoriesImporter.Counters
{
    public interface ICounterResults
    {
        int ErrorCount { get; }
        int ChangedCategoryCount { get; }
        int UnchangedCategoryCount { get; }
    }
}
