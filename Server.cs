using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace JoeBidenPokerClubServer
{
    class Server
    {
        public static int maxPlayer { get; private set; }
        public static int port { get; private set; }
        private static TcpListener? tcpListener;
        static public Dictionary<int, Client> clients = new Dictionary<int, Client>();
        public static void Start(int p, int mp = 128)
        {
            maxPlayer = mp; port = p;
            InitServerData();
            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();
            tcpListener.BeginAcceptTcpClient(new AsyncCallback(TcpConnectCallback), null);
            Console.WriteLine($"Server Started on port {port}");
        }

        private static void TcpConnectCallback(IAsyncResult result)
        {
            if (tcpListener == null)
            {
                Console.WriteLine("Client connect callback failed because listener has not been initialized yet!");
            }
            TcpClient client = tcpListener.EndAcceptTcpClient(result);
            bool found = false;
            for (int i = 1; i < maxPlayer; i++)
            {
                if (clients[i].socket == null)
                {
                    clients[i].socket = client;
                    found = true;
                    Console.WriteLine($"welcome! new client from {client.Client.RemoteEndPoint}");
                    clients[i].Connect(clients[i].socket);
                    break;
                }
            }
            if (!found)
            {
                Console.WriteLine("Max player limit reached, new clients will be ignored!");
                return;
            }
            tcpListener.BeginAcceptTcpClient(new AsyncCallback(TcpConnectCallback), null);

        }

        private static void InitServerData()
        {
            for (int i = 1; i < maxPlayer; i++)
            {
                clients[i] = new Client(i);
            }
        }
    }
}
