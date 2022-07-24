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

        public PokerCard(Decors d = Decors.heart, int p = 0)
        {
            if (p < 0 || p > 13)
                throw new Exception("Wrong Poker Point");
            decor = d;
            point = p;
        }
        static public bool operator <(PokerCard a, PokerCard b)
        {
            if (a.point == b.point &&
                a.decor == b.decor)
                return false;
            else if (a.point == b.point)
            {
                if (a.point == 0 && a.decor == Decors.heart)
                    return false;
                else if (a.point == 0 && a.decor == Decors.spade)
                    return true;
                else return false;
            }
            else if (a.point == 1)
                return false;
            else if (b.point == 1)
                return true;
            else
                return a.point < b.point;
        }
        static public bool operator>(PokerCard a, PokerCard b)
        {
            if (a.point == b.point &&
                a.decor == b.decor)
                return false;
            else if (a.point == b.point)
            {
                if (a.point == 0 && a.decor == Decors.heart)
                    return true;
                else if (a.point == 0 && a.decor == Decors.spade)
                    return false;
                else return false;
            }
            else if (a.point == 1)
                return true;
            else if (b.point == 1)
                return false;
            else
                return a.point > b.point;
        }
        static public bool operator ==(PokerCard a, PokerCard b)
        {
            return (a == null && b == null) ||
                (a != null && b != null &&
                a.point == b.point &&
                a.decor == b.decor);
        }
        static public bool operator !=(PokerCard a, PokerCard b)
        {
            return (a == null && b != null) ||
                (a != null && b == null) ||
                (a != null && b != null &&
                (a.point != b.point ||
                a.decor != b.decor));
        }
        static public void WriteSelfToAPacket(PokerCard c, ref Packet p)
        {
            if (c.notRevealed)
                p.Write(false);
            else
            {
                p.Write(true);
                p.Write((int)c.decor);
                p.Write(c.point);
            }
        }
    }
}
