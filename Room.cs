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

        public void OnPlayerEnterRoom(Client c)
        {
            if (c.playerAccountInfo != null)
            {

            }
        }

        public void OnPlayerExitRoom(Client c)
        {
            if (c.playerAccountInfo != null)
            {

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
        }

        public void HandlePacket(Packet p, ClientPackets rpc)
        {
            switch (rpc)
            {
                //case ClientPackets.joinRoom:
                //    break;
                //case ClientPackets.quitRoom:
                //    break;
                default:
                    manager.HandlePacket(p, rpc);
                    break;
            }
        }
    }
}
