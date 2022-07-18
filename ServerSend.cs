using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace JoeBidenPokerClubServer
{
    class ServerSend
    {
        public static void RpcSend(ServerPackets rpc, int clientId, Action<Packet> toWrite, AsyncCallback? toRead = null)
        {
            Packet p = new Packet(rpc);
            toWrite(p);
            p.WriteLength();
            if (p.Length() > Packet.s_maxBufferSize)
            {
                Console.WriteLine($"RPC packet {(int)rpc} has exceeded the max length of a single packet");
                return;
            }
            Server.clients[clientId].Send(p, toRead);
        }
        public static void RpcSendAll(ServerPackets rpc, Action<Packet> toWrite, AsyncCallback? toRead = null)
        {
            Packet p = new Packet(rpc);
            toWrite(p);
            p.WriteLength();
            if (p.Length() > Packet.s_maxBufferSize)
            {
                Console.WriteLine($"RPC packet {(int)rpc} has exceeded the max length of a single packet");
                return;
            }
            foreach(var c in Server.clients)
            {
                if (c.Value != null)
                    c.Value.Send(p, toRead);
            }
        }
        public static void RpcSendExcept(ServerPackets rpc, int clientId, Action<Packet> toWrite, AsyncCallback? toRead = null)
        {
            Packet p = new Packet(rpc);
            toWrite(p);
            p.WriteLength();
            if (p.Length() > Packet.s_maxBufferSize)
            {
                Console.WriteLine($"RPC packet {(int)rpc} has exceeded the max length of a single packet");
                return;
            }
            foreach (var c in Server.clients)
            {
                if (c.Value != null && c.Key != clientId)
                    c.Value.Send(p, toRead);
            }
        }
        public static void RpcSendSome(ServerPackets rpc, List<int> clientIds, Action<Packet> toWrite, AsyncCallback? toRead = null)
        {
            Packet p = new Packet(rpc);
            toWrite(p);
            p.WriteLength();
            if (p.Length() > Packet.s_maxBufferSize)
            {
                Console.WriteLine($"RPC packet {(int)rpc} has exceeded the max length of a single packet");
                return;
            }
            foreach (var c in Server.clients)
            {
                if (c.Value != null && clientIds.Contains(c.Key))
                    c.Value.Send(p, toRead);
            }
        }
    }
}
