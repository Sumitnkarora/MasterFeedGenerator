using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Endeca.Navigation;

namespace IndigoFeedSystemDataProcessor.Utils
{
    internal static class Extensions
    {
        public static string GetFrenchName(this DimVal dimVal)
        {
            var result = (string)dimVal.Properties["localization_fr"];
            return result;
        }
    }
}
