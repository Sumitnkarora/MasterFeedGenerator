using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace FeedGenerators.Core
{
    using XE = XElement;
    using XA = XAttribute;
    using XN = XNamespace;
    using XD = XDocument;

    public class Category
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public Category(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }

    public class CategoriesFeed
    {
        private readonly string _taxonomyVersionName;
        private const string DefaultNodeName = "entry";
        private readonly XN _gNsAtom;
        private readonly XN _gNsG;
        private readonly XN _gNsC;
        private readonly XN _gNs;
        private readonly XN _indigoLink;

        private XD _catFeedXml;
        private XE _feedElement;
        private readonly Dictionary<string, Category> _synonymsDict = new Dictionary<string, Category>();
        private string _categoriesFilePath;
        private Dictionary<string, string> _categoryBreadcrumbs = new Dictionary<string, string>();

        public CategoriesFeed(string dimensionXmlFilePath, XN googleNsAtom, XN googleNsG, XN googleNsC, XN googleNs, XN indigoLink, string taxanomyVersionName)
        {
            _gNsAtom = googleNsAtom;
            _gNsG = googleNsG;
            _gNsC = googleNsC;
            _gNs = googleNs;
            _taxonomyVersionName = taxanomyVersionName;
            _indigoLink = indigoLink;

            AddRootNode();

            var xd = GetDimensionsXml(dimensionXmlFilePath);
            var rootNode = xd.XPathSelectElements("DIMENSIONS/DIMENSION[@NAME='Categories']/DIMENSION_NODE").First();
            AddNodeToFeed(string.Empty, rootNode);
            _catFeedXml.Add(_feedElement);
        }

        public string CategoriesFileName
        {
            get
            {
                if (File.Exists(_categoriesFilePath))
                {
                    return _categoriesFilePath;
                }
                throw new FileNotFoundException("Categories file not found!", _categoriesFilePath);
            }
        }

        public void Save(string destination)
        {
            _catFeedXml.Save(destination);
            _categoriesFilePath = destination;
        }

        public Category GetCategoryBySynonym(string synonym)
        {
            if ((!_synonymsDict.Keys.Contains(synonym)) || (string.IsNullOrEmpty(synonym)))
                return _synonymsDict["-"];  //It's a temporary hack! Nothing is as constant as temporary.
            return _synonymsDict[synonym];
        }

        public string GetBreadcrumbString(string categoryId)
        {
            return _categoryBreadcrumbs.ContainsKey(categoryId) ? _categoryBreadcrumbs[categoryId] : string.Empty;
        }

        private void AddRootNode()
        {
            _catFeedXml = new XD(new XDeclaration("1.0", "utf-8", null));
            _feedElement = new XE(_gNsAtom + "feed", new object[]
                    {
                     new XA(XN.Xmlns + "g", _gNsG),
                     new XA(XN.Xmlns + "c",_gNsC),
                     new XA(XN.Xmlns + "gd",_gNs)
                    });
            var titleElement = new XE(_gNsAtom + "title", "Category Feed XML");
            var nameElement = new XE(_gNsAtom + "name", "TEST Feed");
            var authorElement = new XE(_gNsAtom + "author", nameElement);
            var idElement = new XE(_gNsAtom + "id",
                string.Format("tag:indigo.ca,{0}:/support/products", DateTime.Now.ToString(DateTime.Now.ToString("yy-MM-dd"))));
            _feedElement.Add(new object[] { titleElement, authorElement, idElement });
        }

        private static XDocument GetDimensionsXml(string dimensionXmlFilePath)
        {
            XD xd;
            using (var xmlReader = XmlReader.Create(new StreamReader(dimensionXmlFilePath, Encoding.UTF8))) // Encoding.GetEncoding("utf-8"))))
            {
                xd = XD.Load(xmlReader);
            }
            return xd;
        }

        private void AddNodeToFeed(string parentId, XContainer node)
        {
            //Get node values 
            var idAttribute = node.XPathSelectElement("./DVAL/DVAL_ID").Attribute("ID");
            if (idAttribute == null) return;
            var id = idAttribute.Value;
            var displayName = node.XPathSelectElement("./DVAL/SYN[@DISPLAY='TRUE']").Value;

            foreach (var synonym in node.XPathSelectElements("./DVAL/SYN[@DISPLAY='FALSE'][@CLASSIFY='TRUE']"))
            {
                if (synonym.Value == id) continue;
                if (_synonymsDict.Keys.Contains(synonym.Value))
                {
                    _synonymsDict[synonym.Value] = new Category(displayName, id);
                }
                else
                {
                    _synonymsDict.Add(synonym.Value, new Category(displayName, id));
                }
            }

            //Create node
            var current = new XE(_gNsAtom + DefaultNodeName);
            var idElement = new XE(_gNsAtom + "id", id);
            var titleElement = new XE(_gNsAtom + "title", displayName);
            //link?? - is it mandatory?
            var linkElement = new XE(_gNsAtom + "link");
            var linkAttr = new XA("href", _indigoLink);
            linkElement.Add(linkAttr);
            var parentsElement = new XE(_gNsG + "parents", parentId);
            var groupElement = new XE(_gNsG + "group", _taxonomyVersionName);
            current.Add(new object[] { idElement, titleElement, linkElement, parentsElement, groupElement });

            //Add node to the feed
            _feedElement.Add(current);

            // Add the node info to the breadcrumbs list
            if (_categoryBreadcrumbs.ContainsKey(parentId))
                _categoryBreadcrumbs.Add(id, _categoryBreadcrumbs[parentId] + " > " + displayName);
            else
                _categoryBreadcrumbs.Add(id, "{TOP_LEVEL}");

            //Add childrens as well
            foreach (var child in node.Elements("DIMENSION_NODE"))
            {
                AddNodeToFeed(id, child);
            }
        }
    }
}
