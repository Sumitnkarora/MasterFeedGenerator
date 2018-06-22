using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Xml.Linq;
using System.Linq;
using Indigo.Feeds.Generator.Core.Enums;
using Indigo.Feeds.Generator.Core.Models;
using Indigo.Feeds.Generator.Core.Services;
using Sterling.Feed.Export.CustomerData.Models;
using Indigo.Feeds.Generator.Core.Extensions;

namespace Sterling.Feed.Export.CustomerData.Services
{
    public class CustomerDataService : IDataService
    {
        private const string DATE_FORMAT = "yyyy-MM-ddTH:mm:ss.fffK";

        public XElement ConvertToXml(ExportData data)
        {
            var customer = (Customer)data;
            var customerNode = new XElement("Customer", new XAttribute("CustomerID", customer.CustomerID));
            customerNode.SetAttributeValue("ExternalCustomerID", customer.MembershipID);
            customerNode.SetAttributeValue("CustomerRewardsNo", customer.LoyaltyNumber);
            customerNode.SetAttributeValue("CustomerType", customer.CustomerType);
            customerNode.SetAttributeValue("Operation", customer.Operation);
            customerNode.SetAttributeValue("OrganizationCode", customer.OrganizationCode);
            customerNode.SetAttributeValue("RegisteredDate", customer.CreatedOn);
            customerNode.SetAttributeValue("Status", customer.Status);

            if (customer.Consumer != null)
            {
                var consumerNode = new XElement("Consumer");
                var billingNode = CreateAddressNode("BillingPersonInfo", customer.Consumer.BillingPersonInfo);
                consumerNode.Add(billingNode);
                customerNode.Add(consumerNode);
            }
            var contactListNode = new XElement("CustomerContactList");
            var contactNode = CreateAddressNode("CustomerContact", customer.CustomerContactList.CustomerContact,
                                               new Dictionary<string, string> {
                                                   { "CustomerContactID", customer.CustomerContactList.CustomerContact.CustomerContactID }
                                               }, true);

            contactListNode.Add(contactNode);
            customerNode.Add(contactListNode);

            var additionalAddressListNode = new XElement("CustomerAdditionalAddressList");
            additionalAddressListNode.SetAttributeValue("Reset", customer.CustomerContactList.CustomerContact.CustomerAdditionalAddressList.Reset);
            if (customer.CustomerContactList.CustomerContact.CustomerAdditionalAddressList.Addresses != null)
            {
                foreach (var address in customer.CustomerContactList.CustomerContact.CustomerAdditionalAddressList.Addresses)
                {
                    var additionalAddressNode = new XElement("CustomerAdditionalAddress");
                    additionalAddressListNode.Add(additionalAddressNode);
                    additionalAddressNode.SetAttributeValue("CustomerAdditionalAddressID", address.AddressID);
                    var personInfoNode = CreateAddressNode("PersonInfo", address.PersonInfo);
                    additionalAddressNode.Add(personInfoNode);
                }
            }
            contactNode.Add(additionalAddressListNode);

            var currencyListNode = new XElement("CustomerCurrencyList");
            currencyListNode.SetAttributeValue("Reset", customer.CustomerCurrencyList.Reset);
            var currencyNode = new XElement("CustomerCurrency");
            currencyNode.SetAttributeValue("Currency", customer.CustomerCurrencyList.CustomerCurrency.Currency);
            currencyNode.SetAttributeValue("IsDefaultCurrency", customer.CustomerCurrencyList.CustomerCurrency.IsDefaultCurrency);
            currencyListNode.Add(currencyNode);
            customerNode.Add(currencyListNode);

            return customerNode;
        }

        private XElement CreateAddressNode(string name, AddressInfo address, Dictionary<string, string> extraAttributes = null, bool isCustomerContact = false)
        {
            var node = new XElement(name);
            if (extraAttributes != null)
            {
                extraAttributes.Keys.ToList().ForEach(attribute =>
                {
                    node.SetAttributeValue(attribute, extraAttributes[attribute]);
                });
            }

            node.SetAttributeValue("AddressID", address.AddressIdOrNickname);
            node.SetAttributeValue("AddressLine1", address.AddressLine1);
            node.SetAttributeValue("AddressLine2", address.AddressLine2);
            node.SetAttributeValue("City", address.City);
            node.SetAttributeValue("Country", address.Country);
            node.SetAttributeValue("DayPhone", address.DayPhone);
            if (isCustomerContact)
            {
                node.SetAttributeValue("EmailID", address.EmailAddress);
            }
            else
            {
                node.SetAttributeValue("EMailID", address.EmailAddress);
            }
            node.SetAttributeValue("FirstName", address.FirstName);
            node.SetAttributeValue("LastName", address.LastName);
            node.SetAttributeValue("MiddleName", address.MiddleInitial);
            node.SetAttributeValue("MobilePhone", address.SMSPhoneNum);
            node.SetAttributeValue("OtherPhone", address.EveningPhone);
            node.SetAttributeValue("PreferredShipAddress", address.PreferredShipAddress);
            node.SetAttributeValue("State", address.ProvinceCode);
            node.SetAttributeValue("ZipCode", address.PostalZip);
            node.SetAttributeValue("Title", address.Title);
            return node;
        }

