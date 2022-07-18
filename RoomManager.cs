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

        static public void RegisterGameRoom(Room r)
        {
            rooms.Add(r);
        }

        static public void Update()
        {
            foreach (Room room in rooms)
                room.Tick();
        }
    }
}
