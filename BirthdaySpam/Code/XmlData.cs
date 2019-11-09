// ---------------------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="XmlData.cs" company="Elcomplus LLC"></copyright>
// <date>2018-06-20</date>
// ---------------------------------------------------------------------------------------------------------------------------------------------------
namespace BirthdaySpam.Code
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml;

    //using NLog;

    public class XmlData
    {
        #region Constants

        public const string KEY = "key";
        public const string ID = "id";
        public const string VAL = "val";

        public const string FILE = "file";
        public const string COMMENT = "comment";
        public const string COMMENTS = "comments";
        public const string BANK = "bank";
        public const string DEBUG = "debug";
        public const string ACTIVE = "active";
        public const string NAME = "name";
        public const string BIRTHDAY = "birthday";
        public const string EMAIL = "email";
        public const string FEE = "fee";
        public const string EXPECTED = "expected";

        // Settings:
        public const string LOGIN = "login";
        public const string PASSWORD = "password";
        public const string SESSION = "session";
        public const string NETWORK_LOGIN = "network_login";
        public const string NETWORK_PASSWORD = "network_password";
        public const string NETWORK_DOMAIN = "network_domain";
        public const string SMTP_DOMAIN = "smtp_domain";
        public const string MAIL_FROM = "mail_from";
        public const string MAIL_SUBJECT = "mail_subject";
        public const string MAIL_MESSAGE = "mail_message";
        public const string HTTP_PORT = "http_port";
        public const string HTTPS_PORT = "https_port";

        #endregion Constants

        #region Fields

        //private readonly Logger _logger;
        private readonly XmlDocument _xml;

        #endregion Fields

        #region Properties

        public Dictionary<string, Dictionary<string, string>> Keys;

        public XmlElement DocumentElement => _xml?.DocumentElement;

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Creates empty XML without data.
        /// </summary>
        public XmlData()
        {
            //_logger = LogManager.GetCurrentClassLogger();
            _xml = new XmlDocument();
            _xml.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\"?><data></data>");
            Keys = new Dictionary<string, Dictionary<string, string>>();
        }

        /// <summary>
        /// Creates XML from node.
        /// </summary>
        /// <param name="node"></param>
        public XmlData(XmlNode node): this()
        {
            XmlNode clone = _xml.ImportNode(node, true);
            _xml.DocumentElement?.AppendChild(clone);
            SyncKeysWithXml();
        }

        #endregion Constructors

        #region Methods

        public XmlNode FirstOrDefault(string attributeName, string value)
        {
            if (_xml.DocumentElement == null)
                return null;

            foreach (XmlNode node in _xml.DocumentElement.ChildNodes)
            {
                if (node.Attributes == null)
                    continue;

                if (node.Attributes[attributeName].Value == value)
                    return node;
            }
            return null;
        }

        public bool DeleteFirst(string attributeName, string value)
        {
            if (_xml.DocumentElement == null)
                return false;

            foreach (XmlNode node in _xml.DocumentElement.ChildNodes)
            {
                if (node.Attributes == null)
                    continue;

                if (node.Attributes[attributeName].Value == value)
                {
                    _xml.DocumentElement.RemoveChild(node);
                    SyncKeysWithXml();
                    return true;
                }
            }
            return false;
        }

        public bool ReplaceFirst(string attributeName, string value, XmlData data)
        {
            if (_xml.DocumentElement == null)
                return false;

            foreach (XmlNode node in _xml.DocumentElement.ChildNodes)
            {
                if (node.Attributes == null)
                    continue;

                if (node.Attributes[attributeName].Value == value)
                {
                    XmlNode importNode = data.FirstOrDefault(attributeName, value);
                    if (importNode != null)
                    {
                        _xml.DocumentElement.ReplaceChild(_xml.ImportNode(importNode, true), node);
                        SyncKeysWithXml();
                        return true;
                    }
                }
            }
            return false;
        }

        public bool Add(XmlData data)
        {
            if (_xml.DocumentElement == null)
                return false;

            if (data?._xml.DocumentElement == null || data._xml.DocumentElement.ChildNodes.Count < 1)
                return false;

            // Find free id for new record:
            int freeId = 0;
            string[] ids = new string[_xml.DocumentElement.ChildNodes.Count];
            foreach (XmlNode node in _xml.DocumentElement.ChildNodes)
            {
                if (node.Attributes == null)
                    continue;

                ids[freeId] = node.Attributes[ID].Value;
                freeId++;
            }
            // Compute id:
            for (freeId = 0; freeId < ids.Length; freeId++)
            {
                if (!ids.Contains($"{freeId}"))
                    break;
            }

            // Add new node:
            XmlNode importNode = data._xml.DocumentElement.ChildNodes[0];
            if (importNode.Attributes == null)
                return false;
            importNode.Attributes[ID].Value = $"{freeId}";
            _xml.DocumentElement.AppendChild(_xml.ImportNode(importNode, true));
            SyncKeysWithXml();
            return true;
        }

        public void SyncKeysWithXml()
        {
            if (_xml.DocumentElement == null)
                return;

            Keys.Clear();
            foreach (XmlNode node in _xml.DocumentElement.ChildNodes)
            {
                if (node.Attributes == null)
                    continue;

                var nodeAttributes = new Dictionary<string, string>();
                foreach (XmlAttribute attribute in node.Attributes)
                {
                    nodeAttributes.Add(attribute.Name, attribute.Value);
                }
                Keys[node.Attributes[ID].Value] = nodeAttributes;
            }
        }

        public void SyncXmlWithKeys()
        {
            if (_xml.DocumentElement == null)
                return;

            _xml.DocumentElement.RemoveAll();
            foreach (Dictionary<string, string> attributes in Keys.Values)
            {
                XmlNode node = _xml.CreateElement(KEY);
                foreach (var attribute in attributes)
                {
                    XmlAttribute newAttribute = _xml.CreateAttribute(attribute.Key);
                    newAttribute.Value = attribute.Value;
                    node.Attributes.Append(newAttribute);
                }
                _xml.DocumentElement.AppendChild(node);
            }
        }

        public void Load(string fileName)
        {
            _xml.RemoveAll();
            _xml.Load(fileName);
            SyncKeysWithXml();
        }

        public void Load(Stream stream)
        {
            _xml.RemoveAll();
            _xml.Load(stream);
            SyncKeysWithXml();
        }

        public void Save(string fileName)
        {
            _xml.Save(fileName);
        }

        public void Save(Stream stream)
        {
            _xml.Save(stream);
        }

        #endregion Methods
    }
}
