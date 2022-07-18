using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace JoeBidenPokerClubServer
{
    class Client
    {
        static readonly int s_maxBufferSize = 4096;
        public int id;
        public TcpClient? socket;
        private NetworkStream? stream;
        private byte[] receiveBuffer;
        Packet receivedPacket;
        public AccountManager.AccountInfo playerAccountInfo = null;

        Dictionary<ServerPackets, AsyncCallback?> clientCallbackMap = new Dictionary<ServerPackets, AsyncCallback?>();
        Dictionary<ClientPackets, Action<Packet>?> clientMessageHandlerMap = new Dictionary<ClientPackets, Action<Packet>?>();

        public Client(int i)
        {
            id = i;
            receiveBuffer = new byte[s_maxBufferSize];
            // callback function map
            clientCallbackMap[ServerPackets.welcome] = ServerRpc_welcome;
            // client rpc handler map
            clientMessageHandlerMap[ClientPackets.welcomeReceived] = ClientRpc_welcomeReceived;
            clientMessageHandlerMap[ClientPackets.login] = ClientRpc_login;
            clientMessageHandlerMap[ClientPackets.register] = ClientRpc_register;
        }
        public void Connect(TcpClient tc)
        {
            socket = tc;
            socket.ReceiveBufferSize = s_maxBufferSize;
            socket.SendBufferSize = s_maxBufferSize;
            stream = socket.GetStream();
            stream.BeginRead(receiveBuffer, 0, s_maxBufferSize, ReceiveCallback, null);
            receivedPacket = new Packet();

            ServerSend.RpcSend(ServerPackets.welcome, id, (Packet p) =>
            {
                p.Write($"Welcome to Joe Biden's Poker Club, Your id is {id}");
                p.Write(id);
            }, clientCallbackMap[ServerPackets.welcome]);
        }
        public void Disconnect()
        {
            Console.WriteLine($"{socket.Client.RemoteEndPoint} has disconnected");
            if (playerAccountInfo != null) AccountManager.Inst.Disconnect(playerAccountInfo.uid);
            socket?.Close();
            stream = null;
            socket = null;
            playerAccountInfo = null;
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                int l = stream.EndRead(result);
                if (l <= 0)
                {
                    Disconnect();
                    return;
                }
                byte[] data = new byte[l];
                Array.Copy(receiveBuffer, data, l);
                receivedPacket.Reset(HandlePacket(data));
                stream.BeginRead(receiveBuffer, 0, s_maxBufferSize, ReceiveCallback, null);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        bool HandlePacket(byte[] data)
        {
            int l = 0;
            receivedPacket.SetBytes(data);
            if (receivedPacket.Length() >= 4)
            {
                l = receivedPacket.ReadInt();
                if (l <= 0) return true;
            }
            while (l > 0 && l <= receivedPacket.UnreadLength())
            {
                byte[] bytes = receivedPacket.ReadBytes(l);
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    Packet p = new Packet(bytes);
                    ClientPackets clientRpcId = (ClientPackets)p.ReadInt();
                    if (clientMessageHandlerMap.ContainsKey(clientRpcId))
                        clientMessageHandlerMap[clientRpcId]?.Invoke(p);
                });
                l = 0;
                if (receivedPacket.UnreadLength() >= 4)
                {
                    l = receivedPacket.ReadInt();
                    if (l <= 0) return true;
                }
            }
            return l <= 1;
        }

        public void Send(Packet p, AsyncCallback? toRead = null)
        {
            try
            {
                if (socket == null || stream == null)
                {
                    Console.WriteLine($"client {id} does not have a valid socket/stream");
                    return;
                }
                stream.BeginWrite(p.ToArray(), 0, p.Length(), toRead, null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        #region server rpc callback
        private void ServerRpc_welcome(IAsyncResult result)
        {
            //Console.WriteLine("Receive Client Callback for welcome");
        }
        #endregion
        #region client rpc handler
        private void ClientRpc_welcomeReceived(Packet p)
        {
            int clientClaimId = p.ReadInt();
            if (clientClaimId != id)
            {
                Console.WriteLine($"client {id} claims to have id {clientClaimId}, there must be an error");
                return;
            }
        }
        private void ClientRpc_login(Packet p)
        {
            ServerSend.RpcSend(ServerPackets.loginCallback, id, (Packet returnP) =>
            {
                if (playerAccountInfo != null)
                {
                    // cannot login twice
                    returnP.Write(false);
                    return;
                }
                int clientUid = p.ReadInt();
                string password = p.ReadString();
                if (AccountManager.Inst.Login(clientUid, password))
                {
                    playerAccountInfo = AccountManager.Inst.GetAccount(clientUid);
                    returnP.Write(true);
                    returnP.Write(clientUid);
                }
                else
                {
                    returnP.Write(false);
                    returnP.Write(0);
                }
            });
        }
        private void ClientRpc_register(Packet p)
        {
            string clientName = p.ReadString();
            string password = p.ReadString();
            ThreadManager.ExecuteOnMainThread(()=>
            {
                var myUid = AccountManager.Inst.Register(clientName, password);
                ServerSend.RpcSend(ServerPackets.registerCallback, id, (Packet p) =>
                {
                    if (AccountManager.Inst.GetAccount(myUid) != null)
                    {
                        p.Write(true);
                        p.Write(myUid);
                    }
                    else
                    {
                        p.Write(false);
                        p.Write(0);
                    }
                });
            });
        }
        #endregion
    }
}
