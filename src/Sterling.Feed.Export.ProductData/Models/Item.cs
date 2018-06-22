using Indigo.Feeds.Generator.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.ProductData.Models
{
    public class Item : ExportData
    {
        [XmlAttribute(AttributeName = "ItemID")]
        public string ItemID;

        [XmlAttribute(AttributeName = "SyncTS")]
        public string SyncTS;

        [XmlAttribute(AttributeName = "Action")]
        public string Action;

        [XmlAttribute(AttributeName = "OrganizationCode")]
        public string OrganizationCode;

        [XmlAttribute(AttributeName = "UnitOfMeasure")]
        public string UnitOfMeasure;

        [XmlElement("PrimaryInformation")]
        public PrimaryInformation PrimaryInformation;

        [XmlElement("ClassificationCodes")]
        public ClassificationCodes ClassificationCodes;

        [XmlElement("SafetyFactorDefinitions")]
        public SafetyFactorDefinitions SafetyFactorDefinitions;

        [XmlElement("ItemLocaleList")]
        public ItemLocaleList ItemLocaleList;

        [XmlElement("AditionalAttributeList")]
        public AdditionalAttributeList AditionalAttributeList;

    }
}
