
using System;
using System.IO;

namespace TSpliter
{
    /// <summary>
    /// 用字节数组实现的一个先进先出的环形内存流。支持分别在不同的线程中对流进行读写。
    /// </summary>
    public sealed class RingMemoryStream : Stream
    {
        private const int INITCAPACITY = 4 * 1024; // 默认初始容量
        private const int INCREMENTSIZE = 4 * 1024;// 自动扩展时一次最少扩展多少字节

        private object m_synObject = new object(); // 同步对象

        private byte[] m_buffer;    // 内部缓冲区
        private int m_capacity;     // 缓冲区的容量
        private int m_length;       // 缓冲区中当前有效数据的长度
        private bool m_expandable;  // 是否可自动扩展容量
        private int m_maxCapacity;  // 可扩展到的最大容量

        private int m_rPos;         // 读指针的偏移位置（指向流中第一个有效字节）
        private int m_wPos;         // 写指针的偏移位置（指向流中最后一个有效字节之后的那个字节）

        /// <summary>
        /// 使用默认的初始容量以及可自动扩展初始化 RingMemoryStream 类的新实例。
        /// </summary>
        public RingMemoryStream()
            : this(INITCAPACITY)
        {
        }

        /// <summary>
        /// 使用指定的初始容量以及可自动扩展初始化 RingMemoryStream 类的新实例。
        /// </summary>
        public RingMemoryStream(int capacity)
            : this(capacity, true)
        {
        }

        /// <summary>
        /// 使用指定的容量和是否可自动扩展初始化 RingMemoryStream 类的新实例。
        /// </summary>
        /// <param name="capacity">指定流的初始容量。</param>
        /// <param name="expandable">是否可自动扩展。</param>
        public RingMemoryStream(int capacity, bool expandable)
            : this(capacity, expandable, -1)
        {
        }

        /// <summary>
        /// 使用指定的容量和是否可扩展以及最大可扩展到的字节数初始化 RingMemoryStream 类的新实例。
        /// </summary>
        /// <param name="capacity">指定流的初始容量。</param>
        /// <param name="expandable">是否可自动扩展。</param>
        /// <param name="maxCapacity">最大可扩展到的字节数。指定 -1 表示不限制最大可扩展到的容量。如果 expandable 为 false，则此参数无意义。</param>
        /// <remarks>
        /// RingMemoryStream 在需要扩展流时首先创建一个新的较大的缓冲区，然后把旧缓冲区中的所有有效数据复制到新创建的缓冲
        /// 区中。如果需要频繁地扩展缓冲区并且要复制的数据量较大时这种方式的性能不够好。要避免频繁地扩展缓冲区可在构造函
        /// 数中指定足够大的初始容量。
        /// </remarks>
        public RingMemoryStream(int capacity, bool expandable, int maxCapacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException("capacity");
            }
            if (expandable && (maxCapacity != -1 && maxCapacity < capacity))
            {
                throw new ArgumentOutOfRangeException("maxCapacity");
            }

            m_length = 0;
            m_capacity = capacity;
            m_expandable = expandable;
            m_maxCapacity = maxCapacity;

            m_buffer = new byte[m_capacity];
            m_rPos = 0;
            m_wPos = 0;
        }

        /// <summary>
        /// 已重写。获取当前流是否支持读取。
        /// </summary>
        public override bool CanRead
        {
            get { return true; }
        }

        /// <summary>
        /// 已重写。获取当前流是否支持查找。
        /// </summary>
        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>
        /// 已重写。获取当前流是否支持写入。
        /// </summary>
        public override bool CanWrite
        {
            get { return true; }
        }

        /// <summary>
        /// 已重写。获取流的长度（字节数）。
        /// </summary>
        public override long Length
        {
            get
            {
                lock (m_synObject)
                {
                    return m_length;
                }
            }
        }

        /// <summary>
        /// 已重写。RingMemoryStream 流不支持 Position 属性，在设置或获取 Position 属性的值时将引发 NotSupportedException 异常。
        /// </summary>
        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// 已重写。调用此方法时将引发 NotSupportedException 异常。
        /// </summary>
        public override void Flush()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 已重写。调用此方法时将引发 NotSupportedException 异常。
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 已重写。调用此方法时将引发 NotSupportedException 异常。
        /// </summary>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 已重写。从当前流中读取字节块并将数据写入 buffer 中。已读取的数据将被流抛弃。
        /// </summary>
        /// <param name="buffer">包含所读取到的字节。</param>
        /// <param name="offset">buffer 中的字节偏移量。</param>
        /// <param name="count">最多读取的字节数。</param>
        /// <returns>成功读取到的总字节数。如果没有读取到任何字节则为 0。</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if (offset < 0 || count < 0)
            {
                throw new ArgumentOutOfRangeException("offset 或 count 不能为负数。");
            }
            if ((buffer.Length - offset) < count)
            {
                throw new ArgumentException("buffer 的长度减去 offset 的结果小于 count。");
            }

