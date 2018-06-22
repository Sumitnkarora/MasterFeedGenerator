using Indigo.Feeds.Generator.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Xml.Linq;
using Indigo.Feeds.Generator.Core.Enums;

namespace Indigo.Feeds.Generator.Core.Services
{
    public interface IDataService
    {
        Type GetDataType();

        IList<DataResult> GetDeletedData(DateTime fromTime, DateTime? toTime);

        DataResult GetData(StringDictionary attributeDictionary, IDataReader reader, string catalog, RunType runType);

        XElement ConvertToXml(ExportData data);

        ExportData MergeData(ExportData previousRecord, ExportData data);

        string GetXmlRoot(RunType runType);
    }
}
