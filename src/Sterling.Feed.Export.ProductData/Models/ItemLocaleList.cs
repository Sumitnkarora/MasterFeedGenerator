using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.ProductData.Models
{
    public class ItemLocaleList
    {
        public ItemLocaleList()
        {
            ItemLocale = new ItemLocale();
        }

        [XmlAttribute(AttributeName = "Reset")]
        public string Reset;

        [XmlElement("ItemLocale")]
        public ItemLocale ItemLocale;

    }
}
