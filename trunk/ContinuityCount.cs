using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

namespace TSpliter
{
    class ContinuityCount
    {
        public Queue<ulong> tscounts = new Queue<ulong>();
        public Queue<ulong> errors = new Queue<ulong>();

        public int old_count = -1;
        public int count = -1;
        public ulong error = 0;
        public ulong ts_counts = 0;

        //每分钟执行一次
        public void SubmitCount()
        {
            lock (this)
            {
                tscounts.Enqueue(ts_counts);
                if (tscounts.Count > 1440) //存放24小时数据
                    tscounts.Dequeue();
                errors.Enqueue(error);
                if (errors.Count > 1440)
                    errors.Dequeue();

                error = 0;
                ts_counts = 0;
            }
        }

        public void GetLoseRate(
            out ulong mine, out ulong min, 
            out ulong tenmine, out ulong tenmin, 
            out ulong houre, out ulong hour, 
            out ulong daye, out ulong day)
        {
            lock (this)
            {
                ulong summin = 0;
                ulong summine = 0;

                ulong sumtenmin = 0;
                ulong sumtenmine = 0;

                ulong sumhour = 0;
                ulong sumhoure = 0;

                ulong sumday = 0;
                ulong sumdaye = 0;
                
                
                for (int i = tscounts.Count - 1; i >0 ; --i)
                {
                    if (tscounts.Count - i - 1== 0)
                    {
                        summin += tscounts.ElementAt(i);
                        summine += errors.ElementAt(i);
                    }
                    if (tscounts.Count - i - 1 < 10)
                    {
                        sumtenmin += tscounts.ElementAt(i);
                        sumtenmine += errors.ElementAt(i);
                    }
                    if (tscounts.Count - i -1 < 60)
                    {
                        sumhour += tscounts.ElementAt(i);
                        sumhoure += errors.ElementAt(i);
                    }
                    if (tscounts.Count - i -1 < 1440)
                    {
                        sumday += tscounts.ElementAt(i);
                        sumdaye += errors.ElementAt(i);
                    }
                }

                mine = summine;
                min = summin;
                tenmine = sumtenmine;
                tenmin = sumtenmin;
                houre = sumhoure;
                hour = sumhour;
                daye = sumdaye;
                day = sumday;
            }
        }

        public bool ckeck_count(int c, int afc)
        {
            lock (this)
            {
                ts_counts++;
                if (count == -1)
                {
                    count = c;
                    return true;
                }
                int next = next_count();
                old_count = count;
                count = c;
                if (afc == 0 || afc == 2)//no adaptation_field, payload only
                {
                    if (old_count == c)
                        return true;
                    return false;
                }
                else if (afc == 1 || afc == 3)//no adaptation_field, payload only
                {
                    if (next != c)
                    {
                        error += (ulong)((c + 16 - old_count - 1) % 16);
                        //error++;
                        return false;
                    }
                    return true;
                }
                return true;
            }//lock
        }

        int next_count() { return (count + 1) % 16; }
    }
}
