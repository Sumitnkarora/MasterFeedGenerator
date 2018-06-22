using Indigo.Feeds.Generator.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.StoreInformation.Models
{
    [Serializable]
    public class Organization : ExportData
    {
        [XmlAttribute(AttributeName = "CapacityOrganizationCode")]
        public string CapacityOrganizationCode = "Indigo_CA";

        [XmlAttribute(AttributeName = "CatalogOrganizationCode")]
        public string CatalogOrganizationCode = "Indigo_CA";

        [XmlAttribute(AttributeName = "InheritConfigFromEnterprise")]
        public string InheritConfigFromEnterprise = "Y";

        [XmlAttribute(AttributeName = "InventoryOrganizationCode")]
        public string InventoryOrganizationCode = "Indigo_CA";

        [XmlAttribute(AttributeName = "InventoryPublished")]
        public string InventoryPublished = "Y";

        [XmlAttribute(AttributeName = "IsHubOrganization")]
        public string IsHubOrganization = "N";

        [XmlAttribute(AttributeName = "IsSourcingKept")]
        public string IsSourcingKept = "Y";

        [XmlAttribute(AttributeName = "LocaleCode")]
        public string LocaleCode;

        [XmlAttribute(AttributeName = "Operation")]
        public string Operation = "Manage";

        [XmlAttribute(AttributeName = "OrganizationCode")]
        public string OrganizationCode;

        [XmlAttribute(AttributeName = "OrganizationName")]
        public string OrganizationName;

        [XmlAttribute(AttributeName = "ParentOrganizationCode")]
        public string ParentOrganizationCode = "Indigo_CA";

        [XmlAttribute(AttributeName = "PrimaryEnterpriseKey")]
        public string PrimaryEnterpriseKey = "Indigo_CA";

        [XmlElement("CorporatePersonInfo")]
        public CorporatePersonInfo CorporatePersonInfo;

        [XmlElement("Node")]
        public Node Node;
    }
}
