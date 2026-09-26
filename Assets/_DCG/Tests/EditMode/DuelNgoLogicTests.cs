using System.Collections.Generic;
using DCG.Core;
using DCG.Networking;
using NUnit.Framework;

namespace DCG.Tests
{
    public sealed class DuelNgoLogicTests
    {
        [Test] public void QuickStartWaitsForBothPlayersThenUsesDefaultsWithoutDrawing()
        {
            var room = new DuelRoom(() => throw new System.Exception("No random draw for defaults"));
            Assert.That(room.QuickStart(0, 1, 10), Is.True);
            Assert.That(room.State.phase, Is.EqualTo(DuelPhase.Characters));
            Assert.That(room.State.characters[1], Is.EqualTo(-1), "Host must not choose on behalf of the guest.");
            Assert.That(room.Join(DuelRoom.Protocol), Is.True);
            Assert.That(room.QuickStart(1, 1, 20), Is.True);
            Assert.That(room.State.characters, Is.EqualTo(new[] { 0, 0 }));
            Assert.That(room.State.selectedMap, Is.EqualTo(0));
            Assert.That(room.State.phase, Is.EqualTo(DuelPhase.Loading));
            Assert.That(room.State.deadline, Is.EqualTo(80));
            Assert.That(room.QuickStart(1, 1, 21), Is.False);
            Assert.That(room.Ready(0, 1, 21), Is.True);
            Assert.That(room.State.phase, Is.EqualTo(DuelPhase.Loading));
        }
        [Test] public void QuickStartCanMixWithManualCharacterAndMapSelection()
        {
            var room = new DuelRoom(() => 1); room.Join(DuelRoom.Protocol);
            room.QuickStart(0, 1, 0); room.Character(1, 1, 4, true);
            Assert.That(room.State.phase, Is.EqualTo(DuelPhase.Maps));
            Assert.That(room.State.characters[1], Is.EqualTo(4));
            Assert.That(room.Map(0, 1, 1, 0), Is.False);
            Assert.That(room.Map(1, 1, 1, 0), Is.True);
            Assert.That(room.State.selectedMap, Is.EqualTo(1));
            Assert.That(room.QuickStart(0, 0, 0), Is.False);
        }
        static PlayerCommand Motion(uint sequence) => new PlayerCommand {
            Envelope = new CommandEnvelope { ActorId = new ActorId(2), Sequence = sequence, CommandType = CommandType.DirectControl }
        };
        [Test] public void LateReliableActionSurvivesNewerUnreliableMovement()
        {
            var buffer = new DuelInputBuffer();
            Assert.That(buffer.Enqueue(Motion(12), false), Is.True);
            var action = Motion(10); action.Envelope.CommandType = CommandType.Action; action.Action.ActionId = 1;
            Assert.That(buffer.Enqueue(action, true), Is.True);
            Assert.That(buffer.Enqueue(action, true), Is.False);
            Assert.That(buffer.Enqueue(Motion(11), false), Is.False);
            var result = new List<PlayerCommand>(); buffer.Drain(result.Add);
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Envelope.CommandType, Is.EqualTo(CommandType.Action));
            Assert.That(result[1].Envelope.Sequence, Is.EqualTo(12));
        }
        [Test] public void StopRejectsOlderMovementAndBoundedEventsDoNotGrowForever()
        {
            var buffer = new DuelInputBuffer(); buffer.Enqueue(Motion(9), false);
            var stop = Motion(10); stop.Envelope.CommandType = CommandType.Stop;
            Assert.That(buffer.Enqueue(stop, true), Is.True);
            Assert.That(buffer.Enqueue(Motion(9), false), Is.False);
            var result = new List<PlayerCommand>(); buffer.Drain(result.Add);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(buffer.Enqueue(Motion(11), false), Is.True);
            for (uint i = 11; i <= 42; i++) { stop.Envelope.Sequence = i; Assert.That(buffer.Enqueue(stop, true), Is.True); }
            stop.Envelope.Sequence = 43; Assert.That(buffer.Enqueue(stop, true), Is.False);
            Assert.That(buffer.PendingEvents, Is.EqualTo(32));
        }
        [Test] public void MovementCoalescesButReliableButtonEdgesRemain()
        {
            var buffer = new DuelInputBuffer(); var press = Motion(1); press.Direct.PressedButtons = ControlButtons.Jump;
            buffer.Enqueue(press, true); buffer.Enqueue(press, false);
            for (uint i = 2; i < 100; i++) buffer.Enqueue(Motion(i), false);
            var result = new List<PlayerCommand>(); buffer.Drain(result.Add);
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Direct.PressedButtons, Is.EqualTo(ControlButtons.Jump));
            Assert.That(result[1].Direct.PressedButtons, Is.EqualTo(ControlButtons.None));
            Assert.That(result[1].Envelope.Sequence, Is.EqualTo(99));
        }
    }
}
