using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace TSpliter
{
    class ConfigStreamInfo
    {
        public string ip;
        public int port;
        public List<int> pids = new List<int>();
    }

    class ConfigParser
    {
        static public ConfigParser Parse(string path)
        {
            try
            {
                ConfigParser rst = new ConfigParser();
                XElement doc = Tools.LoadXml(path);
                XElement pushserverNode = Tools.GetXmlElement(doc, "pushserver");
                rst.PushServerIp = Tools.GetXmlAttribute(pushserverNode, "ip");
                if(rst.PushServerIp!= string.Empty)
                {
                    rst.PushServerPort = Tools.GetXmlAttributeInt(pushserverNode, "port");
                    rst.PushServerInterval = Tools.GetXmlAttributeInt(pushserverNode, "interval");
                }

                XElement multicastNode = Tools.GetXmlElement(doc, "source");
                rst.RecieveBind = Tools.GetXmlAttribute(multicastNode, "bind");
                rst.RecieveIp = Tools.GetXmlAttribute(multicastNode, "ip");
                rst.RecievePort = Tools.GetXmlAttributeInt(multicastNode, "port");

                foreach(var node in Tools.GetXmlElements(Tools.GetXmlElement(doc,"streams"),"stream"))
                {
                    ConfigStreamInfo s = new ConfigStreamInfo();
                    s.ip = Tools.GetXmlAttribute(node, "ip");
                    s.port = Tools.GetXmlAttributeInt(node, "port");
                    string pids = Tools.GetXmlAttribute(node, "pids");
                    foreach(var p in pids.Split(new char[] { ',' }))
                    {
                        s.pids.Add(int.Parse(p));
                    }
                    rst.streamInfo.Add(s);
                }

                return rst;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine(e.StackTrace);
                return null;
            }
        }


        public string PushServerIp;
        public int PushServerPort;
        public int PushServerInterval;

        public string RecieveBind;
        public string RecieveIp;
        public int RecievePort;

        public List<ConfigStreamInfo> streamInfo = new List<ConfigStreamInfo>();
    }
}