        public DataResult GetData(StringDictionary attributeDictionary, IDataReader reader, string catalog, RunType runType)
        {
            var customer = new Customer
            {
                SourceId = reader["CustomerID"].ToString(),
                CustomerID = reader["CustomerID"].ToString(),
                LoyaltyNumber = reader["LoyaltyNumber"] == DBNull.Value ? null : reader["LoyaltyNumber"].ToString(),
                MembershipID = reader["MembershipID"] == DBNull.Value ? null : reader["MembershipID"].ToString(),
                CreatedOn = Convert.ToDateTime(reader["CreatedOn"]).ToUniversalTime().ToString(DATE_FORMAT)
            };

            var addressID = reader["AddressID"] == DBNull.Value ? null : reader["AddressID"].ToString();
            var addrNickName = (reader["AddressNickname"] as string);

            var address = new AddressInfo
            {
                AddressID = addressID,
                AddressNickname = addrNickName,
                AddressIdOrNickname = (string.IsNullOrEmpty(addrNickName) ? addressID : addrNickName).LimitLength(256),
                AddressLine1 = (reader["AddressLine1"] as string).LimitLength(70),
                AddressLine2 = (reader["AddressLine2"] as string).LimitLength(70),
                AddressType = reader["AddressType"] == DBNull.Value ? null : (string)reader["AddressType"],
                City = (reader["City"] as string).LimitLength(35),
                Country = (reader["Country"] as string).LimitLength(40),
                DayPhone = (reader["DayPhone"] as string).LimitLength(40),
                EmailAddress = ((reader["EmailAddress"] as string) ?? string.Empty).LimitLength(150),
                FirstName = ((reader["FirstName"] as string) ?? string.Empty).LimitLength(64),
                LastName = ((reader["LastName"] as string) ?? string.Empty).LimitLength(64),
                MiddleInitial = ((reader["MiddleInitial"] as string) ?? string.Empty).LimitLength(40),
                SMSPhoneNum = ((reader["SMSPhoneNum"] as string) ?? string.Empty).LimitLength(40),
                EveningPhone = ((reader["EveningPhone"] as string) ?? string.Empty).LimitLength(40),
                PreferredShipAddress = (reader["DefaultAddress"] == DBNull.Value ? false : (bool)reader["DefaultAddress"]) ? "Y" : "N",
                ProvinceCode = (reader["ProvinceCode"] as string).LimitLength(35),
                PostalZip = (reader["PostalZip"] as string).LimitLength(35),
                Title = ((reader["Title"] as string) ?? string.Empty).LimitLength(10)
            };

            customer.AddressInfo = address;

            if (address.AddressType?.ToUpper() == "BILLING")
            {
                customer.Consumer = new Consumer
                {
                    BillingPersonInfo = address
                };

                customer.CustomerContactList.CustomerContact = new CustomerContact
                {
                    CustomerContactID = customer.CustomerID,
                    EmailAddress = address.EmailAddress,
                    FirstName = address.FirstName,
                    LastName = address.LastName,
                    MiddleInitial = address.MiddleInitial,
                    SMSPhoneNum = address.SMSPhoneNum,
                    PreferredShipAddress = address.PreferredShipAddress,
                    Title = address.Title,
                    AddressType = address.AddressType
                };
            }
            else
            {
                customer.CustomerContactList.CustomerContact = new CustomerContact(address, customer.CustomerID);
            }

            return new DataResult
            {
                ExportData = customer
            };
        }

        public Type GetDataType()
        {
            return typeof(Customer);
        }

        public IList<DataResult> GetDeletedData(DateTime fromTime, DateTime? toTime)
        {
            throw new NotImplementedException();
        }

        public string GetXmlRoot(RunType runType)
        {
            if (runType == RunType.Full || runType == RunType.OnDemand)
            {
                return "CustomerList";
            }
            return null;
        }

        public ExportData MergeData(ExportData previousRecord, ExportData data)
        {
            var prev = (Customer)previousRecord;
            var curr = (Customer)data;
            var merged = prev;

            if (curr.Consumer != null)
            {
                // current record is a billing address
                if (prev.Consumer == null)
                {
                    merged.Consumer = curr.Consumer;
                }
                return merged;
            }

            if (curr.Consumer == null && prev.Consumer != null && prev.AddressInfo.AddressType.ToUpper() == "BILLING")
            {
                // previous record was a billing address and is also the 1st record for the group. Use the current
                // address as the primary contact.
                merged.CustomerContactList = curr.CustomerContactList;
                return merged;
            }

            // Current record is an additional address.
            var additionalAddr = prev.CustomerContactList.CustomerContact.CustomerAdditionalAddressList.Addresses ?? new List<CustomerAdditionalAddress>();
            additionalAddr.Add(new CustomerAdditionalAddress
            {
                AddressID = curr.AddressInfo.AddressID,
                PersonInfo = new AddressInfo
                {
                    AddressIdOrNickname = curr.AddressInfo.AddressIdOrNickname,
                    AddressLine1 = curr.AddressInfo.AddressLine1,
                    AddressLine2 = curr.AddressInfo.AddressLine2,
                    City = curr.AddressInfo.City,
                    Country = curr.AddressInfo.Country,
                    DayPhone = curr.AddressInfo.DayPhone,
                    FirstName = curr.AddressInfo.FirstName,
                    LastName = curr.AddressInfo.LastName,
                    EveningPhone = curr.AddressInfo.EveningPhone,
                    PreferredShipAddress = curr.AddressInfo.PreferredShipAddress,
                    ProvinceCode = curr.AddressInfo.ProvinceCode,
                    PostalZip = curr.AddressInfo.PostalZip
                }
            });

            merged.CustomerContactList.CustomerContact.CustomerAdditionalAddressList.Addresses = additionalAddr;
            return merged;
        }
    }
}
