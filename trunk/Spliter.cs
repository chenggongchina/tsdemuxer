using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;

namespace TSpliter
{

    class Spliter
    {
        public const int BUFFERSIZE = 1024 * 1024 * 256;
        public RingMemoryStream buffer = new RingMemoryStream(BUFFERSIZE);
        private List<VideoStream> streamList = new List<VideoStream>();

        private Dictionary<int, VideoStream> pidMap = new Dictionary<int, VideoStream>();

        private Dictionary<int, ContinuityCount> pidTsCount = new Dictionary<int, ContinuityCount>();

        //丢包率统计
        public void UpdateLoseRate()
        {

            ulong totalmin = 0;
            ulong totalmine = 0;

            ulong totaltenmin = 0;
            ulong totaltenmine = 0;

            ulong totalhour = 0;
            ulong totalhoure = 0;

            ulong totalday = 0;
            ulong totaldaye = 0;

            foreach (var pid in pidTsCount.Keys)
            {
                ContinuityCount c = pidTsCount[pid];
                c.SubmitCount();
                ulong smin = 0;
                ulong stenmin = 0;
                ulong shour = 0;
                ulong sday = 0;

                ulong mine = 0;
                ulong tenmine = 0;
                ulong houre = 0;
                ulong daye = 0;

                c.GetLoseRate(out mine, out smin, out tenmine, out stenmin, out houre, out shour, out daye, out sday);

                totalmin += smin;
                totalmine += mine;

                totaltenmin += stenmin;
                totaltenmine += tenmine;

                totalhour += shour;
                totalhoure += houre;

                totalday += sday;
                totaldaye += daye;
            }

            double min = 0, tenmin = 0, hour = 0, day = 0;
            if (totalmin > 0) min = (double)totalmine / ((double)totalmin + (double)totalmine);
            if (totaltenmin > 0) tenmin = (double)totaltenmine / ((double)totaltenmin + (double)totaltenmine);
            if (totalhour > 0) hour = (double)totalhoure / ((double)totalhour + (double)totalhoure);
            if (totalday > 0) day = (double)totaldaye / ((double)totalday + (double)totaldaye);

            string droprateMsg = string.Format("丢包率统计\n    1min:{0, 10:E1} ({4}/{5})\n   10min:{1, 10:E1} ({6}/{7})\n   1hour:{2, 10:E1} ({8}/{9})\n  24hour:{3, 10:E1} ({10}/{11})",
                    min,
                    tenmin,
                    hour,
                    day,
                    totalmine,
                    totalmin + totalmine,
                    totaltenmine,
                    totaltenmin + totaltenmine,
                    totalhoure,
                    totalhour + totalhoure,
                    totaldaye,
                    totalday + totaldaye);
            LoggerDispatcher.Instance.Log(droprateMsg);
            Pusher.Memo = droprateMsg.Replace("\n", "");

        }

        public Semaphore sema = new Semaphore(0,1);
        public void AddStream(VideoStream stream)
        {
            streamList.Add(stream);
            foreach (var pid in stream.pidList)
            {
                pidMap[pid] = stream;
                pidTsCount.Add(pid, new ContinuityCount());
            }
        }

        public void AddData(byte[] data)
        {
            try
            {
                buffer.Write(data, 0, data.Length);
            }
            catch (Exception e)
            {
                LoggerDispatcher.Instance.Log("ring buffer write error:" + e.ToString());
            }
            lock (lockObj)
            {
                if (waitFlag)
                {
                    sema.Release();
                    waitFlag = false;
                }
            }
        }

        public void Start()
        {
            Thread myThread = new Thread(new ThreadStart(Split));
            myThread.Priority = ThreadPriority.AboveNormal;
            myThread.Start();
        }

        private int GetPid(byte[] data)
        {
            return ((data[1] & 0x1F) << 8) | data[2];
        }

        private int GetContinuityCount(byte[] data)
        {
            return (data[3] & 0xF);
        }

        private int GetAdaptionFieldControl(byte[] data)
        {
            return (data[3] & 0x30) >> 4;
        }

        int TS_SYNC_BYTE = 0x47;
        private bool IsContinue(byte[] packet)
        {
            if (packet[0] != TS_SYNC_BYTE)
                return true;
            int pid = GetPid(packet);
            int count = GetContinuityCount(packet);
            int afc = GetAdaptionFieldControl(packet);
            if (pid == 0x1FFF) return true;
            if (!pidTsCount.ContainsKey(pid))
                return true;
            return pidTsCount[pid].ckeck_count(count,afc);
        }

        object lockObj = new object();
        bool waitFlag = false;

        List<int> allPids = new List<int>();

        private void Split()
        {
            byte[] data = new byte[188];
          
            while (true)
            {
                try
                {
                    int byteRead = buffer.Read(data, 0, 188);
                    if (byteRead == 0)
                    {
                        lock (lockObj)
                        {
                            waitFlag = true;
                        }
                        sema.WaitOne();
                        continue;
                    }

                   
                    int pid = GetPid(data);
                    //if (!allPids.Contains(pid))
                    //{
                    //    allPids.Add(pid);
                    //    Console.WriteLine(pid);
                    //}
                    
                    //如果缓冲区过满，则暂时关闭连续引用计数统计
                    if (buffer.Length < BUFFERSIZE / 2)
                    {
                        //检测连续引用计数
                        if (!IsContinue(data))
                        {
                            int newcount = pidTsCount[pid].count;
                            int oldcount = pidTsCount[pid].old_count;
                            LoggerDispatcher.Instance.Log(
                                string.Format("pid={0}引用计数不连续,old={1},new={2},delta={3}",
                                    pid,
                                    oldcount,
                                    newcount,
                                    (newcount + 16 - oldcount - 1) % 16
                                )
                            );
                        }
                    }

                    if (pid == 0)
                    {
                        foreach (var vs in streamList)
                        {
                            vs.Send(data);
                        }
                    }
                    else
                    {
                        if (!pidMap.ContainsKey(pid))
                            continue;
                        
                        pidMap[pid].Send(data);
                    }          
                }
                catch (Exception e)
                {
                    LoggerDispatcher.Instance.Log(e.StackTrace);
                    LoggerDispatcher.Instance.Log(e.Message);
                }
            }
        }
    }
}
