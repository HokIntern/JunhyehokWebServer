using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Junhaehok;
using static Junhaehok.HhhHelper;
using static Junhaehok.Packet;

namespace JunhyehokWebServer
{
    class BackendHandle
    {
        bool debug = true;

        Socket so;
        int bytecount;
        int heartbeatMiss = 0;

        private char[] cookieChar;
        private string cookie;
        private byte[] cookieBites;
        private long userId;
        private State status;
        private bool isDummy;
        private int roomId;
        private int chatCount;
        ReceiveHandle recvHandler;

        string remoteHost;
        string remotePort;

        public Socket So { get { return so; } }
        public char[] CookieChar { get { return cookieChar; } set { cookieChar = value; Cookie = cookieChar.ToString(); } }
        public string Cookie { get { return cookie; } set { cookie = value; cookieBites = Encoding.UTF8.GetBytes(value); } }
        public byte[] CookieBites { get { return cookieBites; } }
        public long UserId { get { return userId; } set { userId = value; } }
        public State Status { get { return status; } set { status = value; } }
        public bool IsDummy { get { return isDummy; } set { isDummy = value; } }
        public int RoomId { get { return roomId; } set { roomId = value; } }
        public int ChatCount { get { return chatCount; } set { chatCount = value; } }

        public enum State
        {
            Offline, Online, Lobby, Room, Monitoring, Error
        }

        public BackendHandle(Socket so)
        {
            this.so = so;
            status = State.Online;

            remoteHost = ((IPEndPoint)So.RemoteEndPoint).Address.ToString();
            remotePort = ((IPEndPoint)So.RemoteEndPoint).Port.ToString();
            userId = -1;
            Console.WriteLine("[Client] Connection established with {0}:{1}\n", remoteHost, remotePort);
        }

        public async void StartSequence()
        {
            Packet recvRequest;
            bool doSignout = true;
            while (true)
            {
                recvRequest = await SocketRecvAsync();

                if (ushort.MaxValue == recvRequest.header.code)
                    break;

                //=================Process Request/Get Response=================
                ReceiveHandle recvHandle = new ReceiveHandle(this, recvRequest);
                Packet respPacket = recvHandle.GetResponse();

                //=======================Send Response==========================
                if (ushort.MaxValue != respPacket.header.code) //if it isnt a NoResponsePacket
                {
                    byte[] respBytes = PacketToBytes(respPacket);
                    bool sendSuccess = SendBytes(respBytes);
                    if (!sendSuccess)
                    {
                        Console.WriteLine("Send failed.");
                        break;
                    }
                }

                // if Initialize_fail, it means the user came with a bad cookie
                if (respPacket.header.code == Code.INITIALIZE_FAIL)
                {
                    doSignout = true;
                    break; //close socket connection
                }
                else if (respPacket.header.code == Code.DELETE_USER_SUCCESS || respPacket.header.code == Code.SIGNOUT)
                {
                    //consider removing the RemoveClient call in the SIGNOUT switch/case
                    doSignout = false;
                    break;
                }

                //=======================Check Connection=======================
                if (!isConnected())
                {
                    Console.WriteLine("Connection lost with {0}:{1}", remoteHost, remotePort);
                    break;
                }
            }
            CloseConnection(doSignout);
        }

        private async Task<Packet> SocketRecvAsync()
        {
            Packet disconnectedFlagPacket = new Packet(new Header(ushort.MaxValue, 0), null);
            //=========================Receive==============================
            Header recvHeader;
            Packet recvRequest;

            //========================get HEADER============================
            byte[] headerBytes = await GetBytesAsync(HEADER_SIZE);
            if (null == headerBytes)
                return disconnectedFlagPacket;
            recvHeader = BytesToHeader(headerBytes);
            recvRequest.header = recvHeader;

            //========================get DATA==============================
            byte[] dataBytes = await GetBytesAsync(recvHeader.size);
            if (null == dataBytes)
                return disconnectedFlagPacket;
            recvRequest.data = dataBytes;

            return recvRequest;
        }

        public void CloseConnection(bool signout)
        {
            //=================Signout/Close Connection/Exit Thread==================
            Console.WriteLine("Closing connection with {0}:{1}", remoteHost, remotePort);
            so.Shutdown(SocketShutdown.Both);
            so.Close();
            Console.WriteLine("Connection closed\n");
        }

        private async Task<byte[]> GetBytesAsync(int length)
        {
            byte[] bytes = new byte[length];
            if (length != 0) //this check has to exist. otherwise Receive timeouts for 60seconds while waiting for nothing
            {
                try
                {
                    //so.ReceiveTimeout = 3000000;
                    so.ReceiveTimeout = 20000;
                    bytecount = await Task.Run(() => so.Receive(bytes));

                    //assumes that the line above(so.Receive) will throw exception 
                    //if times out, so the line below(reset hearbeatMiss) will not be reached
                    //if an exception is thrown.
                    heartbeatMiss = 0;
                }
                catch (Exception e)
                {
                    if (!isConnected())
                    {
                        Console.WriteLine("\n" + e.Message);
                        return null;
                    }
                    else
                    {
                        if (bytes.Length != 0)
                        {
                            heartbeatMiss++;
                            if (heartbeatMiss == 3)
                            {
                                Console.WriteLine("[HEARBEAT MISSED] {0}:{1}", remoteHost, remotePort);
                                return null;
                            }

                            //puts -1 bytes into 1st and 2nd bytes (CODE)
                            byte[] noRespBytes = BitConverter.GetBytes((ushort)ushort.MaxValue - 1);
                            bytes[FieldIndex.CODE] = noRespBytes[0];
                            bytes[FieldIndex.CODE + 1] = noRespBytes[1];
                        }
                    }
                }
            }
            return bytes;
        }

        private bool SendBytes(byte[] bytes)
        {
            try
            {
                bytecount = so.Send(bytes);
            }
            catch (Exception e)
            {
                Console.WriteLine("\n" + e.Message);
                return false;
            }
            return true;
        }

        private bool isConnected()
        {
            try
            {
                return !(so.Poll(1, SelectMode.SelectRead) && so.Available == 0);
            }
            catch (SocketException) { return false; }
            catch (Exception) { return false; }
        }
    }
}
