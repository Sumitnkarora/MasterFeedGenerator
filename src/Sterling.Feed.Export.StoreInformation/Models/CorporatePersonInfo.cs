using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.StoreInformation.Models
{
    [Serializable]
    public class CorporatePersonInfo
    {
        [XmlAttribute(AttributeName = "AddressLine1")]
        public string AddressLine1;

        [XmlAttribute(AttributeName = "AddressLine2")]
        public string AddressLine2;

        [XmlAttribute(AttributeName = "City")]
        public string City;

        [XmlAttribute(AttributeName = "Company")]
        public string Company;

        [XmlAttribute(AttributeName = "Country")]
        public string Country = "CA";

        [XmlAttribute(AttributeName = "DayPhone")]
        public string DayPhone;

        [XmlAttribute(AttributeName = "EmailID")]
        public string EmailID;

        [XmlAttribute(AttributeName = "Latitude")]
        public decimal Latitude;

        [XmlAttribute(AttributeName = "Longitude")]
        public decimal Longitude;

        [XmlAttribute(AttributeName = "State")]
        public string State;

        [XmlAttribute(AttributeName = "ZipCode")]
        public string ZipCode;

    }
}
