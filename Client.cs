﻿using System;
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
            clientMessageHandlerMap[ClientPackets.joinRoom] = ClientRpc_joinRoom;
            clientMessageHandlerMap[ClientPackets.createRoom] = ClientRpc_createRoom;
            clientMessageHandlerMap[ClientPackets.bid] = ClientRpc_bid;
            clientMessageHandlerMap[ClientPackets.fold] = ClientRpc_fold;
            clientMessageHandlerMap[ClientPackets.useTimeCard] = ClientRpc_useTimeCard;
            clientMessageHandlerMap[ClientPackets.quitRoom] = ClientRpc_quitRoom;
            clientMessageHandlerMap[ClientPackets.sendChat] = ClientRpc_sendChat;
            clientMessageHandlerMap[ClientPackets.requestAccountInfo] = ClientRpc_requestAccountInfo;
            clientMessageHandlerMap[ClientPackets.requestRoomList] = ClientRpc_requestRoomList;
            clientMessageHandlerMap[ClientPackets.observeRoom] = ClientRpc_observeRoom;
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
            if (playerAccountInfo != null)
            {
                var r = RoomManager.GetPlayerRoom(playerAccountInfo.uid);
                if (r != null)
                {
                    r.OnPlayerExitRoom(this);
                }
                AccountManager.Inst.Disconnect(playerAccountInfo.uid);
            }
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
                    Console.WriteLine($"client {id} trying to log in twice");
                    returnP.Write(false);
                    return;
                }
                int clientUid = p.ReadInt();
                string password = p.ReadString();
                if (AccountManager.Inst.Login(clientUid, password))
                {
                    playerAccountInfo = AccountManager.Inst.GetAccount(clientUid);
                    Console.WriteLine($"{clientUid} log in success");
                    returnP.Write(true);
                    returnP.Write(clientUid);
                }
                else
                {
                    Console.WriteLine($"{clientUid} log in fail");
                    returnP.Write(false);
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
                ServerSend.RpcSend(ServerPackets.registerCallback, id, (Packet returnP) =>
                {
                    if (playerAccountInfo != null)
                    {
                        Console.WriteLine($"client {id} trying to register when logged in");
                        returnP.Write(false);
                        return;
                    }
                    if (AccountManager.Inst.GetAccount(myUid) != null)
                    {
                        playerAccountInfo = AccountManager.Inst.GetAccount(myUid);
                        returnP.Write(true);
                        returnP.Write(myUid);
                    }
                    else
                    {
                        returnP.Write(false);
                        returnP.Write(0);
                    }
                });
            });
        }
        private void ClientRpc_joinRoom(Packet p)
        {
            if (playerAccountInfo == null)
            {
                Console.WriteLine($"user trying to join room without an account");
                ThreadManager.ExecuteOnMainThread(() => {
                    ServerSend.RpcSend(ServerPackets.joinRoomCallback, id, (Packet returnP) =>
                    {
                        returnP.Write(false);
                    });
                });
                return;
            }
            if (RoomManager.GetPlayerRoom(playerAccountInfo.uid) != null)
            {
                Console.WriteLine($"player {playerAccountInfo.uid} has already joined a room {RoomManager.GetRoomIdx(RoomManager.GetPlayerRoom(playerAccountInfo.uid))}");
                ThreadManager.ExecuteOnMainThread(() => {
                    ServerSend.RpcSend(ServerPackets.joinRoomCallback, id, (Packet returnP) =>
                    {
                        returnP.Write(false);
                    });
                });
                return;
            }
            ThreadManager.ExecuteOnMainThread(() =>
            {
                int toJoin = p.ReadInt();
                int cashIn = p.ReadInt();
                bool joinSuccess = false;
                if (cashIn > playerAccountInfo.cash)
                    joinSuccess = false;
                else if (toJoin == -1)
                    joinSuccess = RoomManager.GetFreeRoom().OnPlayerEnterRoom(this, cashIn);
                else
                    joinSuccess = RoomManager.GetRoom(toJoin).OnPlayerEnterRoom(this, cashIn);

                ServerSend.RpcSend(ServerPackets.joinRoomCallback, id, (Packet returnP) =>
                {
                    returnP.Write(joinSuccess);
                    if (joinSuccess)
                        returnP.Write(RoomManager.GetRoomIdx(RoomManager.GetPlayerRoom(playerAccountInfo.uid)));
                });
                ThreadManager.ExecuteAfterFrame(() =>
                {
                    var r = RoomManager.GetPlayerRoom(playerAccountInfo.uid);
                    if (r != null)
                    {
                        r.ForceSync();
                    }
                }, 3);
            });
        }
        private void ClientRpc_createRoom(Packet p)
        {
            if (playerAccountInfo == null)
            {
                Console.WriteLine($"user trying to create room without an account");
                ThreadManager.ExecuteOnMainThread(() => {
                    ServerSend.RpcSend(ServerPackets.createRoomCallback, id, (Packet returnP) =>
                    {
                        returnP.Write(false);
                    });
                });
                return;
            }
            if (RoomManager.GetPlayerRoom(playerAccountInfo.uid) != null)
            {
                Console.WriteLine($"player {playerAccountInfo.uid} has already joined a room {RoomManager.GetRoomIdx(RoomManager.GetPlayerRoom(playerAccountInfo.uid))}");
                ThreadManager.ExecuteOnMainThread(() => {
                    ServerSend.RpcSend(ServerPackets.createRoomCallback, id, (Packet returnP) =>
                    {
                        returnP.Write(false);
                    });
                });
                return;
            }
            Room r = new Room(p.ReadString(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadInt());
            ThreadManager.ExecuteAfterFrame(() =>
            {
                ServerSend.RpcSend(ServerPackets.createRoomCallback, id, (Packet returnP) =>
                {
                    Console.WriteLine($"room no.{RoomManager.GetRoomIdx(r)} is ready");
                    returnP.Write(RoomManager.GetRoomIdx(r) != -1);
                    if (RoomManager.GetRoomIdx(r) != -1)
                        returnP.Write(RoomManager.GetRoomIdx(r));
                });
            });
        }
        private void ClientRpc_bid(Packet p)
        {
            if (playerAccountInfo == null)
            {
                Console.WriteLine($"user trying to bid without an account");
                ThreadManager.ExecuteOnMainThread(() => {
                    ServerSend.RpcSend(ServerPackets.bidCallback, id, (Packet returnP) =>
                    {
                        returnP.Write(false);
                    });
                });
                return;
            }
            if (RoomManager.GetPlayerRoom(playerAccountInfo.uid) == null)
            {
                Console.WriteLine($"player {playerAccountInfo.uid} is trying to bid outside a room");
                ThreadManager.ExecuteOnMainThread(() => {
                    ServerSend.RpcSend(ServerPackets.bidCallback, id, (Packet returnP) =>
                    {
                        returnP.Write(false);
                    });
                });
                return;
            }
            ThreadManager.ExecuteOnMainThread(() =>
            {
                RoomManager.GetPlayerRoom(playerAccountInfo.uid).HandlePacket(p, ClientPackets.bid);
            });
        }
        private void ClientRpc_fold(Packet p)
        {
            if (playerAccountInfo == null)
            {
                Console.WriteLine($"user trying to bfoldwithout an account");
                ThreadManager.ExecuteOnMainThread(() => {
                    ServerSend.RpcSend(ServerPackets.foldCallback, id, (Packet returnP) =>
                    {
                        returnP.Write(false);
                    });
                });
                return;
            }
            if (RoomManager.GetPlayerRoom(playerAccountInfo.uid) == null)
            {
                Console.WriteLine($"player {playerAccountInfo.uid} is trying to fold outside a room");
                ThreadManager.ExecuteOnMainThread(() => {
                    ServerSend.RpcSend(ServerPackets.foldCallback, id, (Packet returnP) =>
                    {
                        returnP.Write(false);
                    });
                });
                return;
            }
            ThreadManager.ExecuteOnMainThread(() =>
            {
                RoomManager.GetPlayerRoom(playerAccountInfo.uid).HandlePacket(p, ClientPackets.fold);
            });
        }
        private void ClientRpc_useTimeCard(Packet p)
        {
            if (playerAccountInfo == null)
            {
                Console.WriteLine($"user trying to use time card without an account");
                ThreadManager.ExecuteOnMainThread(() => {
                    ServerSend.RpcSend(ServerPackets.useTimeCardCallback, id, (Packet returnP) =>
                    {
                        returnP.Write(false);
                    });
                });
                return;
            }
            if (RoomManager.GetPlayerRoom(playerAccountInfo.uid) == null)
            {
                Console.WriteLine($"player {playerAccountInfo.uid} is trying to use time card outside a room");
                ThreadManager.ExecuteOnMainThread(() => {
                    ServerSend.RpcSend(ServerPackets.useTimeCardCallback, id, (Packet returnP) =>
                    {
                        returnP.Write(false);
                    });
                });
                return;
            }
            ThreadManager.ExecuteOnMainThread(() =>
            {
                RoomManager.GetPlayerRoom(playerAccountInfo.uid).HandlePacket(p, ClientPackets.useTimeCard);
            });
        }
        private void ClientRpc_quitRoom(Packet p)
        {
            if (playerAccountInfo == null)
            {
                Console.WriteLine($"user trying to quit room without an account");
                ThreadManager.ExecuteOnMainThread(() => {
                    ServerSend.RpcSend(ServerPackets.quitRoomCallback, id, (Packet returnP) =>
                    {
                        returnP.Write(false);
                    });
                });
                return;
            }
            if (RoomManager.GetPlayerRoom(playerAccountInfo.uid) == null)
            {
                Console.WriteLine($"player {playerAccountInfo.uid} is trying to quit room outside a room");
                ThreadManager.ExecuteOnMainThread(() => {
                    ServerSend.RpcSend(ServerPackets.quitRoomCallback, id, (Packet returnP) =>
                    {
                        returnP.Write(false);
                    });
                });
                return;
            }
            ThreadManager.ExecuteOnMainThread(() =>
            {
                RoomManager.GetPlayerRoom(playerAccountInfo.uid).OnPlayerExitRoom(this);
                ServerSend.RpcSend(ServerPackets.quitRoomCallback, id, (Packet returnP) =>
                {
                    returnP.Write(true);
                });
            });
        }
        private void ClientRpc_sendChat(Packet p)
        {
            // send chat!
        }
        private void ClientRpc_requestAccountInfo(Packet p)
        {
            int pid = p.ReadInt();
            var info = AccountManager.Inst.GetAccount(pid);
            ThreadManager.ExecuteOnMainThread(() =>
            {
                ServerSend.RpcSend(ServerPackets.sendAccountInfo, id, (Packet returnP) =>
                {
                    if (info != null)
                    {
                        returnP.Write(true);
                        returnP.Write(info.uid);
                        returnP.Write(info.name);
                        returnP.Write(info.cash);
                        returnP.Write(info.gameWin + info.gameLose == 0 ?
                            0.0f : (float)info.gameWin / (float)(info.gameWin + info.gameLose));
                        returnP.Write(info.cashWin);
                        returnP.Write(info.cashLose);
                        returnP.Write(info.gameWin + info.gameLose);
                        returnP.Write(info.gameWin + info.gameLose == 0 ?
                            0.0f : (float)info.gameWin / (float)(info.gameWin + info.gameLose));
                    }
                    else
                    {
                        returnP.Write(false);
                    }
                });
            });
        }
        private void ClientRpc_requestRoomList(Packet p)
        {
            ThreadManager.ExecuteOnMainThread(() =>
            {
                ServerSend.RpcSend(ServerPackets.sendRoomList, id, (Packet returnP) =>
                {
                    returnP.Write(RoomManager.Rooms.Count);
                    foreach (var r in RoomManager.Rooms)
                    {
                        var info = r.ReportStat();
                        returnP.Write(info.roomID);
                        returnP.Write(info.name);
                        returnP.Write(info.maxPlayer);
                        returnP.Write(info.curPlayer);
                        returnP.Write(info.maxOb);
                        returnP.Write(info.curOb);
                        returnP.Write(info.sb);
                        returnP.Write(info.roundTime);
                        returnP.Write(info.roundPerTimeCard);
                        returnP.Write(info.roundPassed);
                    }
                });
            });
        }
        private void ClientRpc_observeRoom(Packet p)
        {
            // observe room!
        }
        #endregion
    }
}
