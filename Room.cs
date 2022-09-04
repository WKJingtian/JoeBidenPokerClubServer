using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JoeBidenPokerClubServer
{
    class Room
    {
        int maxPlayerInRoom = 8;
        int maxObInRoom = 4;
        List<Client> players;
        List<Client> obs;
        GameFlowManager manager;
        float emptyTime = 0;
        string roomName = "pokerRoom";

        public Room()
        {
            players = new List<Client>();
            obs = new List<Client>();
            OnGameStart();
        }
        public Room(string name, int sb, int time, int rptc, int pLimit, int oLimit)
        {
            players = new List<Client>();
            obs = new List<Client>();
            roomName = name;
            maxPlayerInRoom = pLimit;
            maxObInRoom = oLimit;
            OnGameStart(sb, time, rptc, maxPlayerInRoom, maxObInRoom);
        }

        public bool Joinable(bool isOb = false)
        {
            if (isOb) return obs.Count < maxObInRoom;
            else return players.Count < maxPlayerInRoom && manager.GetActivePlayerCount(false, false) < maxPlayerInRoom;
        }

        public bool OnPlayerEnterRoom(Client c, int cashIn)
        {
            bool result = false;
            if (cashIn < manager.SmallBlind * 30)
            {
                return false;
            }
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
                if (c.playerAccountInfo.uid == id)
                    return true;
            }
            return false;
        }

        public void OnGameStart(int sb = 1, int time = 30, int rptc = 1, int maxPlayer = 8, int maxOb = 4)
        {
            RoomManager.RegisterGameRoom(this);
            manager = new GameFlowManager(sb, time, rptc, maxPlayer, maxOb);
        }

        public void OnGameEnd()
        {
            manager.ForceEnd();
            players.Clear();
            RoomManager.UnregisterGameRoom(this);
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
        public struct RoomInfo
        {
            public int roomID;
            public string name;
            public int maxPlayer;
            public int curPlayer;
            public int maxOb;
            public int curOb;
            public int sb;
            public int roundTime;
            public int roundPerTimeCard;
            public int roundPassed;
        }
        public RoomInfo ReportStat()
        {
            RoomInfo result;
            result.roomID = RoomManager.GetRoomIdx(this);
            result.name = roomName;
            result.maxPlayer = maxPlayerInRoom;
            result.curPlayer = players.Count;
            result.maxOb = maxObInRoom;
            result.curOb = obs.Count;
            result.sb = manager.SmallBlind;
            result.roundTime = manager.RoundTime;
            result.roundPerTimeCard = manager.Rptc;
            result.roundPassed = manager.Round;
            return result;
        }
        public void ForceSync()
        {
            manager.ForceSync();
        }
    }
}
