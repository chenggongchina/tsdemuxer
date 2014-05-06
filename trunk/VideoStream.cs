using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace TSpliter
{
    class VideoStream
    {
        private UdpClient client;
        public int[] pidList;
        private IPEndPoint socket;
        private int index = 0;

        public VideoStream(IPEndPoint socket, int[] pidList)
        {
            this.socket = socket;
            this.pidList = pidList;
            client = new UdpClient();
            client.Connect(socket);
            client.EnableBroadcast = true;
        }

        byte[] sendBuffer = new byte[188 * 7];

        public void Send(byte[] data)
        {
            try
            {
                Buffer.BlockCopy(data, 0, sendBuffer, index * 188, 188);
                index++;
                if (index == 7)
                {
                    //client.Send(sendBuffer, 188 * 7, socket);
                    client.Send(sendBuffer, 188 * 7);
                    index = 0;
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
