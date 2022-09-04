using System;
using System.Collections;
using System.Collections.Generic;

namespace JoeBidenPokerClubServer
{
    class GameFlowManager
    {
        public class PlayerInGameStat
        {
            public int uid;
            public int moneyInPocket;
            public int moneyInPot;
            public bool hasBidThisRound;
            public bool hasFolded;
            public bool ifAllIn;
            public bool hasQuited;
            public List<PokerCard> hand = new List<PokerCard>();
            public int timeCard;
        }

        private float pauseBetweenRounds = 15;
        private float roundTime = 30;
        public int RoundTime => (int)roundTime;
        private float timer = 0;
        private int roundNum = 0;
        public int Round => roundNum;
        private int roundPerTimeCard = 1;
        public int Rptc => roundPerTimeCard;
        private int smallBlindMoneyNum = 1;
        public int SmallBlind => smallBlindMoneyNum;
        private int currentActivePlayer = 0;
        private int currentSmallBlind = 0;
        private bool gamePaused = true;

        public enum roundState
        {
            bidRound0 = 0,
            bidRound1 = 1,
            bidRound2 = 2,
            bidRound3 = 3,
            roundFinished = 4,
        }
        roundState curState;

        List<PokerCard> deck;
        List<PokerCard> flopTurnRiver;
        List<PlayerInGameStat> players;

