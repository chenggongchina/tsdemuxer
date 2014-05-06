using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.IO;
using System.Collections.Concurrent;

namespace TSpliter
{
    public class LoggerDispatcher
    {
        static public LoggerDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (createLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LoggerDispatcher();
                        }
                    }
                }
                return _instance;
            }
        }
        static object createLock = new object();
        static LoggerDispatcher _instance;

        private LoggerDispatcher()
        {
            Thread logThread = new Thread(new ThreadStart(Start));
            logThread.Priority = ThreadPriority.Lowest;
            logThread.Start();
        }

        const int LOG_INTERVAL = 200;
        const int LOG_MSG_SIZE = 1000;

        private Semaphore sema = new Semaphore(0, LOG_MSG_SIZE);

        public void Start()
        {
            while (true)
            {
                sema.WaitOne();
                //lock (_queue)
                {
                    string msg;
                    if(_queue.TryDequeue(out msg))
                    {
                        Logger.Log(msg);
                    }
                }
            }
        }
        //Queue<string> _queue = new Queue<string>();

        ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();

        public void Log(string log)
        {
            //lock (_queue)
            {
                if (_queue.Count > LOG_MSG_SIZE)
                    return; //drop log
                _queue.Enqueue(log);
                sema.Release();
            }
        }
    }

    public class Logger
    {
        private static Object o = new Object();

        static public string LogPath = AppDomain.CurrentDomain.BaseDirectory;

        private static string getLogFileName()
        {
            if (!Directory.Exists(LogPath))
            {
                Directory.CreateDirectory(LogPath);
            }

            return LogPath + "/" + DateTime.Now.ToLongDateString() + ".log";
        }

        private static bool writeToFile(string filename, string line, bool create)
        {
            lock (o)
            {
                try
                {
                    FileStream fs = new FileStream(filename, create ? FileMode.Create : FileMode.OpenOrCreate, FileAccess.ReadWrite);

                    char[] charData = new char[1024 * 8];
                    byte[] byteData = new byte[1024 * 16];

                    charData = line.ToCharArray();
                    Encoder ec = Encoding.UTF8.GetEncoder();
                    int l = ec.GetBytes(charData, 0, charData.Length, byteData, 0, true);

                    fs.Seek(0, SeekOrigin.End);
                    fs.Write(byteData, 0, l);

                    fs.Flush();
                    fs.Close();
                }
                catch (System.Exception ex)
                {
                    Debug.Print(ex.ToString());
                    return false;
                }
            }

            return true;
        }

        public static void Log(string message, params object[] args)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(DateTime.Now.ToLongTimeString());
            sb.AppendFormat(" {0} -> ", Thread.CurrentThread.ManagedThreadId);

            if (args.Length == 0)
                sb.Append(message);
            else
                sb.AppendFormat(message, args);

            Console.WriteLine(sb.ToString());
            Debug.Print(sb.ToString());

            sb.AppendLine();

            int n = 3;
            while (n-- > 0)
            {
                if (writeToFile(getLogFileName(), sb.ToString(), false))
                    break;
                Thread.Sleep(100);
            }
        }
    }
}
