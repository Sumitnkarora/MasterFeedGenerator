using System.Collections.Generic;
using Indigo.Feeds.Generator.Core.Models;
using System;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.PosData.Models
{
    [XmlRoot("Items")]
    public class PosTransaction : ExportData
    {
        [XmlElement(ElementName ="Item")]
        public List<Item> ItemList { get; set; }
    }
}
