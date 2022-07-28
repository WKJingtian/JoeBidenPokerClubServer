using JoeBidenPokerClubServer;
using System.Threading;
namespace JoeBidenPokerClubServer
{
    class App
    {
        public const int s_serverFrameRate = 5;
        public const int s_msPerTick = 1000 / s_serverFrameRate;

        private static bool running = true;
        static void Main(string[] args)
        {
            Console.Title = "Joe Biden Poker Club";
            Console.WriteLine("Welcome to Joe Biden's Poker Club!");
            Server.Start(4242);
            var am = AccountManager.Inst;
            Thread main = new Thread(new ThreadStart(MainThread));
            main.Start();
        }
        static public void OnQuit()
        {
            Console.WriteLine("Application quit, recording account info now...");
            RoomManager.ShutDownAllRoomNow();
            AccountManager.Inst.ReRecordAll();
        }

        private static void MainThread()
        {
            DateTime loopTimer = DateTime.UtcNow;
            while(running)
            {
                while(loopTimer < DateTime.UtcNow)
                {
                    RoomManager.Update();
                    ThreadManager.UpdateMain();
                    loopTimer = loopTimer.AddMilliseconds(s_msPerTick);
                    if (loopTimer > DateTime.UtcNow)
                        Thread.Sleep(loopTimer - DateTime.UtcNow);
                }
            }
        }
    }
}