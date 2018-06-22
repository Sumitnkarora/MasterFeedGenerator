using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Xml.Linq;
using Indigo.Feeds.Generator.Core.Enums;
using Indigo.Feeds.Generator.Core.Models;
using Indigo.Feeds.Generator.Core.Services;
using Sterling.Feed.Export.PosData.Models;
using Indigo.Feeds.Generator.Core.Extensions;

namespace Sterling.Feed.Export.PosData.Services
{
    public class PosDataService : IDataService
    {
        private const string DATE_FORMAT = "yyyy-MM-ddTH:mm:ss.fffK";
        public Type GetDataType()
        {
            return typeof(PosTransaction);
        }

        public IList<DataResult> GetDeletedData(DateTime fromTime, DateTime? toTime)
        {
            throw new NotImplementedException();
        }

        public DataResult GetData(StringDictionary attributeDictionary, IDataReader reader, string catalog, RunType runType)
        {
            var item = new Item
            {
                ItemId = reader["ItemID"].ToString(),
                Quantity = Int32.Parse(reader["Quantity"].ToString()),
                Reference_1 = reader["Reference_1"].ToString(),
                ShipNode = reader["ShipNode"].ToString(),
                TransactionDate = Convert.ToDateTime(reader["TransactionDate"]).ToUniversalTime().ToString(DATE_FORMAT)
            };

            List<Item> itemList = new List<Item>();
            itemList.Add(item);

            var posTransaction = new PosTransaction();
            posTransaction.ItemList = itemList;
            posTransaction.SourceId = reader["Reference_1"].ToString();

            return new DataResult
            {
                ExportData = posTransaction
            };
        }

        public XElement ConvertToXml(ExportData data)
        {
            var posTransaction = (PosTransaction)data;

            var itemListNode = new XElement("Items");

            foreach (Item item in posTransaction.ItemList)
            {
                var itemNode = new XElement("Item");
                itemNode.SetAttributeValue("ItemID", item.ItemId);
                itemNode.SetAttributeValue("Quantity", item.Quantity);
                itemNode.SetAttributeValue("Reference_1", item.Reference_1);
                itemNode.SetAttributeValue("ShipNode", item.ShipNode);
                itemNode.SetAttributeValue("TransactionDate", item.TransactionDate);

                itemListNode.Add(itemNode);
            }

            return itemListNode;
        }

        public ExportData MergeData(ExportData previousRecord, ExportData data)
        {
            var merged = (PosTransaction)previousRecord;
            var curr = (PosTransaction)data;

            foreach (Item item in curr.ItemList)
            {
                merged.ItemList.Add(item);
            }

            return merged;
        }

        public string GetXmlRoot(RunType runType)
        {
            return null;
        }
    }
}
