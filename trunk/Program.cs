using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using System.Collections.Concurrent;

namespace TSpliter
{
    class Program
    {
        static int REPORT_INTERVAL = 1000 * 60;
        static int BUFFER_REPORT_INTERVAL = 1000;

        static string VERSION = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        static string UPDATE_DATE = "2012-09-21";

        static string FormatBufferSize(long fileSize)
        {
            if (fileSize < 0)
            {
                throw new ArgumentOutOfRangeException("fileSize");
            }
            else if (fileSize >= 1024 * 1024 * 1024)
            {
                return string.Format("{0:########0.00} GB", ((Double)fileSize) / (1024 * 1024 * 1024));
            }
            else if (fileSize >= 1024 * 1024)
            {
                return string.Format("{0:####0.00} MB", ((Double)fileSize) / (1024 * 1024));
            }
            else if (fileSize >= 1024)
            {
                return string.Format("{0:####0.00} KB", ((Double)fileSize) / 1024);
            }
            else
            {
                return string.Format("{0} bytes", fileSize);
            }
        }

        static long MaxBuffer =0;

        static void BufferReportThread()
        {
            while (true)
            {
                try
                {
                    long bfsize = spliter.buffer.Length;

                    if(bfsize >= Spliter.BUFFERSIZE)
                    {
                        Pusher.ErrorCode = -1;
                        Pusher.ErrorDesc = "buffer is full!data dropped";
                    }
                    else if (bfsize > Spliter.BUFFERSIZE / 2)
                    {
                        Pusher.ErrorCode = 1;
                        Pusher.ErrorDesc = "buffer is too high!";
                    }
                    else
                    {
                        Pusher.ErrorCode = 0;
                        Pusher.ErrorDesc = "";
                    }

                    string bufmsg = string.Format("buffer size: {0} / {1}",
                        FormatBufferSize(bfsize), FormatBufferSize(MaxBuffer));

                    Console.WriteLine(bufmsg);
                    Pusher.StatusDesc = bufmsg;

                    if (bfsize > MaxBuffer)
                        MaxBuffer = bfsize;
                    Thread.Sleep(BUFFER_REPORT_INTERVAL);
                }
                catch (Exception e)
                {
                    Console.WriteLine("buffer report thread error:" + e.StackTrace);
                }
            }
        }

        static void StatusReportThread()
        {
            while (true)
            {
                try
                {
                    long bfsize = spliter.buffer.Length;
                    LoggerDispatcher.Instance.Log(string.Format("buffer size: {0} / {1}",
                        FormatBufferSize(bfsize), FormatBufferSize(MaxBuffer)));

                    spliter.UpdateLoseRate();
                    Thread.Sleep(REPORT_INTERVAL);
                }
                catch (Exception e)
                {
                    LoggerDispatcher.Instance.Log("report thread error:"+e.StackTrace+"\n"+e.Message);
                }
            }
        }
        static Spliter spliter;

        static void ShowTitle()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("*******************************\n");
            sb.AppendFormat("TS demuxer UDP转发工具\n");
            sb.AppendFormat("videoworks copyright @2012\n版权所有，禁止拷贝使用\n");
            sb.AppendFormat("version:{0}\n", VERSION);
            sb.AppendFormat("update :{0}\n", UPDATE_DATE);
            sb.AppendFormat("*******************************\n");
            Console.Write(sb);
        }

        //push
        static Pusher pusher;

        static void Main(string[] args)
        {
            Console.Title = "videoworks TS demuxer v" + VERSION;
            if (args.Length != 2)
            {
                ShowTitle();
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("\n\n使用方式:\n");
                sb.AppendFormat("TSpliter [config路径] [log路径]\n");
                Console.Write(sb);
                return;
            }
            try
            {
                string configPath = args[0];
                string logPath = args[1];

                ConfigParser cfg = ConfigParser.Parse(configPath);
                if (cfg == null)
                {
                    Console.WriteLine("配置文件读取失败");
                    return;
                }

                if (logPath != string.Empty)
                {
                    Logger.LogPath = logPath;
                }

                //init pushserver
                if (cfg.PushServerIp != string.Empty)
                {
                    pusher = new Pusher(cfg.PushServerIp, cfg.PushServerPort, cfg.PushServerInterval);
                }

                //init streams
                spliter = new Spliter();
                foreach (var s in cfg.streamInfo)
                {
                    spliter.AddStream(new VideoStream(new IPEndPoint(IPAddress.Parse(s.ip), s.port), s.pids.ToArray()));
                }
                spliter.Start();

                //init udp reciving
                IPAddress address = IPAddress.Parse(cfg.RecieveIp);
                IPEndPoint socket = new IPEndPoint(address, cfg.RecievePort);
                
                UdpClient client = new UdpClient();
                client.ExclusiveAddressUse = false;
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                if (cfg.RecieveBind == string.Empty)
                    client.Client.Bind(new IPEndPoint(IPAddress.Any, cfg.RecievePort));
                else
                    client.Client.Bind(new IPEndPoint(IPAddress.Parse(cfg.RecieveBind), cfg.RecievePort));

                client.JoinMulticastGroup(address);
                client.Client.ReceiveBufferSize = 1024 * 1024 * 256;
                Console.WriteLine("udp recieve buffer size=" + client.Client.ReceiveBufferSize);

                Thread reportThread = new Thread(new ThreadStart(StatusReportThread));
                reportThread.Priority = ThreadPriority.Lowest;
                reportThread.Start();

                Thread bufferReportThread = new Thread(new ThreadStart(BufferReportThread));
                bufferReportThread.Priority = ThreadPriority.Lowest;
                bufferReportThread.Start();
                
                LoggerDispatcher.Instance.Log("started.");

                Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                Console.WriteLine("main thread level=" + Thread.CurrentThread.Priority.ToString());
                while (true)
                {
                    try
                    {
                        byte[] data = client.Receive(ref socket);
                        if (data != null && data.Length > 0)
                        {
                            spliter.AddData(data);
                        }
                    }
                    catch (Exception e)
                    {
                        LoggerDispatcher.Instance.Log(e.StackTrace);
                        LoggerDispatcher.Instance.Log(e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("命令行解析错误"+e.Message);
                return;
            }
        }
    }
}
