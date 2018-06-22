using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.StoreInformation.Models
{
    [Serializable]
    public class Node
    {
        [XmlAttribute(AttributeName = "ActivateFlag")]
        public string ActivateFlag = "Y";

        [XmlAttribute(AttributeName = "AllowGiftWrap")]
        public string AllowGiftWrap = "Y";

        [XmlAttribute(AttributeName = "InventoryTracked")]
        public string InventoryTracked = "Y";

        [XmlAttribute(AttributeName = "IsFulfillmentNode")]
        public string IsFulfillmentNode = "Y";

        [XmlAttribute(AttributeName = "IsItemBasedAllocationAllowed")]
        public string IsItemBasedAllocationAllowed = "Y";

        [XmlAttribute(AttributeName = "Latitude")]
        public decimal Latitude;

        [XmlAttribute(AttributeName = "Longitude")]
        public decimal Longitude;

        [XmlAttribute(AttributeName = "NodeType")]
        public string NodeType;

        [XmlAttribute(AttributeName = "ShipNodeClass")]
        public string ShipNodeClass;

        [XmlAttribute(AttributeName = "ReceivingNode")]
        public string ReceivingNode = "Y";

        [XmlAttribute(AttributeName = "ReturnsNode")]
        public string ReturnsNode = "Y";

        [XmlAttribute(AttributeName = "ReturnCenterFlag")]
        public string  ReturnCenterFlag = "Y";

        [XmlAttribute(AttributeName = "ShipNode")]
        public string ShipNode;

        [XmlAttribute(AttributeName = "ShipnodeType")]
        public string ShipnodeType = "Store";

        [XmlAttribute(AttributeName = "TimeDiff")]
        public decimal TimeDiff;
    }
}
