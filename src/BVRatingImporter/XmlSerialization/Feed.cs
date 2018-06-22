using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using BVRatingImporter.Execution;

namespace BVRatingImporter.XmlSerialization
{
    /// <summary>
    /// Schema is available at http://www.bazaarvoice.com/xs/PRR/StandardClientFeed/5.6
    /// </summary>
    [XmlRoot("Feed", Namespace = "http://www.bazaarvoice.com/xs/PRR/StandardClientFeed/5.6")]
    public class Feed
    {
        [XmlNamespaceDeclarations]
        public XmlSerializerNamespaces XmlNamespaces
        {
            get
            {
                var xsn = new XmlSerializerNamespaces();
                xsn.Add("", "http://www.bazaarvoice.com/xs/PRR/StandardClientFeed/5.6");
                return xsn;
            }
        }

        [XmlAttribute(AttributeName = "name")]
        public string ClientName { get; set; }

        [XmlAttribute(AttributeName = "extractDate")]
        public DateTime ExtractDate { get; set; }

        public List<Product> Products { get; set; }
    }

    public class Product
    {
        [XmlAttribute("id")]
        public string Id { get; set; }

        [XmlElement("ReviewStatistics")]
        public ReviewStatistics ReviewStatistics { get; set; }

        [XmlElement("NativeReviewStatistics")]
        public ReviewStatistics NativeReviewStatistics { get; set; }

        //[XmlArray("Reviews")]
        //public List<Review> Reviews { get; set; }
    }

    public class ReviewStatistics
    {
        //[XmlElement("AverageOverallRating")]
        //public float AverageOverallRating { get; set; }

        //[XmlElement("OverallRatingRange")]
        //public int OverallRatingRange { get; set; }

        [XmlElement("TotalReviewCount")]
        public int TotalReviewCount { get; set; }

        //[XmlElement("RatingsOnlyReviewCount")]
        //public int RatingsOnlyReviewCount { get; set; }

        //[XmlElement("RecommendedCount")]
        //public int RecommendedCount { get; set; }

        //[XmlElement("NotRecommendedCount")]
        //public int NotRecommendedCount { get; set; }

        //[XmlArray("AverageRatingValues")]
        //public List<AverageRatingValue> AverageRatingValues { get; set; }

        [XmlArray("RatingDistribution")]
        public List<RatingDistributionItem> RatingDistribution { get; set; }

        [XmlArray("LocaleDistribution")]
        public List<LocaleDistributionItem> LocaleDistribution { get; set; }

        public static readonly ReviewStatistics Empty = new ReviewStatistics();
    }

    public class RatingDistributionItem
    {
        [XmlElement("RatingValue")]
        public int RatingValue { get; set; }

        [XmlElement("Count")]
        public int Count { get; set; }
    }

    public class LocaleDistributionItem
    {
        [XmlElement("DisplayLocale")]
        public string DisplayLocale { get; set; }

        [XmlElement("ReviewStatistics")]
        public ReviewStatistics ReviewStatistics { get; set; }

        public static readonly LocaleDistributionItem Empty = new LocaleDistributionItem();

        public static LocaleDistributionItem operator +(LocaleDistributionItem left, LocaleDistributionItem right)
        {
            if (!left.DisplayLocale.Equals(right.DisplayLocale, StringComparison.OrdinalIgnoreCase))
            {
                throw new ApplicationException("Expected equal general locales in both operands.");
            }

            var ratingDistribution = new List<RatingDistributionItem>(5);

            for (int i = 1; i <= 5; i++)
            {
                var leftCount = Runner.GetRatingDistributionItemCount(i, left);
                var rightCount = Runner.GetRatingDistributionItemCount(i, right);

                if (leftCount == null && rightCount == null)
                    continue;

                var ratingDistributionItem = new RatingDistributionItem
                {
                    RatingValue = i,
                    Count = (leftCount ?? 0) + (rightCount ?? 0)
                };

                ratingDistribution.Add(ratingDistributionItem);
            }

            var result = new LocaleDistributionItem
            {
                DisplayLocale = left.DisplayLocale,
                ReviewStatistics = new ReviewStatistics
                {
                    RatingDistribution = ratingDistribution,
                    TotalReviewCount = left.ReviewStatistics.TotalReviewCount + right.ReviewStatistics.TotalReviewCount
                }
            };

            return result;
        }
    }

