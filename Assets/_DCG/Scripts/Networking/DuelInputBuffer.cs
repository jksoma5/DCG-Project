using System;
using System.Collections.Generic;
using DCG.Core;

namespace DCG.Networking
{
    // Replace held movement; preserve reliable button edges and discrete actions across loss/reordering.
    public sealed class DuelInputBuffer
    {
        readonly Queue<PlayerCommand> events = new Queue<PlayerCommand>();
        PlayerCommand motion;
        bool hasMotion, hasMotionSequence, hasEventSequence, hasStop;
        uint motionSequence, eventSequence, stopSequence;
        public int PendingEvents => events.Count;
        public static bool IsMotion(PlayerCommand command) => command.Envelope.CommandType == CommandType.DirectControl || command.Envelope.CommandType == CommandType.FightInput;
        public static bool HasEvent(PlayerCommand command) => !IsMotion(command) ||
            command.Direct.PressedButtons != 0 || command.Fight.Pressed != 0 || command.Fight.Released != 0;
        public static PlayerCommand WithoutEdges(PlayerCommand command)
        { command.Direct.PressedButtons = 0; command.Fight.Pressed = command.Fight.Released = 0; return command; }
        public bool Enqueue(PlayerCommand command, bool reliable)
        {
            uint sequence = command.Envelope.Sequence;
            if (reliable)
            {
                if (!HasEvent(command) || (hasEventSequence && !CommandValidation.IsNewer(sequence, eventSequence)) || events.Count >= 32) return false;
                eventSequence = sequence; hasEventSequence = true;
                if (command.Envelope.CommandType == CommandType.Stop)
                {
                    stopSequence = sequence; hasStop = true;
                    if (hasMotion && !CommandValidation.IsNewer(motion.Envelope.Sequence, sequence)) hasMotion = false;
                }
                events.Enqueue(command); return true;
            }
            if (!IsMotion(command) || (hasMotionSequence && !CommandValidation.IsNewer(sequence, motionSequence)) ||
                (hasStop && !CommandValidation.IsNewer(sequence, stopSequence))) return false;
            motionSequence = sequence; hasMotionSequence = true; motion = WithoutEdges(command); hasMotion = true;
            return true;
        }
        public void Drain(Action<PlayerCommand> consume)
        {
            while (events.Count > 0) consume(events.Dequeue());
            if (hasMotion) { hasMotion = false; consume(motion); }
        }
    }
}
