using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JoeBidenPokerClubServer
{
    public class ThreadManager
    {
        private static readonly List<Action> executeOnMainThread = new List<Action>();
        private static readonly List<Action> executeCopiedOnMainThread = new List<Action>();
        private static readonly Dictionary<Action, int> upcomingTaskList =
            new Dictionary<Action, int>();
        private static bool actionToExecuteOnMainThread = false;

        /// <summary>Sets an action to be executed on the main thread.</summary>
        /// <param name="_action">The action to be executed on the main thread.</param>
        public static void ExecuteOnMainThread(Action _action)
        {
            if (_action == null)
            {
                Console.WriteLine("No action to execute on main thread!");
                return;
            }
            lock (executeOnMainThread)
            {
                executeOnMainThread.Add(_action);
                actionToExecuteOnMainThread = true;
            }
        }
        public static void ExecuteAfterFrame(Action _action, int n = 1)
        {
            upcomingTaskList[_action] = n;
        }

        /// <summary>Executes all code meant to run on the main thread. NOTE: Call this ONLY from the main thread.</summary>
        public static void UpdateMain()
        {
            if (actionToExecuteOnMainThread)
            {
                executeCopiedOnMainThread.Clear();
                lock (executeOnMainThread)
                {
                    executeCopiedOnMainThread.AddRange(executeOnMainThread);
                    executeOnMainThread.Clear();
                    actionToExecuteOnMainThread = false;
                }

                for (int i = 0; i < executeCopiedOnMainThread.Count; i++)
                {
                    try
                    {
                        executeCopiedOnMainThread[i]();
                    }
                    catch (Exception e) { Console.WriteLine($"{e.Message}"); }
                }
            }

            foreach (var item in upcomingTaskList)
            {
                upcomingTaskList[item.Key]--;
                if (upcomingTaskList[item.Key] <= 0)
                {
                    var act = item.Key;
                    upcomingTaskList.Remove(item.Key);
                    ExecuteOnMainThread(act);
                }
            }
        }
    }
}