    //public class Review : CommentReviewCommonFields
    //{
    //    [XmlAttribute(AttributeName = "id")]
    //    public string Id { get; set; }

    //    [XmlElement("RatingsOnly")]
    //    public bool IsRatingsOnly { get; set; }

    //    [XmlElement("ReviewText")]
    //    public string ReviewText { get; set; }

    //    [XmlElement("NumComments")]
    //    public int NumComments { get; set; }

    //    [XmlElement("Rating")]
    //    public int Rating { get; set; }
    //    public bool ShouldSerializeRating()
    //    {
    //        return Rating > 0;
    //    }

    //    [XmlElement("RatingRange")]
    //    public int RatingRange { get; set; }
    //    public bool ShouldSerializeRatingRange()
    //    {
    //        return Rating > 0;
    //    }

    //    [XmlElement("Recommended")]
    //    public bool IsRecommended { get; set; }
    //    public bool ShouldSerializeIsRecommended()
    //    {
    //        return IsRecommended;
    //    }

    //    //[XmlElement("ProductReviewsUrl")]
    //    //public string ProductReviewsUrl { get; set; }

    //    //[XmlElement("ProductReviewsDeepLinkedUrl")]
    //    //public string ProductReviewsDeepLinkedUrl { get; set; }

    //    [XmlElement("Featured")]
    //    public bool IsFeatured { get; set; }

    //    [XmlElement("SendEmailAlertWhenCommented")]
    //    public bool SendEmailAlertWhenCommented { get; set; }

    //    [XmlArray("ClientResponses")]
    //    public List<ClientResponse> ClientResponses { get; set; }

    //    [XmlArray("Comments")]
    //    public List<Comment> Comments { get; set; }

    //    //[XmlArray("ClientComments")]
    //    //public List<ClientComment> ClientComments { get; set; }

    //    [XmlIgnore]
    //    public ContentType ContentType { get; set; }
    //}

    //public class UserProfileReference
    //{
    //    [XmlAttribute(AttributeName = "id")]
    //    public string Id { get; set; }

    //    [XmlElement("ExternalId")]
    //    public string ExternalId { get; set; }

    //    [XmlElement("DisplayName")]
    //    public string DisplayName { get; set; }

    //    [XmlElement("Anonymous")]
    //    public bool IsAnonymous { get; set; }

    //    [XmlElement("HyperlinkingEnabled")]
    //    public bool IsHyperlinkingEnabled { get; set; }
    //}

    //public class ClientResponse
    //{
    //    [XmlElement("Department")]
    //    public string Department { get; set; }

    //    [XmlElement("Name")]
    //    public string Name { get; set; }

    //    [XmlElement("Response")]
    //    public string Response { get; set; }

    //    //[XmlElement("ResponseMarkup")]
    //    //public MarkupType ResponseMarkup { get; set; }

    //    [XmlElement("ResponseType")]
    //    public string ResponseType { get; set; }

    //    [XmlElement("ResponseSource")]
    //    public string ResponseSource { get; set; }

    //    [XmlElement("Date")]
    //    public DateTime Date { get; set; }

    //    //[XmlArray("ProductReferences")]
    //    //public List<ProductReference> ProductReferences { get; set; }
    //}

    //public class ProductReference
    //{
    //    [XmlElement("Product")]
    //    public FeedProduct Product { get; set; }

    //    [XmlElement("Caption")]
    //    public string Caption { get; set; }
    //}

    //public class FeedProduct
    //{
    //    [XmlAttribute(AttributeName = "id")]
    //    public string Id { get; set; }