        public GameFlowManager(int sb, int time, int rptc, int maxPlayer, int maxOb)
        {
            smallBlindMoneyNum = sb;
            roundTime = time;
            roundPerTimeCard = rptc;
            players = new List<PlayerInGameStat>();
            players.Capacity = maxPlayer;
            deck = new List<PokerCard>();
            flopTurnRiver = new List<PokerCard>();
        }
        public int GetPlayerIndexById(int id)
        {
            for(int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && players[i].uid == id)
                    return i;
            }
            return -1;
        }
        public int GetPlayerIdByIndex(int idx)
        {
            if (idx >= 0 && idx < players.Count &&
                players[idx] != null)
                return players[idx].uid;
            return -1;
        }
        public PlayerInGameStat GetPlayerInfoById(int id)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && players[i].uid == id)
                    return players[i];
            }
            return null;
        }
        public int HighestBid()
        {
            int max = 0;
            foreach (var p in players)
            {
                if (p.moneyInPot > max)
                    max = p.moneyInPot;
            }
            return max;
        }
        private bool IfBidRoundComplete()
        {
            foreach (var p in players)
            {
                if (p != null)
                    Console.WriteLine($"checking {p.uid} stat: {p.hasFolded} - {p.hasBidThisRound} - {p.ifAllIn} - {p.moneyInPot} - {HighestBid()}");
                if (p != null &&
                    !p.hasFolded && !p.ifAllIn &&
                    (!p.hasBidThisRound || p.moneyInPot != HighestBid()))
                    return false;
            }
            return true;
        }
        public int GetNextPlayerIdx(int fromIdx, bool ignoreFolded = true)
        {
            int result = -1;
            if (fromIdx < 0 || fromIdx >= players.Count)
                return result;
            result = fromIdx + 1;
            while (result < players.Count)
            {
                if (players[result] != null &&
                    (!ignoreFolded || !players[result].hasFolded))
                    return result;
                result++;
            }
            result = 0;
            while (result < players.Count)
            {
                if (players[result] != null &&
                    (!ignoreFolded || !players[result].hasFolded))
                    return result;
                result++;
            }
            return -1;
        }
        public int GetActivePlayerCount(bool ignoreFolded = true, bool ignoreQuitted = true)
        {
            int result = 0;
            foreach (var p in players)
            {
                if (p != null &&
                    (!ignoreQuitted || !p.hasQuited) &&
                    (!ignoreFolded || !p.hasFolded))
                    result++;
            }
            return result;
        }
        public bool AddPlayerToGame(int id, int buyIn)
        {
            bool added = false;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == null)
                {
                    players[i] = new PlayerInGameStat();
                    players[i].uid = id;
                    players[i].moneyInPocket = buyIn;
                    added = true;
                }
            }
            if(!added)
            {
                if (players.Count >= players.Capacity)
                    return false;
                PlayerInGameStat newPlayer = new PlayerInGameStat();
                newPlayer.uid = id;
                newPlayer.moneyInPocket = buyIn;
                players.Add(newPlayer);
                added = true;
            }
            SyncPlayers();
            return added;
        }
        public void RemovePlayer(int id)
        {
            var stat = GetPlayerInfoById(id);
            if (stat != null)
            {
                stat.hasQuited = true;
                stat.hasFolded = true;
            }
            SyncPlayers();
        }
        public void Tick()
        {
            timer += (float)App.s_msPerTick / 1000.0f;
            if (gamePaused)
            {
                bool playerNeedsUpdate = false;
                for (int idx = players.Count - 1; idx >= 0; idx--)
                {
                    if (players[idx] != null && players[idx].hasQuited)
                    {
                        AccountManager.Inst.accountInfo[players[idx].uid].cash += players[idx].moneyInPocket;
                        players[idx] = null;
                        playerNeedsUpdate = true;
                    }
                }
                if (playerNeedsUpdate) SyncPlayers();
                if (timer > pauseBetweenRounds &&
                    GetActivePlayerCount(false) > 1)
                {
                    gamePaused = false;
                    StartNewRound();
                    return;
                }
            }
            else if (timer > roundTime)
            {
                timer = 0;
                if (currentActivePlayer < 0 ||
                    currentActivePlayer >= players.Count ||
                    players[currentActivePlayer] == null)
                {
                    Console.WriteLine($"GAME ERROR: {currentActivePlayer} is not a valid player rank");
                }
                Console.Write($"Force player {currentActivePlayer} to act:    ");
                if (PlayerCheckOrFold(players[currentActivePlayer].uid))
                    Console.WriteLine("check");
                else
                    Console.WriteLine("fold");
                if (GetActivePlayerCount() <= 1)
                {
                    RoundEnd();
                }
                else
                {
                    if (IfBidRoundComplete())
                    {
                        Console.WriteLine("advance to a new round");
                        NewBidRound();
                    }
                    else
                    {
                        Console.WriteLine("asking next player to act");
                        currentActivePlayer = GetNextPlayerIdx(currentActivePlayer);
                        RequestAction(players[currentActivePlayer].uid);
                    }
                }
            }
        }
        public bool PlayerBid(int id, int amount)
        {
            var player = GetPlayerInfoById(id);
            if (player == null) return false;
            player.ifAllIn = player.moneyInPocket <= amount;
            int bidAmount = (int)MathF.Min(amount, player.moneyInPocket);
            player.moneyInPocket -= bidAmount;
            player.moneyInPot += bidAmount;
            player.hasBidThisRound = true;
            return player.ifAllIn || player.moneyInPot >= HighestBid();
        }
        public bool PlayerCheckOrFold(int id)
        {
            var player = GetPlayerInfoById(id);
            if (player == null) return false;
            if (player.moneyInPot < HighestBid())
            {
                player.hasFolded = true;
                return false;
            }
            player.hasBidThisRound = true;
            return true;
        }
        public void StartNewRound()
        {
            Console.WriteLine("new game round started");
            deck.Clear();
            flopTurnRiver.Clear();
            roundNum++;
            if (GetActivePlayerCount(false) <= 1)
            {
                gamePaused = true;
                // sync room stat
                return;
            }
            else
            {
                SyncPlayers();
                SyncStat();
            }
            foreach (var player in players)
            {
                player.hand.Clear();
                player.moneyInPot = 0;
                player.hasFolded = false;
                player.ifAllIn = false;
                if (roundNum % roundPerTimeCard == 0) player.timeCard++;
                SyncPlayersHand(player.uid, true);
            }
            for (int i = 1; i <= 13; i++)
            {
                deck.Add(new PokerCard(PokerCard.Decors.spade, i));
                deck.Add(new PokerCard(PokerCard.Decors.heart, i));
                deck.Add(new PokerCard(PokerCard.Decors.diamond, i));
                deck.Add(new PokerCard(PokerCard.Decors.club, i));
            }
            currentSmallBlind = GetNextPlayerIdx(currentSmallBlind);
            foreach (var player in players)
            {
                int randIdx = new Random().Next(0, deck.Count);
                PokerCard c1 = deck[randIdx];
                player.hand.Add(c1);
                deck.RemoveAt(randIdx);
                randIdx = new Random().Next(0, deck.Count);
                PokerCard c2 = deck[randIdx];
                player.hand.Add(c2);
                deck.RemoveAt(randIdx);
                SyncPlayersHand(player.uid);
            }
            curState = roundState.bidRound0;
            SyncPlayers();
            SyncStat();
            SyncFlopTurnRiver();
            NewBidRound();
        }
        public void NewBidRound()
        {
            foreach (var p in players)
            {
                p.hasBidThisRound = false;
            }
            timer = 0;
            int randIdx = 0;
            switch (curState)
            {
                case roundState.bidRound0:
                    Console.WriteLine("round 0");
                    if (!PlayerBid(GetPlayerIdByIndex(currentSmallBlind), smallBlindMoneyNum))
                        Console.WriteLine("small blind fail to place a bid");
                    if (!PlayerBid(GetPlayerIdByIndex(GetNextPlayerIdx(currentSmallBlind)),
                        2 * smallBlindMoneyNum))
                        Console.WriteLine("big blind fail to place a bid");
                    currentActivePlayer = GetNextPlayerIdx(GetNextPlayerIdx(currentSmallBlind));
                    RequestAction(players[currentActivePlayer].uid);
                    SyncFlopTurnRiver();
                    SyncPlayers();
                    SyncStat();
                    curState = roundState.bidRound1;
                    break;

                case roundState.bidRound1:
                    Console.WriteLine("round 1");
                    randIdx = new Random().Next(0, deck.Count);
                    flopTurnRiver.Add(deck[randIdx]);
                    deck.RemoveAt(randIdx);
                    randIdx = new Random().Next(0, deck.Count);
                    flopTurnRiver.Add(deck[randIdx]);
                    deck.RemoveAt(randIdx);
                    randIdx = new Random().Next(0, deck.Count);
                    flopTurnRiver.Add(deck[randIdx]);
                    deck.RemoveAt(randIdx);
                    currentActivePlayer = GetNextPlayerIdx(GetNextPlayerIdx(currentSmallBlind));
                    RequestAction(players[currentActivePlayer].uid);
                    SyncFlopTurnRiver();
                    SyncPlayers();
                    SyncStat();
                    curState = roundState.bidRound2;
                    break;

                case roundState.bidRound2:
                    Console.WriteLine("round 2");
                    randIdx = new Random().Next(0, deck.Count);
                    flopTurnRiver.Add(deck[randIdx]);
                    deck.RemoveAt(randIdx);
                    currentActivePlayer = GetNextPlayerIdx(GetNextPlayerIdx(currentSmallBlind));
                    RequestAction(players[currentActivePlayer].uid);
                    SyncFlopTurnRiver();
                    SyncPlayers();
                    SyncStat();
                    curState = roundState.bidRound3;
                    break;

                case roundState.bidRound3:
                    Console.WriteLine("round 3");
                    randIdx = new Random().Next(0, deck.Count);
                    flopTurnRiver.Add(deck[randIdx]);
                    deck.RemoveAt(randIdx);
                    currentActivePlayer = GetNextPlayerIdx(GetNextPlayerIdx(currentSmallBlind));
                    RequestAction(players[currentActivePlayer].uid);
                    SyncFlopTurnRiver();
                    SyncPlayers();
                    SyncStat();
                    curState = roundState.roundFinished;
                    break;

                case roundState.roundFinished:
                    Console.WriteLine("round final");
                    // divide the pool
                    RoundEnd();
                    break;

                default:
                    Console.WriteLine("round error");
                    break;

            }
        }
        public void RoundEnd()
        {
            // calculate the result
            Console.WriteLine("GameRound end, now start to calculate result");
            gamePaused = true;
            timer = 0;
            currentActivePlayer = -1;

            Dictionary<int, int[]> playerRooundScore = new Dictionary<int, int[]>();
            Dictionary<int, List<PokerCard>> playerResultHand = new Dictionary<int, List<PokerCard>>();
            foreach (var p in players)
            {
                if (p != null && !p.hasFolded &&
                    !p.hasQuited && p.moneyInPot > 0)
                {
                    List<PokerCard> temp = new List<PokerCard>();
                    List<PokerCard> resultHand = new List<PokerCard>();
                    foreach (var c in p.hand)
                        temp.Add(c);
                    foreach (var c in flopTurnRiver)
                        temp.Add(c);
                    playerRooundScore[p.uid] = TellScore(temp, ref resultHand);
                    playerResultHand[p.uid] = resultHand;
                }
                else ;//tell them they are losers! 
            }
            if (playerRooundScore.Count == 1)
            {
                var winnerInfo = GetPlayerInfoById(playerRooundScore.Keys.ToList<int>()[0]);
                int winnerBid = winnerInfo.moneyInPot;
                foreach (var p in players)
                {
                    if ( p != null && p.uid != winnerInfo.uid)
                    {
                        int loseAmount = (int)MathF.Min(winnerBid, p.moneyInPot);
                        p.moneyInPot -= loseAmount;
                        winnerInfo.moneyInPot += loseAmount;
                    }
                }
            }
            else
            {
                while (playerRooundScore.Count > 0)
                {
                    var winnerScore = new int[] { -1, -1, -1, -1, -1, -1 };
                    List<int> winnerList = new List<int>();
                    foreach (var score in playerRooundScore)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            if (winnerScore[i] < (score.Value)[i])
                            {
                                winnerScore = score.Value;
                                break;
                            }
                            else if (winnerScore[i] > (score.Value)[i])
                            {
                                break;
                            }
                        }
                    }

                    foreach (var score in playerRooundScore)
                    {
                        if (score.Value[0] == winnerScore[0] && score.Value[1] == winnerScore[1] &&
                            score.Value[2] == winnerScore[2] && score.Value[3] == winnerScore[3] &&
                            score.Value[4] == winnerScore[4] && score.Value[5] == winnerScore[5])
                            winnerList.Add(score.Key);
                    }

                    foreach (int id in playerRooundScore.Keys)
                    {
                        SyncPlayersHand(id, true);
                        Congradulation(id, winnerList.Contains(id));
                    }

                    int winnerMaxBet = 0;
                    int winnerTotalBet = 0;
                    Dictionary<int, int> winnerBets = new Dictionary<int, int>();
                    foreach (int id in winnerList)
                    {
                        PlayerInGameStat stat = GetPlayerInfoById(id);
                        if (stat.moneyInPot > winnerMaxBet)
                            winnerMaxBet = stat.moneyInPot;
                        winnerBets[id] = stat.moneyInPot;
                        winnerTotalBet += stat.moneyInPot;
                    }
                    int totalPoolMoney = 0;
                    foreach (var p in players)
                    {
                        int moneyLose = (int)MathF.Min(winnerMaxBet, p.moneyInPot);
                        p.moneyInPot -= moneyLose;
                        totalPoolMoney += moneyLose;
                    }
                    foreach (int id in winnerList)
                    {
                        PlayerInGameStat stat = GetPlayerInfoById(id);
                        stat.moneyInPocket += totalPoolMoney * winnerBets[id] / winnerTotalBet;
                    }
                    foreach (var p in players)
                    {
                        if (p != null && playerRooundScore.ContainsKey(p.uid) &&
                            p.moneyInPot == 0)
                        {
                            playerRooundScore.Remove(p.uid);
                            playerResultHand.Remove(p.uid);
                        }
                    }
                }
            }
            foreach (var p in players)
            {
                if (p != null)
                {
                    p.moneyInPocket += p.moneyInPot;
                    p.moneyInPot = 0;
                }
            }
            Console.WriteLine("Result calculation finished");
            SyncPlayers();
        }
        public void HandlePacket(Packet p, ClientPackets rpc)
        {
            int id;
            switch (rpc)
            {
                case ClientPackets.bid:
                    id = p.ReadInt();
                    int bid = p.ReadInt();
                    if (id != GetPlayerIdByIndex(currentActivePlayer))
                        break;
                    timer = 0;
                    bool bidSuccess = PlayerBid(id, bid);
                    if (!bidSuccess)
                    {
                        RequestAction(id);
                    }
                    else
                    {
                        if (IfBidRoundComplete())
                        {
                            NewBidRound();
                        }
                        else
                        {
                            currentActivePlayer = GetNextPlayerIdx(currentActivePlayer);
                            RequestAction(players[currentActivePlayer].uid);
                        }
                    }
                    SyncPlayers();
                    ThreadManager.ExecuteOnMainThread(() => {
                        ServerSend.RpcSend(ServerPackets.bidCallback, Server.GetClientRankByUid(id), (Packet returnP) =>
                        {
                            returnP.Write(bidSuccess);
                        });
                    });
                    break;
                case ClientPackets.fold:
                    id = p.ReadInt();
                    if (id != GetPlayerIdByIndex(currentActivePlayer))
                        break;
                    timer = 0;
                    bool checkSuccess = PlayerCheckOrFold(id);
                    if (GetActivePlayerCount() <= 1)
                    {
                        RoundEnd();
                    }
                    else
                    {
                        if (IfBidRoundComplete())
                        {
                            NewBidRound();
                        }
                        else
                        {
                            currentActivePlayer = GetNextPlayerIdx(currentActivePlayer);
                            RequestAction(players[currentActivePlayer].uid);
                        }
                    }
                    SyncPlayers();
                    ThreadManager.ExecuteOnMainThread(() => {
                        ServerSend.RpcSend(ServerPackets.foldCallback, Server.GetClientRankByUid(id), (Packet returnP) =>
                        {
                            returnP.Write(checkSuccess);
                        });
                    });
                    break;
                case ClientPackets.useTimeCard:
                    id = p.ReadInt();
                    bool useSuccess = false;
                    if (id == GetPlayerIdByIndex(currentActivePlayer) &&
                        GetPlayerInfoById(id).timeCard > 0)
                    {
                        GetPlayerInfoById(id).timeCard--;
                        timer -= roundTime;
                        useSuccess = true;
                    }
                    SyncPlayers();
                    SyncStat();
                    ThreadManager.ExecuteOnMainThread(() => {
                        ServerSend.RpcSend(ServerPackets.useTimeCardCallback, Server.GetClientRankByUid(id), (Packet returnP) =>
                        {
                            returnP.Write(useSuccess);
                        });
                    });
                    break;
                default:
                    break;
            }
        }
        private int[] TellScore(List<PokerCard> cards, ref List<PokerCard>  resultHand)
        {
            var result = new int[] { 0, 0, 0, 0, 0, 0 };
            if (IsStraightFlash(cards, ref resultHand))
                result[0] = 100;
            else if (IsFlash(cards, ref resultHand))
                result[0] = 50;
            else if (IsStraight(cards, ref resultHand))
                result[0] = 40;
            else
            {
                BruteForceDoSort(cards, ref resultHand);
                if (resultHand.Count < 5)
                    result[0] = 0;
                else if (resultHand[0].point == resultHand[1].point &&
                    resultHand[0].point == resultHand[2].point &&
                    resultHand[0].point == resultHand[3].point)
                    result[0] = 90;
                else if (resultHand[0].point == resultHand[1].point &&
                    resultHand[0].point == resultHand[2].point &&
                    resultHand[3].point == resultHand[4].point)
                    result[0] = 80;
                else if (resultHand[0].point == resultHand[1].point &&
                    resultHand[0].point == resultHand[2].point)
                    result[0] = 30;
                else if (resultHand[0].point == resultHand[1].point &&
                    resultHand[2].point == resultHand[3].point)
                    result[0] = 20;
                else if (resultHand[0].point == resultHand[1].point)
                    result[0] = 10;
                else
                    result[0] = 0;
            }
            for (int i = 0; i < resultHand.Count; i++)
            {
                if (i > 5)
                    throw new Exception("Tell Score: out of index");
                if (resultHand[i].point == 1)
                    result[i + 1] = 100;
                else
                    result[i + 1] = resultHand[i].point;
            }
            return result;
        }
        private bool IsStraight(List<PokerCard> cards, ref List<PokerCard> finalHand)
        {
            bool result = false;
            PokerCard max = null;
            foreach (var c in cards)
            {
                if (c.point == 1 &&
                    HasPoint(cards, 13) > 0 &&
                    HasPoint(cards, 12) > 0 &&
                    HasPoint(cards, 11) > 0 &&
                    HasPoint(cards, 10) > 0)
                {
                    if (max == null || max < c)
                    {
                        max = c;
                        finalHand.Clear();
                        finalHand.Add(c);
                        finalHand.Add(GetPoint(cards, 13));
                        finalHand.Add(GetPoint(cards, 12));
                        finalHand.Add(GetPoint(cards, 11));
                        finalHand.Add(GetPoint(cards, 10));
                    }
                    result = true;
                }
                else if (HasPoint(cards, c.point + 1) > 0 &&
                    HasPoint(cards, c.point + 2) > 0 &&
                    HasPoint(cards, c.point + 3) > 0 &&
                    HasPoint(cards, c.point + 4) > 0)
                {
                    if (max == null || max < c)
                    {
                        max = c;
                        finalHand.Clear();
                        if (c.point == 1) finalHand.Add(c);
                        finalHand.Add(GetPoint(cards, c.point + 4));
                        finalHand.Add(GetPoint(cards, c.point + 3));
                        finalHand.Add(GetPoint(cards, c.point + 2));
                        finalHand.Add(GetPoint(cards, c.point + 1));
                        if (c.point != 1) finalHand.Add(c);
                    }
                    result = true;
                }
            }
            return result;
        }
        private bool IsStraightFlash(List<PokerCard> cards, ref List<PokerCard> finalHand)
        {
            bool result = false;
            PokerCard max = null;
            foreach (var c in cards)
            {
                if (c.point == 1 &&
                    HasPointDecor(cards, 13, c.decor) > 0 &&
                    HasPointDecor(cards, 12, c.decor) > 0 &&
                    HasPointDecor(cards, 11, c.decor) > 0 &&
                    HasPointDecor(cards, 10, c.decor) > 0)
                {
                    if (max == null || max < c)
                    {
                        max = c;
                        finalHand.Clear();
                        finalHand.Add(c);
                        finalHand.Add(GetPointDecor(cards, 13, c.decor));
                        finalHand.Add(GetPointDecor(cards, 12, c.decor));
                        finalHand.Add(GetPointDecor(cards, 11, c.decor));
                        finalHand.Add(GetPointDecor(cards, 10, c.decor));
                    }
                    result = true;
                }
                else if (HasPointDecor(cards, c.point + 1, c.decor) > 0 &&
                    HasPointDecor(cards, c.point + 2, c.decor) > 0 &&
                    HasPointDecor(cards, c.point + 3, c.decor) > 0 &&
                    HasPointDecor(cards, c.point + 4, c.decor) > 0)
                {
                    if (max == null || max < c)
                    {
                        max = c;
                        finalHand.Clear();
                        if (c.point == 1) finalHand.Add(c);
                        finalHand.Add(GetPointDecor(cards, c.point + 4, c.decor));
                        finalHand.Add(GetPointDecor(cards, c.point + 3, c.decor));
                        finalHand.Add(GetPointDecor(cards, c.point + 2, c.decor));
                        finalHand.Add(GetPointDecor(cards, c.point + 1, c.decor));
                        if (c.point != 1) finalHand.Add(c);
                    }
                    result = true;
                }
            }
            return result;
        }
        private bool IsFlash(List<PokerCard> cards, ref List<PokerCard> finalHand)
        {
            bool result = false;
            foreach (var c in cards)
            {
                if (HasDecor(cards, c.decor) > 4)
                {
                    finalHand.Clear();
                    if (GetPointDecor(cards, 1, c.decor) != null)
                        finalHand.Add(GetPointDecor(cards, 1, c.decor));
                    int p = 13;
                    while (finalHand.Count < 5 && p >= 0)
                    {
                        if (GetPointDecor(cards, p, c.decor) != null)
                            finalHand.Add(GetPointDecor(cards, 1, c.decor));
                        p--;
                    }
                    result = true;
                }
            }
            return result;
        }
        private void BruteForceDoSort(List<PokerCard> cards, ref List<PokerCard> finalHand)
        {
            int lookingFor = 5 - finalHand.Count;
            PokerCard temp = null;
            for (int ii = lookingFor; ii > 0; ii--)
            {
                if (HasPoint(cards, 1) == ii)
                {
                    temp = GetPointDecor(cards, 1, PokerCard.Decors.heart);
                    if (temp != null)
                    {
                        cards.Remove(temp);
                        finalHand.Add(temp);
                        if (finalHand.Count == 5) return;
                    }
                    temp = GetPointDecor(cards, 1, PokerCard.Decors.spade);
                    if (temp != null)
                    {
                        cards.Remove(temp);
                        finalHand.Add(temp);
                        if (finalHand.Count == 5) return;
                    }
                    temp = GetPointDecor(cards, 1, PokerCard.Decors.diamond);
                    if (temp != null)
                    {
                        cards.Remove(temp);
                        finalHand.Add(temp);
                        if (finalHand.Count == 5) return;
                    }
                    temp = GetPointDecor(cards, 1, PokerCard.Decors.club);
                    if (temp != null)
                    {
                        cards.Remove(temp);
                        finalHand.Add(temp);
                        if (finalHand.Count == 5) return;
                    }
                    BruteForceDoSort(cards, ref finalHand);
                    return;
                }
                for (int i = 13; i > 1; i--)
                {
                    if (HasPoint(cards, i) == ii)
                    {
                        temp = GetPointDecor(cards, i, PokerCard.Decors.heart);
                        if (temp != null)
                        {
                            cards.Remove(temp);
                            finalHand.Add(temp);
                            if (finalHand.Count == 5) return;
                        }
                        temp = GetPointDecor(cards, i, PokerCard.Decors.spade);
                        if (temp != null)
                        {
                            cards.Remove(temp);
                            finalHand.Add(temp);
                            if (finalHand.Count == 5) return;
                        }
                        temp = GetPointDecor(cards, i, PokerCard.Decors.diamond);
                        if (temp != null)
                        {
                            cards.Remove(temp);
                            finalHand.Add(temp);
                            if (finalHand.Count == 5) return;
                        }
                        temp = GetPointDecor(cards, i, PokerCard.Decors.club);
                        if (temp != null)
                        {
                            cards.Remove(temp);
                            finalHand.Add(temp);
                            if (finalHand.Count == 5) return;
                        }
                        BruteForceDoSort(cards, ref finalHand);
                        return;
                    }
                }
            }
        }
        private int HasPoint(List<PokerCard> cards, int targetP)
        {
            int result = 0;
            foreach(var c in cards)
            {
                if (c.point == targetP)
                    result++;
            }
            return result;
        }
        private int HasDecor(List<PokerCard> cards, PokerCard.Decors targetD)
        {
            int result = 0;
            foreach (var c in cards)
            {
                if (c.decor == targetD)
                    result++;
            }
            return result;
        }
        private int HasPointDecor(List<PokerCard> cards, int targetP, PokerCard.Decors targetD)
        {
            int result = 0;
            foreach (var c in cards)
            {
                if (c.decor == targetD &&
                    c.point == targetP)
                    result++;
            }
            return result;
        }
        private PokerCard GetPoint(List<PokerCard> cards, int targetP)
        {
            foreach (var c in cards)
            {
                if (c.point == targetP)
                    return c;
            }
            return null;
        }
        private PokerCard GetPointDecor(List<PokerCard> cards, int targetP, PokerCard.Decors targetD)
        {
            foreach (var c in cards)
            {
                if (c.decor == targetD &&
                    c.point == targetP)
                    return c;
            }
            return null;
        }
        private void CallEachPlayer(Action<int> action, bool ignoreQuitted = true)
        {
            foreach (var p in players)
            {
                if (p != null &&
                    (!p.hasQuited || !ignoreQuitted))
                {
                    action(Server.GetClientRankByUid(p.uid));
                }
            }
        }
        private void SyncStat()
        {
            CallEachPlayer((int clientRank)=>
            {
                ServerSend.RpcSend(ServerPackets.syncRoomStat, clientRank, (Packet p) =>
                {
                    p.Write(gamePaused ? pauseBetweenRounds - timer : roundTime - timer);
                    p.Write(roundNum);
                    p.Write(roundPerTimeCard);
                    p.Write(smallBlindMoneyNum);
                    p.Write(currentActivePlayer);
                    p.Write(currentSmallBlind);
                });
            });
        }
        private void SyncPlayers()
        {
            CallEachPlayer((int clientRank) =>
            {
                ServerSend.RpcSend(ServerPackets.syncPlayerStat, clientRank, (Packet p) =>
                {
                    for(int i = 0; i < players.Capacity; i++)
                    {
                        if (i >= players.Count || players[i] == null)
                        {
                            p.Write(false);
                            continue;
                        }
                        p.Write(true);
                        p.Write(players[i].uid);
                        if (AccountManager.Inst.accountInfo.ContainsKey(players[i].uid) &&
                            AccountManager.Inst.accountInfo[players[i].uid] != null)
                            p.Write(AccountManager.Inst.accountInfo[players[i].uid].name);
                        else
                            p.Write("Anonymous player");
                        p.Write(players[i].moneyInPocket);
                        p.Write(players[i].moneyInPot);
                        p.Write(players[i].hasBidThisRound);
                        p.Write(players[i].hasFolded);
                        p.Write(players[i].ifAllIn);
                        p.Write(players[i].hasQuited);
                        p.Write(players[i].timeCard);
                    }
                });
            });
        }
        private void SyncPlayersHand(int uid, bool toEveryone = false)
        {
            if (toEveryone)
            {
                CallEachPlayer((int clientRank) =>
                {
                    ServerSend.RpcSend(ServerPackets.syncPlayerHand, clientRank, (Packet p) =>
                    {
                        p.Write(uid);
                        int i = 0;
                        foreach (var c in GetPlayerInfoById(uid).hand)
                        {
                            PokerCard.WriteSelfToAPacket(c, ref p);
                            i++;
                        }
                        while (i < 2)
                        {
                            PokerCard temp = new PokerCard();
                            temp.notRevealed = true;
                            PokerCard.WriteSelfToAPacket(temp, ref p);
                            i++;
                        }
                    });
                });
            }
            else
            {
                ServerSend.RpcSend(ServerPackets.syncPlayerHand, Server.GetClientRankByUid(uid), (Packet p) =>
                {
                    p.Write(uid);
                    int i = 0;
                    foreach (var c in GetPlayerInfoById(uid).hand)
                    {
                        PokerCard.WriteSelfToAPacket(c, ref p);
                        i++;
                    }
                    while (i < 2)
                    {
                        PokerCard temp = new PokerCard();
                        temp.notRevealed = true;
                        PokerCard.WriteSelfToAPacket(temp, ref p);
                        i++;
                    }
                });
            }
        }
        private void SyncFlopTurnRiver()
        {
            CallEachPlayer((int clientRank) =>
            {
                ServerSend.RpcSend(ServerPackets.syncFlopTurnRiver, clientRank, (Packet p) =>
                {
                    int i = 0;
                    foreach (var c in flopTurnRiver)
                    {
                        PokerCard.WriteSelfToAPacket(c, ref p);
                        i++;
                    }
                    while (i < 5)
                    {
                        PokerCard temp = new PokerCard();
                        temp.notRevealed = true;
                        PokerCard.WriteSelfToAPacket(temp, ref p);
                        i++;
                    }
                });
            });
        }
        private void RequestAction(int uid)
        {
            ServerSend.RpcSend(ServerPackets.requestPlayerAction, Server.GetClientRankByUid(uid), (Packet p) =>
            {

            });
        }
        private void Congradulation(int uid, bool isWinner)
        {
            CallEachPlayer((int clientRank) =>
            {
                ServerSend.RpcSend(ServerPackets.congrateWinner, clientRank, (Packet p) =>
                {
                    p.Write(uid);
                    p.Write(isWinner);
                });
            });
        }
        public void ForceEnd()
        {
            gamePaused = true;
            foreach (var p in players)
            {
                if (p != null && AccountManager.Inst.accountInfo.ContainsKey(p.uid))
                {
                    AccountManager.Inst.accountInfo[p.uid].cash += p.moneyInPocket;
                    AccountManager.Inst.accountInfo[p.uid].cash += p.moneyInPot;
                    p.moneyInPocket = 0;
                    p.moneyInPot = 0;
                }
            }
            players.Clear();
            SyncPlayers();
            SyncStat();
        }
        public void ForceSync()
        {
            SyncStat();
            SyncPlayers();
            SyncFlopTurnRiver();
            foreach (var player in players)
                SyncPlayersHand(player.uid);
        }
    }
}