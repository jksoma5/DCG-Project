using System.Collections.Generic;
using DCG.Core;

namespace DCG.Gameplay
{
    // The offline authority follows the same command boundary as a future network adapter.
    public sealed class LocalSession : ICommandGateway
    {
        readonly SimulationWorld world;
        readonly Queue<PlayerCommand> queue = new Queue<PlayerCommand>();
        readonly Dictionary<ActorId, uint> sequences = new Dictionary<ActorId, uint>();
        public int PendingCount => queue.Count;
        public LocalSession(SimulationWorld world) { this.world = world; }
        public bool Submit(ActorId sender, PlayerCommand command)
        {
            var envelope = command.Envelope;
            var actor = world.Find(envelope.ActorId);
            if (sender != envelope.ActorId || actor == null || !actor.Health.IsAlive || queue.Count >= 256)
                return false;
            if (sequences.TryGetValue(sender, out var previous) &&
                !CommandValidation.IsNewer(envelope.Sequence, previous)) return false;
            if ((envelope.CommandType == CommandType.MoveTo || envelope.CommandType == CommandType.AttackMove) &&
                !CommandValidation.IsFinite(command.Move.Destination)) return false;
            if (envelope.CommandType == CommandType.Target)
            {
                var target = world.Find(command.Target.TargetActorId);
                if (target == null || target.team == actor.team || !target.Health.IsAlive) return false;
            }
            if (actor.Movement is IActorActionPolicy policy)
            {
                if (!policy.Accepts(command)) return false;
            }
            else if (envelope.CommandType != CommandType.MoveTo && envelope.CommandType != CommandType.AttackMove &&
                envelope.CommandType != CommandType.Target && envelope.CommandType != CommandType.Stop) return false;
            sequences[sender] = envelope.Sequence;
            queue.Enqueue(command);
            return true;
        }
        public void Drain()
        {
            while (queue.Count > 0)
            {
                var command = queue.Dequeue();
                var actor = world.Find(command.Envelope.ActorId);
                if (actor != null && actor.Health.IsAlive) actor.Receive(command);
            }
        }
    }
}
