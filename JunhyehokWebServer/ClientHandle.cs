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
using System.Net.WebSockets;

namespace JunhyehokWebServer
{
    class ClientHandle
    {
        bool debug = true;

        HttpListenerContext listenerContext;
        int bytecount;
        int heartbeatMiss = 0;

        private WebSocket webSocket;
        private char[] cookieChar;
        private string cookie;
        private byte[] cookieBites;
        private long userId;
        private State status;
        private bool isDummy;
        private int roomId;
        private int chatCount;
        ReceiveHandle recvHandler;

        public string remoteHost;
        public string remotePort;

        public WebSocket WebSocket { get { return webSocket; } }
        public HttpListenerContext ListenerContext { get { return listenerContext; } }
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

        public ClientHandle(HttpListenerContext listenerContext)
        {
            this.listenerContext = listenerContext;
            status = State.Online;
            
            remoteHost = ((IPEndPoint)listenerContext.Request.RemoteEndPoint).Address.ToString();
            remotePort = ((IPEndPoint)listenerContext.Request.RemoteEndPoint).Port.ToString();
            userId = -1;
            Console.WriteLine("[Client] Connection established with {0}:{1}\n", remoteHost, remotePort);
        }

        public async void StartSequence()
        {
            Packet recvRequest;
            bool doSignout = true;

            int count = 0;
            WebSocketContext webSocketContext = null;
            try
            {
                webSocketContext = await listenerContext.AcceptWebSocketAsync(subProtocol: null);
                Interlocked.Increment(ref count);
                Console.WriteLine("Processed: {0}", count);
            }
            catch (Exception e)
            {
                listenerContext.Response.StatusCode = 500;
                listenerContext.Response.Close();
                Console.WriteLine("Exception: {0}", e);
                return;
            }

            webSocket = webSocketContext.WebSocket;

            try
            {
                byte[] receiveBuffer = new byte[1024];

                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
                        
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        break;
                    }
                    else if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Cannot accept text frame", CancellationToken.None);
                        break;
                    }
                    else
                    {
                        recvRequest = BytesToPacket(receiveBuffer);

                        if (ushort.MaxValue == recvRequest.header.code)
                            break;

                        //=================Process Request/Get Response=================
                        ReceiveHandle recvHandle = new ReceiveHandle(this, recvRequest);
                        Packet respPacket = recvHandle.GetResponse();

                        //=======================Send Response==========================
                        if (ushort.MaxValue != respPacket.header.code) //if it isnt a NoResponsePacket
                        {
                            //byte[] respBytes = PacketToBytes(respPacket);
                            SendPacket(respPacket);
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
                    }
                }
            }
            catch (Exception e)
            {
                // Just log any exceptions to the console. Pretty much any exception that occurs when calling `SendAsync`/`ReceiveAsync`/`CloseAsync` is unrecoverable in that it will abort the connection and leave the `WebSocket` instance in an unusable state.
                Console.WriteLine("Exception: {0}", e);
            }
            finally
            {
                // Clean up by disposing the WebSocket once it is closed/aborted.
                if (webSocket != null)
                    webSocket.Dispose();
            }

            CloseConnection(doSignout);
        }

        public void CloseConnection(bool signout)
        {
            //=================Signout/Close Connection/Exit Thread==================
            Signout(signout);
            Console.WriteLine("Closing connection with {0}:{1}", remoteHost, remotePort);
            Console.WriteLine("Connection closed\n");
        }

        public async void SendPacket(Packet packet)
        {
            byte[] bytes = PacketToBytes(packet);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length), WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        private void Signout(bool signout)
        {
            if (status == State.Lobby || status == State.Room)
            {
                ReceiveHandle.RemoveClient(this, signout);
                this.roomId = 0;
            }
            this.status = State.Offline;
        }
    }
}
