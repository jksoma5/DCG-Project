using System;


using DCG.Networking;
using NUnit.Framework;

namespace DCG.Tests
{
    public sealed class DuelRoomTests
    {
        static DuelRoom Selected(Func<int> coin = null)
        {
            var room = new DuelRoom(coin ?? (() => 0));
            Assert.That(room.Join(DuelRoom.Protocol), Is.True);
            room.Character(0, 1, 0, true); room.Character(1, 1, 2, true);
            return room;
        }
        [Test] public void BothCharactersMustBeConfirmedAndSameClassIsAllowed()
        {
            var room = new DuelRoom(() => 0);
            Assert.That(room.Character(1, 1, 0, true), Is.False);
            Assert.That(room.Join("old-client"), Is.False);
            Assert.That(room.Join(DuelRoom.Protocol), Is.True);
            Assert.That(room.Join(DuelRoom.Protocol), Is.False);
            room.Character(0, 1, 4, true); room.Character(1, 1, 4, false);
            Assert.That(room.State.phase, Is.EqualTo(DuelPhase.Characters));
            Assert.That(room.Character(0, 1, 3, true), Is.False);
            Assert.That(room.Character(1, 1, 4, true), Is.True);
            Assert.That(room.State.phase, Is.EqualTo(DuelPhase.Maps));
            Assert.That(room.Map(1, 0, 0, 0), Is.False);
            Assert.That(room.Map(2, 1, 0, 0), Is.False);
            Assert.That(room.Map(1, 1, 2, 0), Is.False);
        }
        [Test] public void MatchingMapsDoNotDrawAndConflictingMapsDrawExactlyOnce()
        {
            int draws = 0;
            var room = Selected(() => { draws++; return 1; });
            room.Map(0, 1, 0, 0);
            Assert.That(room.State.selectedMap, Is.EqualTo(-1));
            room.Map(1, 1, 1, 0);
            Assert.That(room.State.selectedMap, Is.EqualTo(1));
            Assert.That(room.Map(1, 1, 0, 0), Is.False);
            Assert.That(draws, Is.EqualTo(1));
            room = Selected(() => throw new Exception("Equal maps must not draw"));
            room.Map(0, 1, 1, 0); room.Map(1, 1, 1, 0);
            Assert.That(room.State.selectedMap, Is.EqualTo(1));
        }
        [Test] public void ReadyBarrierCountdownResultAndRematchRejectOldMessages()
        {
            var room = Selected(); room.Map(0, 1, 0, 0); room.Map(1, 1, 1, 0);
            room.Ready(0, 1, 1);
            Assert.That(room.Advance(4), Is.False);
            Assert.That(room.Finish(false, true), Is.False);
            room.Ready(1, 1, 5);
            Assert.That(room.Advance(7.9), Is.False);
            Assert.That(room.Advance(8), Is.True);
            Assert.That(room.Finish(true, true), Is.False);
            Assert.That(room.Finish(false, false), Is.True);
            Assert.That(room.State.winner, Is.EqualTo(-1));
            room.Rematch(0, 1);
            Assert.That(room.State.phase, Is.EqualTo(DuelPhase.Result));
            room.Rematch(1, 1);
            Assert.That(room.State.round, Is.EqualTo(2));
            Assert.That(room.State.characters, Is.EqualTo(new[] { -1, -1 }));
            Assert.That(room.State.maps, Is.EqualTo(new[] { -1, -1 }));
            Assert.That(room.Character(0, 1, 0, true), Is.False);
            Assert.That(room.Ready(1, 1, 100), Is.False);
        }
        [Test] public void LoadingTimesOutAndDisconnectHasPhaseSpecificOutcome()
        {
            var room = Selected(); room.Map(0, 1, 0, 0); room.Map(1, 1, 0, 0);
            Assert.That(room.Advance(60), Is.True);
            Assert.That(room.State.phase, Is.EqualTo(DuelPhase.Closed));
            room = Selected(); room.Disconnect();
            Assert.That(room.State.phase, Is.EqualTo(DuelPhase.Closed));
            room = Selected(); room.Map(0, 1, 0, 0); room.Map(1, 1, 0, 0);
            room.Ready(0, 1, 0); room.Ready(1, 1, 0); room.Advance(3); room.Disconnect();
            Assert.That(room.State.phase, Is.EqualTo(DuelPhase.Result));
            Assert.That(room.State.winner, Is.EqualTo(0));
            Assert.That(room.Rematch(0, 1), Is.False);
        }
    }
}
