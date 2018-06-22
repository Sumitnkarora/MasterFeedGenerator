using BVRatingImporter.XmlSerialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BVRatingImporter.Entities
{
    public class ProductRating: IEquatable<ProductRating>
    {
        public long PID { get; protected set; }
        public string LanguageString { get; protected set; }

        public RatingCounts NativeRatings { get; protected set; }
        public RatingCounts ExternalRatings { get; protected set; }

        public int NativeTotalReviewCount { get; protected set; }
        public int ExternalTotalReviewCount { get; protected set; }
        public int CombinedTotalReviewCount { get; protected set; }

        public class RatingCounts : IEquatable<RatingCounts>
        {
            public RatingCounts()
            {
            }

            public RatingCounts(IEnumerable<RatingDistributionItem> ratingDistributionItems)
            {
                if (ratingDistributionItems == null)
                    return;

                ratingDistributionItems.ToList().ForEach(item =>
                {
                    if (item.RatingValue > 0)
                    {
                        this[item.RatingValue] = item.Count;
                    }
                });
            }
            
            private readonly int[] _ratings = new int[5];

            public int this[int rating]
            {
                get { return _ratings[rating - 1]; }
                set { _ratings[rating - 1] = value; }
            }

            public bool Equals(RatingCounts other)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (_ratings[i] != other._ratings[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            public override string ToString()
            {
                var stringBuilder = new StringBuilder();

                for (int i = 0; i < 5; i++)
                    stringBuilder.AppendFormat("Rating: {0}, Count: {1} - ", i + 1, _ratings[i]);

                stringBuilder.Remove(stringBuilder.Length - 3, 3);

                return stringBuilder.ToString();
            }
        }

        // Constructor for database end.
        public ProductRating(long pid, string languageString, int nativeTotalReviewCount, int externalTotalReviewCount,
            int combinedTotalReviewCount, RatingCounts nativeRatings, RatingCounts externalRatings)
        {
            PID = pid;
            LanguageString = languageString;

            NativeRatings = nativeRatings;
            ExternalRatings = externalRatings;

            NativeTotalReviewCount = nativeTotalReviewCount;
            ExternalTotalReviewCount = externalTotalReviewCount;
            CombinedTotalReviewCount = combinedTotalReviewCount;
        }

        // Constructor for xml file end.
        public ProductRating(long pid, string generalLocaleString,
            ReviewStatistics nativeReviewStatistics, ReviewStatistics externalReviewStatistics)
        {
            PID = pid;
            LanguageString = generalLocaleString;
            
            NativeRatings = nativeReviewStatistics != null
                ? new RatingCounts(nativeReviewStatistics.RatingDistribution)
                : new RatingCounts();

            ExternalRatings = externalReviewStatistics != null
                ? new RatingCounts(externalReviewStatistics.RatingDistribution)
                : new RatingCounts();

            NativeTotalReviewCount = nativeReviewStatistics != null ? nativeReviewStatistics.TotalReviewCount : 0;
            ExternalTotalReviewCount = externalReviewStatistics != null ? externalReviewStatistics.TotalReviewCount : 0;
        }

        public bool Equals(ProductRating other)
        {
            var result = PID == other.PID && LanguageString.Equals(other.LanguageString, StringComparison.Ordinal)
                         && NativeRatings.Equals(other.NativeRatings) && ExternalRatings.Equals(other.ExternalRatings);

            return result;
        }

        public override string ToString()
        {
            var result = string.Format("PID: {0}, Language: {1}, NativeRatings {2}, ExternalRatings {3}", PID,
                LanguageString, NativeRatings, ExternalRatings);

            return result;
        }
    }
}
