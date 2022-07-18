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
            public int moneyInPoxket;
            public int moneyInPot;
            public bool hasBidThisRound;
            public bool hasFolded;
            public bool ifAllIn;
            public bool hasQuited;
            public List<PokerCard> hand = new List<PokerCard>();
            public int timeCard;
        }

        private float pauseBetweenRounds = 3;
        private float roundTime = 30;
        private float timer = 0;
        private int roundNum = 0;
        private int timeCardPerRound = 1;
        private int smallBlindMoneyNum = 1;
        private int currentActivePlayer = 0;
        private int currentSmallBlind = 0;
        private bool gamePaused = false;

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

        public GameFlowManager(int maxPlayer)
        {
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
                    players[i].moneyInPoxket = buyIn;
                    added = true;
                }
            }
            if(!added)
            {
                if (players.Count >= players.Capacity)
                    return false;
                PlayerInGameStat newPlayer = new PlayerInGameStat();
                newPlayer.uid = id;
                newPlayer.moneyInPoxket = buyIn;
                players.Add(newPlayer);
                added = true;
            }
            return !added;
        }

        public void RemovePlayer(int id)
        {
            foreach (var p in players)
            {
                if (p.uid == id)
                {
                    p.hasQuited = true;
                    p.hasFolded = true;
                }
            }
        }

        public void Tick()
        {
            timer += (float)App.s_msPerTick / 1000.0f;
            if (gamePaused && timer > pauseBetweenRounds && 
                GetActivePlayerCount(false) > 1)
            {
                gamePaused = false;
                StartNewRound();
                return;
            }

            if (timer > roundTime)
            {
                timer = 0;
                PlayerCheckOrFold(players[currentActivePlayer].uid);
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
                        // request the player to give input
                    }
                }
            }
        }

        public int HighestBid()
        {
            int max = 0;
            foreach(var p in players)
            {
                if (p.moneyInPot > max)
                    max = p.moneyInPot;
            }
            return max;
        }

        public bool PlayerBid(int id, int amount)
        {
            foreach (var player in players)
            {
                if (player.uid == id)
                {
                    player.ifAllIn = player.moneyInPoxket <= amount;
                    player.moneyInPoxket -= amount;
                    player.moneyInPot += amount;
                    player.hasBidThisRound = true;
                    return player.moneyInPot >= HighestBid();
                }
            }
            return false;
        }
        public bool PlayerCheckOrFold(int id)
        {
            foreach (var player in players)
            {
                if (player.uid == id)
                {
                    if (player.moneyInPot < HighestBid())
                    {
                        player.hasFolded = true;
                        return false;
                    }
                    player.hasBidThisRound = true;
                    return true;
                }
            }
            return false;
        }

        public void StartNewRound()
        {
            deck.Clear();
            flopTurnRiver.Clear();
            roundNum++;
            for(int idx = players.Count - 1; idx >= 0; idx--)
            {
                if (players[idx] != null && players[idx].hasQuited)
                {
                    AccountManager.Inst.GetAccount(players[idx].uid).cash += players[idx].moneyInPoxket;
                    players.RemoveAt(idx);
                }
            }
            if (GetActivePlayerCount(false) <= 1)
            {
                gamePaused = true;
                // sync room stat
                return;
            }
            else { /* sync room stat */ }
            foreach (var player in players)
            {
                player.hand.Clear();
                player.moneyInPot = 0;
                player.hasFolded = false;
                player.ifAllIn = false;
                if (roundNum % timeCardPerRound == 0) player.timeCard++;
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

                // sync player hand and stat
            }
            curState = roundState.bidRound0;
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
                    PlayerBid(GetPlayerIdByIndex(currentSmallBlind), smallBlindMoneyNum);
                    PlayerBid(GetPlayerIdByIndex(GetNextPlayerIdx(currentSmallBlind)),
                        2 * smallBlindMoneyNum);
                    currentActivePlayer = GetNextPlayerIdx(GetNextPlayerIdx(currentSmallBlind));
                    // tell the player to do action
                    curState = roundState.bidRound1;
                    break;

                case roundState.bidRound1:
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
                    curState = roundState.bidRound2;
                    break;

                case roundState.bidRound2:
                    randIdx = new Random().Next(0, deck.Count);
                    flopTurnRiver.Add(deck[randIdx]);
                    deck.RemoveAt(randIdx);
                    currentActivePlayer = GetNextPlayerIdx(GetNextPlayerIdx(currentSmallBlind));
                    curState = roundState.bidRound3;
                    break;

                case roundState.bidRound3:
                    randIdx = new Random().Next(0, deck.Count);
                    flopTurnRiver.Add(deck[randIdx]);
                    deck.RemoveAt(randIdx);
                    currentActivePlayer = GetNextPlayerIdx(GetNextPlayerIdx(currentSmallBlind));
                    curState = roundState.roundFinished;
                    break;

                case roundState.roundFinished:
                    // divide the pool
                    RoundEnd();
                    break;

                default:
                    break;

            }
        }

        private bool IfBidRoundComplete()
        {
            foreach (var p in players)
            {
                if (!p.hasFolded && !p.hasBidThisRound &&
                    !p.ifAllIn && p.moneyInPot != HighestBid())
                    return false;
            }
            return true;
        }

        public void RoundEnd()
        {
            // calculate the result
            gamePaused = true;
            timer = 0;
            currentActivePlayer = -1;


        }

        public void HandlePacket(Packet p, ClientPackets rpc)
        {
            int id;
            switch (rpc)
            {
                case ClientPackets.bid:
                    id = p.ReadInt();
                    int bid = p.ReadInt();
                    if (id == GetNextPlayerIdx(currentActivePlayer))
                        break;
                    timer = 0;
                    PlayerBid(id, bid);
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
                            // request the player to give input
                        }
                    }
                    break;
                case ClientPackets.fold:
                    id = p.ReadInt();
                    if (id == GetNextPlayerIdx(currentActivePlayer))
                        break;
                    timer = 0;
                    PlayerCheckOrFold(id);
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
                            // request the player to give input
                        }
                    }
                    break;
                case ClientPackets.useTimeCard:
                    if (p.ReadInt() == GetNextPlayerIdx(currentActivePlayer))
                        timer -= roundTime;
                    break;
                default:
                    break;
            }
        }

        private int[] TellScore(List<PokerCard> cards)
        {
            return new int[] { 0, 0, 0, 0, 0, 0};
        }
    }
}
