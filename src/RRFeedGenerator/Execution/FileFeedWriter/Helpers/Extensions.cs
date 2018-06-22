
using System;
using System.Collections.Generic;
using System.Linq;

namespace RRFeedGenerator.Execution.FileFeedWriter.Helpers
{
    public static class Extensions
    {
        public static object DbNullToNull(this object value)
        {
            return Utility.DbNullToNull(value);
        }

        public static object EmptyToNull(this object value)
        {
            var workingValue = value as string;

            if (workingValue == null)
                return value;

            return Utility.EmptyToNull(workingValue);
        }

        public static object EmptyOrDbNullToNull(this object value)
        {
            return value.DbNullToNull().EmptyToNull();
        }

        public static object PrintIfNull(this object value)
        {
            if (value == null)
                return "<null>";

            return value;
        }

        public static string PrintElements<T>(this List<T> input)
        {
            const int magicLimit = 10;
            
            if (input == null)
                return null;

            string result = string.Join(",", input.Select(i => i.ToString()).ToArray(), 0, Math.Min(input.Count, magicLimit));

            if (input.Count > magicLimit)
                result += "...";
            
            return result;
        }
    }
}
