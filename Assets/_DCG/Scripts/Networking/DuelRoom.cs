using System;
using DCG.Core;

namespace DCG.Networking
{
    public enum DuelPhase { Characters, Maps, Loading, Countdown, Battle, Result, Closed }

    [Serializable]
    public sealed class DuelState
    {
        public int round = 1;
        public DuelPhase phase;
        public bool guestConnected;
        public int[] characters = { -1, -1 };
        public bool[] characterLocked = new bool[2];
        public bool[] quickStart = new bool[2];
        public int[] maps = { -1, -1 };
        public bool[] mapLocked = new bool[2];
        public bool[] ready = new bool[2];
        public bool[] rematch = new bool[2];
        public int selectedMap = -1;
        public int winner = -2; // -1 draw; -2 no result
        public double deadline;
        public string message = "";
    }

    // Only the host owns this state machine. Sender slots come from the connection, never the packet.
    public sealed class DuelRoom
    {
        public const string Protocol = "dcg-ngo-2.13-duel-v2";
        public DuelState State { get; private set; } = new DuelState();
        readonly Func<int> coin;
        public DuelRoom(Func<int> coin) { this.coin = coin; }
        public bool Join(string protocol)
        {
            if (protocol != Protocol || State.guestConnected || State.phase != DuelPhase.Characters) return false;
            State.guestConnected = true;
            return true;
        }
        bool Sender(int slot, int round, DuelPhase phase) => slot >= 0 && slot < 2 &&
            (slot == 0 || State.guestConnected) && State.round == round && State.phase == phase;
        public bool Character(int slot, int round, int id, bool confirm)
        {
            if (!Sender(slot, round, DuelPhase.Characters) || id < 0 || id > (int)ClassId.Paul || State.characterLocked[slot]) return false;
            State.characters[slot] = id;
            State.characterLocked[slot] = confirm;
            AdvanceCharacters();
            return true;
        }
        public bool QuickStart(int slot, int round, double now)
        {
            if (!Sender(slot, round, DuelPhase.Characters) || State.characterLocked[slot]) return false;
            State.characters[slot] = (int)ClassId.Graves;
            State.characterLocked[slot] = State.quickStart[slot] = true;
            State.maps[slot] = 0;
            State.mapLocked[slot] = true;
            AdvanceCharacters();
            ResolveMap(now);
            return true;
        }
        void AdvanceCharacters()
        {
            if (State.guestConnected && State.characterLocked[0] && State.characterLocked[1]) State.phase = DuelPhase.Maps;
        }
        void ResolveMap(double now)
        {
            if (State.phase != DuelPhase.Maps || !State.mapLocked[0] || !State.mapLocked[1]) return;
            State.selectedMap = State.maps[0] == State.maps[1] ? State.maps[0] : State.maps[coin() & 1];
            State.phase = DuelPhase.Loading; State.deadline = now + 60;
        }
        public bool Map(int slot, int round, int id, double now)
        {
            if (!Sender(slot, round, DuelPhase.Maps) || id < 0 || id > 1 || State.mapLocked[slot]) return false;
            State.maps[slot] = id; State.mapLocked[slot] = true;
            ResolveMap(now);
            return true;
        }
        public bool Ready(int slot, int round, double now)
        {
            if (!Sender(slot, round, DuelPhase.Loading) || State.ready[slot]) return false;
            State.ready[slot] = true;
            if (State.ready[0] && State.ready[1]) { State.phase = DuelPhase.Countdown; State.deadline = now + 3; }
            return true;
        }
        public bool Advance(double now)
        {
            if (State.phase == DuelPhase.Loading && now >= State.deadline)
            { Close("Map preparation timed out."); return true; }
            if (State.phase != DuelPhase.Countdown || now < State.deadline) return false;
            State.phase = DuelPhase.Battle;
            return true;
        }
        public bool Finish(bool firstAlive, bool secondAlive)
        {
            if (State.phase != DuelPhase.Battle || (firstAlive && secondAlive)) return false;
            State.winner = firstAlive ? 0 : secondAlive ? 1 : -1;
            State.phase = DuelPhase.Result;
            return true;
        }
        public bool Rematch(int slot, int round)
        {
            if (!Sender(slot, round, DuelPhase.Result) || !State.guestConnected || State.rematch[slot]) return false;
            State.rematch[slot] = true;
            if (State.rematch[0] && State.rematch[1]) State = new DuelState { round = round + 1, guestConnected = true };
            return true;
        }
        public void Disconnect()
        {
            State.guestConnected = false;
            if (State.phase == DuelPhase.Battle) { State.winner = 0; State.phase = DuelPhase.Result; State.message = "Opponent disconnected. You win."; }
            else Close("Opponent disconnected. Session ended.");
        }
        public void Close(string reason) { State.phase = DuelPhase.Closed; State.message = reason; }
    }
}
