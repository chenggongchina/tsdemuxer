using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Videoworks.PushClient;
using System.Threading;

namespace TSpliter
{
    public class Pusher
    {
        const string PUSH_CHANNEL = "tvf_monitor_v0.0.0.3";

        PushClientManager manager;
        int Interval = 1000;
        public Pusher(string ip,int port,int pushInterval)
        {
            if (ip==null||ip == string.Empty)
                return;

            this.Interval = pushInterval;
            manager = new PushClientManager(ip, port);
            Thread pushThread = new Thread(new ThreadStart(PushThread));
            pushThread.Priority = ThreadPriority.Lowest;
            pushThread.Start();

        }

        public volatile static string StatusDesc = "";
        public volatile static int ErrorCode = 0;
        public volatile static string ErrorDesc = "";
        public volatile static string Memo = "";


        void PushThread()
        {
            while (true)
            {
                //statusdesc, errorcode, errordesc, memo
                BaseInfo info = new BaseInfo() { statusdesc = StatusDesc, errorcode = ErrorCode, errordesc = ErrorDesc, memo = Memo };
                manager.Write(PUSH_CHANNEL, info.ToString());
                Thread.Sleep(Interval);
            }
        }
    }
}
