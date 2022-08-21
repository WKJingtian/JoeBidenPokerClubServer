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
        static List<Room> toRemove = new List<Room>();
        static List<Room> toAdd = new List<Room>();
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
            toAdd.Add(r);
        }
        static public void UnregisterGameRoom(Room r)
        {
            toRemove.Add(r);
        }

        static public void Update()
        {
            foreach (var r in toAdd)
                rooms.Add(r);
            foreach (var r in toRemove)
                rooms.Remove(r);
            toAdd.Clear();
            toRemove.Clear();
            foreach (Room room in rooms)
                room.Tick();
        }

        static public void ShutDownAllRoomNow()
        {
            foreach (Room room in rooms)
                room.OnGameEnd();
        }

        static public List<Room>  Rooms => rooms;
    }
}
