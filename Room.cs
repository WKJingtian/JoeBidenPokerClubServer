using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JoeBidenPokerClubServer
{
    class Room
    {
        static readonly int s_maxPlayerInRoom = 8;
        static readonly int s_maxObInRoom = 4;
        List<Client> players;
        List<Client> obs;
        GameFlowManager manager;
        float emptyTime = 0;

        public Room()
        {
            players = new List<Client>();
            obs = new List<Client>();
            manager = new GameFlowManager(s_maxPlayerInRoom);
            RoomManager.RegisterGameRoom(this);
        }

        public bool Joinable(bool isOb = false)
        {
            if (isOb) return obs.Count < s_maxObInRoom;
            else return players.Count < s_maxPlayerInRoom && manager.GetActivePlayerCount(false, false) < s_maxPlayerInRoom;
        }

        public bool OnPlayerEnterRoom(Client c, int cashIn)
        {
            bool result = false;
            if (c.playerAccountInfo != null &&
                !players.Contains(c))
            {
                result = manager.AddPlayerToGame(c.playerAccountInfo.uid, cashIn);
                if (result)
                {
                    players.Add(c);
                    c.playerAccountInfo.cash -= cashIn;
                }
            }
            return result;
        }

        public void OnPlayerExitRoom(Client c)
        {
            if (c.playerAccountInfo != null &&
                players.Contains(c))
            {
                manager.RemovePlayer(c.playerAccountInfo.uid);
                players.Remove(c);
            }
        }
        public bool ifContains(int id)
        {
            foreach (Client c in players)
            {
                if (c.id == id)
                    return true;
            }
            return false;
        }

        public void OnGameStart()
        {
            manager = new GameFlowManager(s_maxPlayerInRoom);
        }

        public void OnGameEnd()
        {

        }

        public void Tick()
        {
            manager.Tick();
            if (players.Count == 0)
            {
                emptyTime += App.s_msPerTick;
                if (emptyTime > 30000)
                    OnGameEnd();
            }
            else emptyTime = 0;
        }

        public void HandlePacket(Packet p, ClientPackets rpc)
        {
            switch (rpc)
            {
                default:
                    manager.HandlePacket(p, rpc);
                    break;
            }
        }
    }
}