    //    [XmlElement("ExternalId")]
    //    public string ExternalId { get; set; }
    //}

    //public class Comment : CommentReviewCommonFields
    //{
    //    [XmlElement("CommentText")]
    //    public string CommentText { get; set; }

    //    [XmlElement("UserNickname")]
    //    public string UserNickname { get; set; }

    //    [XmlAttribute(AttributeName = "id")]
    //    public string Id { get; set; }
    //}

    //public class ClientComment
    //{
    //    [XmlElement("Date")]
    //    public DateTime Date { get; set; }

    //    [XmlElement("Name")]
    //    public string Name { get; set; }

    //    [XmlElement("Comment")]
    //    public Comment Comment { get; set; }
    //}

    //public class CommentReviewCommonFields
    //{
    //    [XmlElement("ModerationStatus")]
    //    public ModerationStatusType ModerationStatus { get; set; }

    //    [XmlElement("LastModificationTime")]
    //    public DateTime LastModificationTime { get; set; }

    //    [XmlElement("UserProfileReference")]
    //    public UserProfileReference UserProfileReference { get; set; }

    //    [XmlElement("Title")]
    //    public string Title { get; set; }
    //    public bool ShouldSerializeTitle()
    //    {
    //        return !string.IsNullOrWhiteSpace(Title);
    //    }

    //    [XmlElement("CampaignId")]
    //    public string CampaignId { get; set; }
    //    public bool ShouldSerializeCampaignId()
    //    {
    //        return !string.IsNullOrWhiteSpace(CampaignId);
    //    }

    //    [XmlElement("NumFeedbacks")]
    //    public int NumFeedbacks { get; set; }

    //    [XmlElement("NumPositiveFeedbacks")]
    //    public int NumPositiveFeedbacks { get; set; }

    //    [XmlElement("NumNegativeFeedbacks")]
    //    public int NumNegativeFeedbacks { get; set; }

    //    [XmlElement("DisplayLocale")]
    //    public LocaleType DisplayLocale { get; set; }
    //    public bool ShouldSerializeDisplayLocale()
    //    {
    //        return DisplayLocale != LocaleType.NA;
    //    }

    //    [XmlElement("SubmissionTime")]
    //    public DateTime SubmissionTime { get; set; }

    //    [XmlElement("AuthenticationType")]
    //    public AuthenticationType AuthenticationType { get; set; }

    //    [XmlElement("UserEmailAddress")]
    //    public string UserEmailAddress { get; set; }
    //    public bool ShouldSerializeUserEmailAddress()
    //    {
    //        return !string.IsNullOrWhiteSpace(UserEmailAddress);
    //    }

    //    [XmlElement("SendEmailAlertWhenPublished")]
    //    public bool SendEmailAlertWhenPublished { get; set; }

    //    [XmlElement("OriginatingDisplayCode")]
    //    public string OriginatingDisplayCode { get; set; }
    //    public bool ShouldSerializeOriginatingDisplayCode()
    //    {
    //        return !string.IsNullOrWhiteSpace(OriginatingDisplayCode);
    //    }

    //    [XmlElement("FirstPublishTime")]
    //    public DateTime FirstPublishTime { get; set; }

    //    [XmlElement("LastPublishTime")]
    //    public DateTime LastPublishTime { get; set; }

    //    [XmlArray("ProductReferences")]
    //    public List<ProductReference> ProductReferences { get; set; }
    //}

    //public class UserInfo
    //{
    //    public UserProfileReference UserProfileReference { get; set; }
    //    public string Email { get; set; }
    //    public string Name { get; set; }
    //}

    //public enum LocaleType
    //{
    //    en_CA,
    //    fr_CA,
    //    en_US,
    //    es_US,
    //    fr_US,
    //    fr_FR,
    //    en_GB,
    //    en_AU,
    //    en,
    //    fr,
    //    NA
    //}
}
