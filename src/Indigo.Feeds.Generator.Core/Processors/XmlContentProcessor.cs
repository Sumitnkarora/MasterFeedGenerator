using Indigo.Feeds.Generator.Core.Models;
using Indigo.Feeds.Generator.Core.Processors.Contracts;
using Indigo.Feeds.Generator.Core.Utils;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Indigo.Feeds.Generator.Core.Processors
{
    public class XmlContentProcessor : BaseFileProcessor, IXmlFileContentProcessor
    {
        private static ConcurrentDictionary<Type, XmlSerializer> serializers = new ConcurrentDictionary<Type, XmlSerializer>();
        private const string FileExtension = "xml";
        private const string GzipFileExtension = ".gz";
        private readonly bool _isGzip;
        private readonly string _fileNameFormat;
        
        public XmlContentProcessor(bool isGzip, string fileNameFormat)
        {
            _isGzip = isGzip;
            _fileNameFormat = fileNameFormat;
        }

        public override OutputInstruction FileRead(FileInfo source)
        {
            throw new NotImplementedException();
        }
        
        public override FileInfo FileWrite(OutputInstruction instruction)
        {
            if (instruction == null || instruction.Data == null || !instruction.Data.Any())
            {
                return null;
            }

            var filePath = GeneratorHelper.GetFilePath(instruction.OutputName, instruction.OutputLocation, _fileNameFormat);
            XmlWriter xmlWriter = null;
            GZipStream gZipStream = null;

            if (_isGzip)
            {
                gZipStream = new GZipStream(FileCreate(filePath + ".gz"), CompressionMode.Compress);
                xmlWriter = FileWriterCreate(gZipStream);
            }
            else
            {
                xmlWriter = FileWriterCreate(filePath);
            }

            var serializer = GetSerializer(instruction.Data.First().GetType(), string.Empty);

            using (xmlWriter)
            {
                serializer.Serialize(xmlWriter, instruction.Data);
            }

            if (gZipStream != null)
            {
                gZipStream.Close();
            }

            return new FileInfo(filePath);
        }

        public virtual XmlWriter FileWriterCreate(string filePath)
        {
            Encoding utf8noBOM = new UTF8Encoding(false);
            return XmlWriter.Create(filePath, new XmlWriterSettings { Indent = true, Encoding = utf8noBOM });
        }

        public virtual XmlWriter FileWriterCreate(Stream stream)
        {
            Encoding utf8noBOM = new UTF8Encoding(false);
            return XmlWriter.Create(stream, new XmlWriterSettings { Indent = true, Encoding = utf8noBOM });
        }

        public override FileInfo[] GetContentFiles(string folderPath)
        {
            var workingFolder = new DirectoryInfo(Path.Combine(folderPath));
            return workingFolder.GetFiles(
                string.Format("*.{0}{1}", FileExtension, _isGzip ? GzipFileExtension : string.Empty), 
                SearchOption.AllDirectories);
        }

        public XmlSerializer GetSerializer(Type type, string defaultNamespace)
        {
            return serializers.GetOrAdd(type, t => { return new XmlSerializer(t, defaultNamespace); });
        }
    }
}
