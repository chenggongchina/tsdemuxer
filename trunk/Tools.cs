using System;
using System.Net;

using System.Xml.Linq;
using System.Collections.Generic;

namespace TSpliter
{
    public class Tools
    {
        #region XML操作
        public static XElement LoadXml(string path)
        {
            return XElement.Load(path);
        }

        public static XElement GetXmlElement(XElement xml, string key)
        {
            return xml.Element(key);
        }

        public static IEnumerable<XElement> GetXmlElements(XElement xml, string key)
        {
            return xml.Elements(key);
        }

        public static string GetXmlAttribute(XElement xml, string attribute)
        {
            return xml.Attribute(attribute).Value;
        }

        public static float GetXmlAttributeFloat(XElement xml, string attribute)
        {
            return float.Parse(xml.Attribute(attribute).Value);
        }

        public static int GetXmlAttributeInt(XElement xml, string attribute)
        {
            return int.Parse(xml.Attribute(attribute).Value);
        }
        #endregion
    }
}
