using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Indigo.Feeds.Generator.Core.Processors.Contracts
{
    public interface IXmlFileContentProcessor : IFileContentProcessor
    {
        XmlWriter FileWriterCreate(string filePath);

        XmlWriter FileWriterCreate(Stream stream);

        XmlSerializer GetSerializer(Type t, string defaultNamespace);
    }
}
