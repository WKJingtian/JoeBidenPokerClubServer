using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JoeBidenPokerClubServer
{
    class PokerCard
    {
        public enum Decors
        {
            spade = 0,
            heart = 1,
            diamond = 2,
            club = 3,
        }

        public bool notRevealed = false;
        // heart 0 is the big joker
        // spade 0 is the small joker
        public Decors decor;
        public int point;

        public PokerCard(Decors d, int p)
        {
            if (p < 0 || p > 13)
                throw new Exception("Wrong Poker Point");
            decor = d;
            point = p;
        }

        static public void WritePokerInfoToPacket(PokerCard pc, ref Packet pa)
        {
            if (pc.notRevealed)
                pa.Write(pc.notRevealed);
            else
            {
                pa.Write(pc.notRevealed);
                pa.Write((int)pc.decor);
                pa.Write(pc.point);
            }
        }
    }
}
