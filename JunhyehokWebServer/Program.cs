using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Junhaehok;
using static Junhaehok.HhhHelper;
using System.Web;
using System.Net.WebSockets;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace JunhyehokWebServer
{
    class Program
    {
        static void Main(string[] args)
        {
            string host = null;     //Default
            string clientPort = "30000";  //Default
            string mmfName = "JunhyehokMmf"; //Default

            //=========================GET ARGS=================================
            if (args.Length == 0)
            {
                Console.WriteLine("Format: JunhyehokServer -cp [client port] -mmf [MMF name]");
                Environment.Exit(0);
            }

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--help":
                        Console.WriteLine("Format: JunhyehokServer -cp [client port] -mmf [MMF name]");
                        Environment.Exit(0);
                        break;
                    case "-mmf":
                        mmfName = args[++i];
                        break;
                    case "-cp":
                        clientPort = args[++i];
                        break;
                    default:
                        Console.Error.WriteLine("ERROR: incorrect inputs \nFormat: JunhyehokServer -cp [client port] -mmf [MMF name]");
                        Environment.Exit(0);
                        break;
                }
            }

            //=======================AGENT CONNECT================================
            Console.WriteLine("Connecting to Agent...");
            string agentInfo = "127.0.0.1:40000";
            Socket agentSocket = Connect(agentInfo);
            BackendHandle agent = new BackendHandle(agentSocket);
            agent.StartSequence();

            //======================BACKEND CONNECT===============================
            Console.WriteLine("Connecting to Backend...");
            string backendInfo = "";
            try { backendInfo = System.IO.File.ReadAllText("backend.conf"); }
            catch (Exception e) { Console.WriteLine("\n" + e.Message); Environment.Exit(0); }
            Socket backendSocket = Connect(backendInfo);
            BackendHandle backend = new BackendHandle(backendSocket);
            AdvertiseToBackend(backend, clientPort);
            backend.StartSequence();

            //======================INITIALIZE==================================
            Console.WriteLine("Initializing lobby and rooms...");
            ReceiveHandle recvHandle = new ReceiveHandle(backendSocket, mmfName);

            //======================UPDATE MMF==================================
            Console.WriteLine("Updating Memory Mapped File...");
            ReceiveHandle.UpdateMMF();

            //===================CLIENT SOCKET ACCEPT===========================
            Console.WriteLine("Accepting clients...");
            string address = "http://+:" + clientPort + "/wsJinhyehok/";

            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(address);
            listener.Start();

            while (true)
            {
                HttpListenerContext listenerContext = listener.GetContext();
                if (listenerContext.Request.IsWebSocketRequest)
                {
                    ClientHandle client = new ClientHandle(listenerContext);
                    client.StartSequence();
                }
                else
                {
                    listenerContext.Response.StatusCode = 400;
                    listenerContext.Response.Close();
                }
            }

            /*
            StartAcceptAsync(address);

            //================READ MMF TO GET IPX COMMANDS======================
            while (true)
            {
                using (var mmf = MemoryMappedFile.OpenExisting(mmfName + "IPX"))
                {
                    byte[] buffer = new byte[1];
                    // Create accessor to MMF
                    using (var accessor = mmf.CreateViewAccessor(0, buffer.Length))
                    {
                        // Wait for the lock
                        Mutex mutex = Mutex.OpenExisting("MMF_IPX" + mmfName);
                        mutex.WaitOne();

                        // Read from MMF
                        accessor.ReadArray<byte>(0, buffer, 0, buffer.Length);
                        if (buffer[0] == 0)
                        {
                            mutex.ReleaseMutex();
                            break;
                        }
                        mutex.ReleaseMutex();
                    }
                }
            }
            ReceiveHandle.UpdateMMF(false);
            backend.So.Shutdown(SocketShutdown.Both);
            backend.So.Close();
            Environment.Exit(0);
            */
        }
        public static async void StartAcceptAsync(string listenerPrefix)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(listenerPrefix);
            listener.Start();

            while (true)
            {
                //HttpListenerContext listenerContext = listener.GetContext();
                HttpListenerContext listenerContext = await HelperExtensions.GetContextAsync(listener);
                if (listenerContext.Request.IsWebSocketRequest)
                {
                    ClientHandle client = new ClientHandle(listenerContext);
                    client.StartSequence();
                }
                else
                {
                    listenerContext.Response.StatusCode = 400;
                    listenerContext.Response.Close();
                }
            }
        }
        public static void AdvertiseToBackend(BackendHandle backend, string clientPort)
        {
            FBAdvertiseRequest fbAdvertiseRequest;
            char[] ip = ((IPEndPoint)backend.So.LocalEndPoint).Address.ToString().ToCharArray();
            char[] ipBuffer = new char[15];
            Array.Copy(ip, ipBuffer, ip.Length);
            fbAdvertiseRequest.ip = ipBuffer;
            fbAdvertiseRequest.port = int.Parse(clientPort);
            byte[] advertiseBytes = Serializer.StructureToByte(fbAdvertiseRequest);
            backend.So.SendBytes(new Packet(new Header(Code.ADVERTISE, (ushort)advertiseBytes.Length), advertiseBytes));
        }
        public static Socket Connect(string info)
        {
            string host;
            int port;
            string[] hostport = info.Split(':');
            host = hostport[0];
            if (!int.TryParse(hostport[1], out port))
            {
                Console.Error.WriteLine("port must be int. given: {0}", hostport[1]);
                Environment.Exit(0);
            }

            Socket so = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ipAddress = IPAddress.Parse(host);

            Console.WriteLine("Establishing connection to {0}:{1} ...", host, port);

            try
            {
                so.Connect(ipAddress, port);
                Console.WriteLine("Connection established.\n");
            }
            catch (Exception)
            {
                Console.WriteLine("Peer is not alive.");
            }

            return so;
        }
    }

    // This extension method wraps the BeginGetContext / EndGetContext methods on HttpListener as a Task, using a helper function from the Task Parallel Library (TPL).
    // This makes it easy to use HttpListener with the C# 5 asynchrony features.
    public static class HelperExtensions
    {
        public static Task<HttpListenerContext> GetContextAsync(this HttpListener listener)
        {
            return Task.Factory.FromAsync<HttpListenerContext>(listener.BeginGetContext, listener.EndGetContext, TaskCreationOptions.None);
        }
    }
}
