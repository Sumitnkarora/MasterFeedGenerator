using Indigo.Feeds.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.ProductData.Models
{
    public class PrimaryInformation
    {
        [XmlAttribute(AttributeName = "AllowGiftWrap")]
        public string AllowGiftWrap;

        [XmlAttribute(AttributeName = "ExtendedDescription")]
        public string ExtendedDescription;

        [XmlAttribute(AttributeName = "IsPickupAllowed")]
        public string IsPickupAllowed;

        [XmlAttribute(AttributeName = "IsReturnable")]
        public string IsReturnable;

        [XmlAttribute(AttributeName = "IsShippingAllowed")]
        public string IsShippingAllowed;

        [XmlAttribute(AttributeName = "ReturnWindow")]
        public int ReturnWindow = ParameterUtils.GetParameter<int>("ReturnWindow");

        [XmlAttribute(AttributeName = "ShortDescription")]
        public string ShortDescription;

        [XmlAttribute(AttributeName = "ExtendedDisplayDescription")]
        public string ExtendedDisplayDescription;

        [XmlAttribute(AttributeName = "Status")]
        public string Status;

        [XmlAttribute(AttributeName = "ProductLine")]
        public string ProductLine;

        [XmlAttribute(AttributeName = "ImageLocation")]
        public string ImageLocation;

        [XmlAttribute(AttributeName = "UnitHeight")]
        public decimal UnitHeight;

        [XmlAttribute(AttributeName = "UnitHeightUOM")]
        public string UnitHeightUOM;

        [XmlAttribute(AttributeName = "UnitLength")]
        public decimal UnitLength;

        [XmlAttribute(AttributeName = "UnitLengthUOM")]
        public string UnitLengthUOM;
       

        [XmlAttribute(AttributeName = "UnitWeight")]
        public decimal UnitWeight;

        [XmlAttribute(AttributeName = "UnitWeightUOM")]
        public string UnitWeightUOM;
        

        [XmlAttribute(AttributeName = "UnitWidth")]
        public decimal UnitWidth;

        [XmlAttribute(AttributeName = "UnitWidthUOM")]
        public string UnitWidthUOM;
       

        [XmlIgnore]
        public string Catalogue { get; set; }

    }
}
