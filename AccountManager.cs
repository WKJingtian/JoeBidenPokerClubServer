using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JoeBidenPokerClubServer
{
    class AccountManager
    {
        private static AccountManager instance = null;
        public static AccountManager Inst
        {
            get
            {
                if (instance == null)
                    instance = new AccountManager();
                return instance;
            }
        }
        private readonly static string pathToAccountFile = ".\\AccountDatabase.txt";
        private readonly static int minUid = 10000000;
        private readonly static int maxUid = 99999999;
        private Random randomizer;
        public class AccountInfo
        {
            public string name;
            public string password;
            public string email;
            public string signiture;
            public int uid;
            public int cash;
            public int gameWin;
            public int gameLose;
            public int cashWin;
            public int cashLose;
        }
        public Dictionary<int, AccountInfo> accountInfo;
        public Dictionary<int, bool> ifOnline;
        protected AccountManager()
        {
            if (instance != null)
            {
                Console.WriteLine("ERROR: try to boost account manager twice!");
                return;
            }
            instance = this;
            accountInfo = new Dictionary<int, AccountInfo>();
            ifOnline = new Dictionary<int, bool>();
            if (System.IO.File.Exists(pathToAccountFile) == true)
            {
                var accountFile = System.IO.File.OpenText(pathToAccountFile);
                string line = accountFile.ReadLine();
                while (line != null)
                {
                    string[] infoSegs = line.Split(' ');
                    if (infoSegs.Length != 10)
                    {
                        Console.WriteLine($"WARN: user info {line} does not comply to the format");
                        line = accountFile.ReadLine();
                        continue;
                    }
                    try
                    {
                        AccountInfo info = new AccountInfo();
                        info.name = infoSegs[0];
                        info.password = infoSegs[1];
                        info.email = infoSegs[2];
                        info.signiture = infoSegs[3];
                        info.uid = Int32.Parse(infoSegs[4]);
                        info.cash = Int32.Parse(infoSegs[5]);
                        info.gameWin = Int32.Parse(infoSegs[6]);
                        info.gameLose = Int32.Parse(infoSegs[7]);
                        info.cashWin = Int32.Parse(infoSegs[8]);
                        info.cashLose = Int32.Parse(infoSegs[9]);
                        if (accountInfo.ContainsKey(info.uid))
                            Console.WriteLine($"repeat uid {info.uid} shared by {info.name} and {accountInfo[info.uid].name}");
                        else
                            accountInfo[info.uid] = info;
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine(e);
                        line = accountFile.ReadLine();
                        continue;
                    }
                    line = accountFile.ReadLine();
                }
                accountFile.Close();
            }
            else
            {
                Console.WriteLine("ERROR: Account Info Not Found!");
                return;
            }
            randomizer = new Random();
            foreach (var account in accountInfo)
                ifOnline[account.Key] = false;
        }
        private int GenerateNewUid()
        {
            int result = randomizer.Next(minUid, maxUid);
            while (accountInfo.ContainsKey(result))
            {
                result = randomizer.Next(minUid, maxUid);
            }
            return result;
        }
        public int Register(string name, string password, string email = "null", string signiture = "null")
        {
            int newUid = GenerateNewUid();
            //ThreadManager.ExecuteOnMainThread(() =>
            //{
                if (name.Length == 0 ||
                    password.Length == 0 ||
                    email.Length == 0 ||
                    signiture.Length == 0)
                {
                    Console.WriteLine($"ERROR: register input invalid");
                    return 0;
                }
                AccountInfo info = new AccountInfo();
                info.name = name.Replace(' ', '_');
                info.password = password.Replace(' ', '_');
                info.email = email.Replace(' ', '_');
                info.signiture = signiture.Replace(' ', '_');
                info.uid = newUid;
                info.cash = 0;
                info.gameWin = 0;
                info.gameLose = 0;
                info.cashWin = 0;
                info.cashLose = 0;
                accountInfo[info.uid] = info;

                if (System.IO.File.Exists(pathToAccountFile) == true)
                {
                    var accountFile = new StreamWriter(pathToAccountFile, true);
                    accountFile.WriteLine($"{info.name} {info.password} {info.email} {info.signiture} {info.uid} {info.cash} {info.gameWin} {info.gameLose} {info.cashWin} {info.cashLose}");
                    accountFile.Close();
                }
                ifOnline[newUid] = false;
            //});
            return accountInfo.ContainsKey(newUid) ? newUid : 0;
        }
        public bool Login(int uid, string password)
        {
            bool result = accountInfo.ContainsKey(uid) && 
                accountInfo[uid].password == password &&
                ifOnline.ContainsKey(uid) &&
                !ifOnline[uid];
            if (result) ifOnline[uid] = true;
            return result;
        }
        public string FindPassword(string email = "null")
        {
            if (email == "null")
            {
                Console.WriteLine("Caanot find account with no email attached to it");
                return "";
            }
            foreach (var info in accountInfo)
            {
                if (info.Value.email == email)
                    return info.Value.password;
            }
            Console.WriteLine($"Caanot find email {email}'s account");
            return "";
        }
        public void ReRecordAccountInfo(int uid)
        {
            ThreadManager.ExecuteOnMainThread(() =>
            {
                string tempFile = Path.GetTempFileName();
                using (var sr = new StreamReader(pathToAccountFile))
                using (var sw = new StreamWriter(tempFile))
                {
                    string line = sr.ReadLine();
                    while (line != null)
                    {
                        string[] infoSegs = line.Split(' ');
                        if (infoSegs.Length != 10)
                        {
                            line = sr.ReadLine();
                            continue;
                        }
                        try
                        {
                            int readUid = Int32.Parse(infoSegs[4]);
                            if (readUid == uid)
                            {
                                var info = accountInfo[uid];
                                sw.WriteLine($"{info.name} {info.password} {info.email} {info.signiture} {info.uid} {info.cash} {info.gameWin} {info.gameLose} {info.cashWin} {info.cashLose}");
                            }
                            else sw.WriteLine(line);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            line = sr.ReadLine();
                            continue;
                        }
                        line = sr.ReadLine();
                    }
                    File.Delete(pathToAccountFile);
                    File.Move(tempFile, pathToAccountFile);
                }
            });
        }
        public void ReRecordAll()
        {
            foreach (var a in accountInfo)
            {
                ReRecordAccountInfo(a.Key);
            }
        }
        public void Disconnect(int uid)
        {
            ifOnline[uid] = false;
        }
        public AccountInfo GetAccount(int uid)
        {
            return accountInfo.ContainsKey(uid) ? accountInfo[uid] : null;
        }
        public bool IfOnline(int uid)
        {
            return ifOnline.ContainsKey(uid) ? ifOnline[uid] : false;
        }
    }
}