            lock (m_synObject)
            {
                // 真正要读取的字节数
                int readLen = Math.Min(m_length, count);
                if (readLen == 0)
                {
                    return 0;
                }

                ReadInternal(buffer, offset, readLen);

                return readLen;
            }
        }

        /// <summary>
        /// 已重写。将字节块写入当前流。
        /// </summary>
        /// <param name="buffer">要写入当前流的字节块。</param>
        /// <param name="offset">buffer 中的偏移量，从此处开始写入。</param>
        /// <param name="count">最多写入的字节数。</param>
        /// <remarks>
        /// 如果流能够被写入的字节数小于要写入的字节数，并且流不支持自动扩展或者已扩展到最大允许扩展到的大小
        /// 则会引发 NotSupportedException 异常。
        /// </remarks>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if (offset < 0 || count < 0)
            {
                throw new ArgumentOutOfRangeException("offset 或 count 不能为负数。");
            }
            if ((buffer.Length - offset) < count)
            {
                throw new ArgumentException("buffer 的长度减去 offset 的结果小于 count。");
            }

            lock (m_synObject)
            {
                // 要往流中写入 buffer 中的数据，流的容量至少要是这么多
                int minCapacityNeeded = m_length + count;

                // 如果需要扩展流则扩展流
                ExpandStream(minCapacityNeeded);

                // 如果无法再容纳下指定的字节数
                if (minCapacityNeeded > m_capacity)
                {
                    throw new NotSupportedException("无法再往流中写入 " + count + " 个字节。");
                }

                this.WriteInternal(buffer, offset, count);
            }
        }

        private void WriteInternal(byte[] buffer, int offset, int count)
        {
            if (m_rPos > m_wPos)
            {
                Buffer.BlockCopy(buffer, offset, m_buffer, m_wPos, count);
            }
            else
            {
                int afterWritePosLen = m_capacity - m_wPos;

                // 如果 m_rPos 之后的字节数够用
                if (afterWritePosLen >= count)
                {
                    Buffer.BlockCopy(buffer, offset, m_buffer, m_wPos, count);
                }
                else
                {
                    Buffer.BlockCopy(buffer, offset, m_buffer, m_wPos, afterWritePosLen);
                    int restLen = count - afterWritePosLen;
                    Buffer.BlockCopy(buffer, afterWritePosLen, m_buffer, 0, restLen);
                }
            }
            m_wPos += count;
            m_wPos %= m_capacity;
            m_length += count;
        }

        private void ReadInternal(byte[] buffer, int offset, int count)
        {
            if (m_rPos < m_wPos)
            {
                Buffer.BlockCopy(m_buffer, m_rPos, buffer, offset, count);
            }
            else
            {
                int afterReadPosLen = m_capacity - m_rPos;

                // 如果 m_rPos 之后的字节数够用
                if (afterReadPosLen >= count)
                {
                    Buffer.BlockCopy(m_buffer, m_rPos, buffer, offset, count);
                }
                else
                {
                    Buffer.BlockCopy(m_buffer, m_rPos, buffer, offset, afterReadPosLen);
                    int restLen = count - afterReadPosLen;
                    Buffer.BlockCopy(m_buffer, 0, buffer, afterReadPosLen, restLen);
                }
            }

            m_rPos += count;
            m_rPos %= m_capacity;
            m_length -= count;
        }

        // 扩展流
        // 此方法在扩展流时首先创建一个新的较大的缓冲区，然后把旧缓冲区中的所有有效数据复制到新创建的缓冲
        // 区中。如果频繁地扩展缓冲区或者要复制的数据量较大时性能可能比较低。
        private void ExpandStream(int minSize)
        {
            // 不支持扩展
            if (!m_expandable)
            {
                return;
            }

            // 不需要扩展
            if (m_capacity >= minSize)
            {
                return;
            }

            // 如果无法再扩展
            if (m_maxCapacity != -1 && (m_maxCapacity - m_capacity) < INCREMENTSIZE)
            {
                return;
            }

            // 计算要扩展几块（INCREMENTSIZE 的倍数）
            int blocksNum = (int)Math.Ceiling((double)(minSize - m_capacity) / INCREMENTSIZE);

            // 创建新的缓冲区，并把旧缓冲区中的数据复制到新缓冲区中
            byte[] buffNew = new byte[m_capacity + blocksNum * INCREMENTSIZE];
            int strLen = m_length;
            ReadInternal(buffNew, 0, m_length);
            m_buffer = buffNew;
            m_rPos = 0;
            m_wPos = strLen;
            m_capacity = buffNew.Length;
            m_length = strLen;
        }
    }
}