using Junhaehok;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static Junhaehok.HhhHelper;

namespace JunhyehokWebServer
{
    public static class SocketExtensions
    {
        public static bool SendBytes(this Socket so, Packet packet)
        {
            byte[] bytes = PacketToBytes(packet);
            int bytecount;
            try
            {
                string remoteHost = ((IPEndPoint)so.RemoteEndPoint).Address.ToString();
                string remotePort = ((IPEndPoint)so.RemoteEndPoint).Port.ToString();
                bytecount = so.Send(bytes);
                Console.WriteLine("\n[Client] {0}:{1}", remoteHost, remotePort);
                Console.WriteLine("==SEND: \n" + PacketDebug(packet));
            }
            catch (Exception e)
            {
                Console.WriteLine("\n" + e.Message);
                return false;
            }
            return true;
        }
        /*
        private static bool isConnected()
        {
            try
            {
                return !(so.Poll(1, SelectMode.SelectRead) && so.Available == 0);
            }
            catch (SocketException) { return false; }
        }
        */
    }
}
