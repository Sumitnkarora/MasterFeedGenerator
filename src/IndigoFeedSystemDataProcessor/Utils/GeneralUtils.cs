using System;
using System.Collections.Generic;
using System.Linq;

namespace IndigoFeedSystemDataProcessor.Utils
{
    internal class GeneralUtils
    {
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(IEnumerable<TSource> source,
                                                             Func<TSource, TKey> keySelector)
        {
            var seenKeys = new HashSet<TKey>();
            return source.Where(element => seenKeys.Add(keySelector(element)));
        }

        public static IEnumerable<T> DeepCopy<T>(IEnumerable<T> list) where T : ICloneable
        {
            return new List<T>(list.Select(x => x.Clone()).Cast<T>());
        }

        public static bool IsNumeric(Object expression)
        {
            if (expression == null || expression is DateTime)
                return false;

            if (expression is Int16 || expression is Int32 || expression is Int64 || expression is Decimal ||
                expression is Single || expression is Double || expression is Boolean)
                return true;

            try
            {
                if (expression is string)
                    Double.Parse(expression as string);
                else
                    Double.Parse(expression.ToString());
                return true;
            }
            catch
            {
            } // just dismiss errors but return false
            return false;
        }

        public static bool IsBoolean(string input)
        {
            bool dummy;
            return bool.TryParse(input, out dummy);
        }
    }
}
