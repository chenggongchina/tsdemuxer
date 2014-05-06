using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace TSpliter
{

    //    json样例：
    //{
    //'sendtime':'2012-04-19 19:00:00', 
    //'fact':'cutv',
    //'type':’iprecord’, 
    //‘status’:’Idle’,
    //‘statusdesc’:’’,
    //‘memo’:’’,
    //‘channel’:’’,
    //‘starttime’:’’,
    //‘endtime’:’’,
    //‘progresstime’:’’
    //}


    //基础监控（程序存活监控）
    public class BaseInfo
    {
        public DateTime startuptime { get; set; }	//程序启动时间，如2012-4-27 19:00:00
        public DateTime sendtime { get; set; }		//消息发送时间，如2012-4-27 19:00:00
        public String host { get; set; }			//程序所在机器的主机名，如 COMPUTE-443	
        public String type { get; set; }			//应用程序名/监控模块名，如iprecord 
        public Int32 instanceid { get; set; }	//实例ID（若程序为多实例，必须填写）
        public String status { get; set; }		//枚举：Idle, Working
        public String statusdesc { get; set; }	//状态描述
        public Int32 errorcode { get; set; }	//数字：0：NoError；<0 ErrorCode；>0 WarningCode
        public String errordesc { get; set; }	//错误描述，用于指导运维人员处理，用于指导程序员定位BUG
        public String memo { get; set; }		//业务备注描述

        public BaseInfo()
        {
            startuptime = Process.GetCurrentProcess().StartTime;
            sendtime = DateTime.Now;
            host = System.Net.Dns.GetHostName();
            type = "tsdemux";
            instanceid = 0;
            status = "Working";
            
        }

        public override string ToString()
        {
            sendtime = DateTime.Now;
            return String.Format("{{'startuptime':'{0:yyyy-MM-dd HH:mm:ss}', 'sendtime':'{1:yyyy-MM-dd HH:mm:ss}','host':'{2}','type':'{3}','instanceid':'{4}','status':'{5}','statusdesc':'{6}','errorcode':'{7}','errordesc':'{8}','memo':'{9}'}}",
                startuptime, sendtime, host, type, instanceid, status, statusdesc, errorcode, errordesc, memo);
        }
    }
    //模块：Controlcenter、vpserver、tcpproxy、deployAdapter、TSpliter
    public class FactBaseInfo : BaseInfo
    {
        public String fact { get; set; }			//视频工厂名称，如cutv 

        public override string ToString()
        {
            return String.Format("{{{0}, 'fact':'{1}'}}", base.ToString().Trim(new char[] { '{', '}' }), fact); ;
        }
    }


    //流处理级别监控/
    //说明：与流式数据处理相关，需要实时监控的，每个应用程序可能发出多个StreamInfo包，每个代表一个频道。
    //模块： record、iprecord、fileserver
    public class StreamInfo : FactBaseInfo
    {
        public Int32 channel { get; set; }		//频道
        public DateTime starttime { get; set; }		//开始北京时间
        public DateTime endtime { get; set; }		//结束北京时间
        public DateTime progresstime { get; set; }	//当前处理到的北京时间

        public override string ToString()
        {
            return String.Format("{{{0}, 'channel':'{1}', 'starttime':'{2:yyyy-MM-dd HH:mm:ss}', 'endtime':'{3:yyyy-MM-dd HH:mm:ss}', 'progresstime':'{4:yyyy-MM-dd HH:mm:ss}'}}",
                base.ToString().Trim(new char[] { '{', '}' }), channel, starttime, endtime, progresstime); ;
        }
    }

    //视频网关监控
    //模块：videogatedownload、videogatetranscode
    public class VideoGateInfo : StreamInfo
    {
        public String videogate { get; set; }	//视频网关名
        public override string ToString()
        {
            return String.Format("{{{0}, 'videogate':'{1}'}}", base.ToString().Trim(new char[] { '{', '}' }), videogate); ;
        }
    }

    //预处理监控
    //模块：preprocesser(transcode + audio)
    public class PreprocessInfo : StreamInfo
    {
        public Int32 preprocessmask { get; set; }	//预处理掩码，对应掩码类型位数。
    }

    //document任务类型类监控
    //模块：synclient、cutworker、deployworker
    public class DocInfo : FactBaseInfo
    {
        public String Customer { get; set; }
        public Int32 DocumentID { get; set; }
        public String ChannelName { get; set; }
        public String ColumnName { get; set; }
        public String Title { get; set; }
        //  public String lastdocument{get;set;}		
        //当前/上一个处理的document，格式 [customer],[docid],[channel],[column] ,[title]
        //如： tencent 333 CCTVNEWS 新闻三十分 温家宝会见XXXX
        public override string ToString()
        {
            return String.Format("{{{0}, 'lastdocument':'[{1}],[{2}],[{3}],[{4}],[{1}]'}}",
                base.ToString().Trim(new char[] { '{', '}' }), Customer, DocumentID, ChannelName, ColumnName, Title);
        }
    }

    public class EditorInfo: VideoGateInfo 
    {
        public String  username{get;set;}//编辑人员，如“张三”
       public DateTime  cachetime{get;set;}//缓冲到的北京时间
       public EditOperation lastoperation { get; set; }//enum{title,keywords,desc,prevclip,nextclip,prevcut,nextcut, …… }，在此定义很多界面操作，把编辑上一个操作放入此处。每个button都可以有一个操作Enum。
       public override string ToString()
       {
           return String.Format("{{{0}, 'username':'{1}', 'cachetime':'{2:yyyy-MM-dd HH:mm:ss.ff}', 'lastoperation':'{3}'}",
               base.ToString().Trim(new char[] { '{', '}' }), username, cachetime, lastoperation); ;
       }

       public EditorInfo(String videogateName, String factname, String userName)
       {
           username = userName;
           startuptime = DateTime.Now;
           memo = username;
           instanceid = Process.GetCurrentProcess().Id;
           videogate = videogateName;
           fact = factname;
       }
       private static EditorInfo Instance { get; set; }
       public static EditorInfo SetNewInstance(String videogateName, String factname, String userName)
       {
           Instance = new EditorInfo(videogateName, factname, userName);
           return Instance;
       }
       public static EditorInfo GetInstance()
       {
           return Instance;
       }
}
    public class TriggerInfo:FactBaseInfo
    {
        public String  Videogate{get;set;}//视频网关名
          public override string ToString()
        {
            return String.Format("{{{0}, 'Videogate':'{1}'}}",
                base.ToString().Trim(new char[] { '{', '}' }), Videogate);
        }
}

    public enum EditOperation
    {
        None = 0,
        Title = 1,
        Keywords = 2,
        Desc = 3,
        PrevClip = 4,
        NextClip = 5,
        PrevCut = 6,
        NextCut = 7,
    }
}
