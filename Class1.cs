using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace SocketUtiles
{
    class Client
    {
        public const int BuffSize = 1024;
        public const int MaxReceivedStrSize = 1000;
        public const int MinReceivedStrSize = 10;
        public const int ReconnectTimeInit = 25; // in ms
        public Socket Socket;
        public IPEndPoint Ep;
        public int ReconnectTimer;
        private string DataReceived;
        private string DataToSend;
        public byte[] ReceiveBuff = new byte[BuffSize];
        public byte[] SendBuff = new byte[BuffSize];
        public int SendDataLength;
        public ManualResetEvent SendBuffAvaible = new ManualResetEvent(false);
        public ManualResetEvent ReceiveAvaible = new ManualResetEvent(false);
        public ManualResetEvent SocketAvaible = new ManualResetEvent(false);
        private object DataReceivedLock = new object();
        private object DataSendLock = new object();

        public void ResetReconnectTimer()
        {
            ReconnectTimer = ReconnectTimeInit;
        }
        public Client(Socket s, IPEndPoint Ep)
        {
            this.Socket = s;
            this.Ep = Ep;
            this.DataReceived = "";
            this.DataToSend = "";
            this.SendDataLength = 0;
            this.ReconnectTimer = ReconnectTimeInit;
        }
        public void FetchDataToRead(int length)
        {
            if (Monitor.TryEnter(DataReceivedLock, TimeSpan.FromMilliseconds(100)))
            {
                try
                {
                    if (DataReceived.Length > MaxReceivedStrSize)
                    {
                        ReceiveAvaible.Reset();
                    }
                    if (DataReceived.Length <= MinReceivedStrSize)
                    {
                        ReceiveAvaible.Set();
                    }
                    DataReceived += Encoding.ASCII.GetString(ReceiveBuff, 0, length);
                }
                finally
                {
                    Monitor.Exit(DataReceivedLock);
                }
            }
            else
            {
                FetchDataToRead(length);
            }
        }
        public void ReadAllData(int MaxLength)
        {
        }
        public string ReadAllData()
        {
            if (Monitor.TryEnter(DataReceivedLock, TimeSpan.FromMilliseconds(100)))
            {
                try
                {
                    string tmp = DataReceived;
                    DataReceived = "";
                    return tmp;
                }
                finally
                {
                    Monitor.Exit(DataReceivedLock);
                }
            }
            else
            {
                return "";
            }
        }

        public void AddDataToSend(string strToSend)
        {
            if (Monitor.TryEnter(DataSendLock))
            {
                DataToSend += strToSend;
                Monitor.Exit(DataSendLock);
            }
            SendBuffAvaible.Set();
        }
        public void FetchDataToSend()
        {
            byte[] tmp;
            if (Monitor.TryEnter(DataSendLock))
            {
                tmp = Encoding.ASCII.GetBytes(DataToSend);
                Monitor.Exit(DataSendLock);
            }
            else
            {
                return;
            }
            Array.Copy(tmp, 0, SendBuff, SendDataLength, tmp.Length);
            if (tmp.Length >= BuffSize)
            {
                SendDataLength += BuffSize;
            }
            else
            {
                SendDataLength += tmp.Length;
            }
        }
        public void BytesSent(int n)
        {
            if (n > SendDataLength)
            {
                throw new Exception("More Bytes Sent then Bytes remaining!");
            }
            else
            {
                SendDataLength -= n;
                Array.Copy(SendBuff, n, SendBuff, 0, SendDataLength);
                if (SendDataLength == 0 && DataToSend.Length == 0)
                {
                    SendBuffAvaible.Reset();
                }
            }
        }
    }
    class UtileClient
    {
        private readonly string ServerIp;
        private readonly int ServerPort;

        //private Client server;
        //private List<Client> clients = new List<Client>();
        private ManualResetEvent ConnectDone = new ManualResetEvent(false);
        private void ConnectCallback(IAsyncResult ar)
        {
            Client server = (Client)ar.AsyncState;
            try
            {
                server.Socket.EndConnect(ar);
                server.SocketAvaible.Set();
                server.ReceiveAvaible.Set();
                Console.WriteLine("Socket connected to {0}",
                    server.Socket.RemoteEndPoint.ToString());
                server.ResetReconnectTimer();
                server.Socket.BeginReceive(server.ReceiveBuff, 0, Client.BuffSize, 0,
                    new AsyncCallback(ServerReadCallBack), server);
                server.Socket.BeginSend(server.SendBuff, 0, server.SendDataLength, SocketFlags.None,
                    new AsyncCallback(ServerSendCallBack), server);
            }
            catch (SocketException e)
            {
                //server.Socket = null;
                server.SocketAvaible.Reset();
                server.ReceiveAvaible.Reset();
                Console.WriteLine("Remote server not working: " + server.Ep.ToString() +
                    ", reconnect in " + server.ReconnectTimer.ToString() + "ms");
                Thread.Sleep(server.ReconnectTimer);
                server.ReconnectTimer *= 2;
                server.Socket.BeginConnect(server.Ep,
                    new AsyncCallback(ConnectCallback), ar.AsyncState);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        public void ServerSendCallBack(IAsyncResult ar)
        {
            try
            {
                Client client = (Client)ar.AsyncState;
                int BytesSent = client.Socket.EndSend(ar);
                client.BytesSent(BytesSent);

                client.SendBuffAvaible.WaitOne();
                client.FetchDataToSend();
                client.Socket.BeginSend(client.SendBuff, 0, client.SendDataLength, SocketFlags.None,
                    new AsyncCallback(ServerSendCallBack), client);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        public void ServerReadCallBack(IAsyncResult ar)
        {
            Client client = (Client)ar.AsyncState;
            int BytesReceived = 0;
            try
            {
                BytesReceived = client.Socket.EndReceive(ar);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            if (BytesReceived > 0)
            {
                client.FetchDataToRead(BytesReceived);
                client.ReceiveAvaible.WaitOne();
                client.Socket.BeginReceive(client.ReceiveBuff, 0, Client.BuffSize, 0,
                    new AsyncCallback(ServerReadCallBack), client);
            }
            else
            {
                client.Socket.Close();
            }
        }
        public void ServerAcceptCallBack(IAsyncResult ar)
        {
            List<Client> clients = (List<Client>)ar.AsyncState;
            Socket server = clients[0].Socket;
            Socket remote = server.EndAccept(ar);
            Client client = new Client(remote, (IPEndPoint)remote.RemoteEndPoint);
            Console.WriteLine("New client connecting: "
                + ((IPEndPoint)client.Socket.RemoteEndPoint).Address.ToString()
                + " port: " + ((IPEndPoint)client.Socket.RemoteEndPoint).Port.ToString());

            // say hallo to client
            clients.Add(client);
            client.SocketAvaible.Set();
            client.ReceiveAvaible.Set();

            // begin receiving the data from client
            client.Socket.BeginReceive(client.ReceiveBuff, 0, Client.BuffSize, 0,
                new AsyncCallback(ServerReadCallBack), client);
            client.Socket.BeginSend(client.SendBuff, 0, client.SendDataLength, SocketFlags.None,
                new AsyncCallback(ServerSendCallBack), client);

            // relance the listen process
            Console.WriteLine("New listen processe lanced");
            server.BeginAccept(new AsyncCallback(ServerAcceptCallBack), server);
        }
        public List<Client> CreateServer()
        {
            // Make server listening
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(ServerIp), ServerPort);
            serverSocket.Bind(endPoint);
            serverSocket.Listen(10);

            List<Client> clients = new List<Client>();
            Client server = new Client(serverSocket, endPoint);
            clients.Add(server);
            // Begin connect client asyncally
            serverSocket.BeginAccept(new AsyncCallback(ServerAcceptCallBack), clients);
            return clients;
        }
        public Client CreateClientUntilDone()
        {
            var server = CreateClientOnlyBegin();
            server.SocketAvaible.WaitOne();
            return server;
        }
        public Client CreateClientOnlyBegin()
        {
            Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(ServerIp), ServerPort);
            Client server = new Client(client, endpoint);
            server.Socket = client;
            client.BeginConnect(endpoint,
                new AsyncCallback(ConnectCallback), server);
            return server;
        }
        public UtileClient(string ServerIp, int ServerPort)
        {
            this.ServerIp = ServerIp;//"127.0.0.1"
            this.ServerPort = ServerPort;// 11000
        }
        public void EndConnect(Client server)
        {
            server.Socket.Shutdown(SocketShutdown.Both);
            server.Socket.Close();
        }
    }
}
