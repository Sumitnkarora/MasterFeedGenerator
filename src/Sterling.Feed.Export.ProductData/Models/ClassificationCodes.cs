using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.ProductData.Models
{
    public class ClassificationCodes
    {
        [XmlAttribute(AttributeName = "CommodityCode")]
        public string CommodityCode;

    }
}
