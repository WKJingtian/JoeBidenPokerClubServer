using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JoeBidenPokerClubServer
{
    class RoomManager
    {
        static List<Room> rooms = new List<Room>();
        static public Room GetPlayerRoom(int id)
        {
            foreach (var r in rooms)
            {
                if (r.ifContains(id))
                    return r;
            }
            return null;
        }
        static public Room GetFreeRoom()
        {
            foreach (var r in rooms)
            {
                if (r.Joinable())
                    return r;
            }
            return new Room();
        }
        static public Room GetRoom(int roomIdx)
        {
            if (roomIdx >= 0 && roomIdx < rooms.Count &&
                rooms[roomIdx] != null &&
                rooms[roomIdx].Joinable())
                return rooms[roomIdx];
            return GetFreeRoom();
        }
        static public int GetRoomIdx(Room r)
        {
            return rooms.IndexOf(r);
        }

        static public void RegisterGameRoom(Room r)
        {
            rooms.Add(r);
        }

        static public void Update()
        {
            foreach (Room room in rooms)
                room.Tick();
        }

        static public void ShutDownAllRoomNow()
        {
            foreach (Room room in rooms)
                room.OnGameEnd();
        }
    }
}
