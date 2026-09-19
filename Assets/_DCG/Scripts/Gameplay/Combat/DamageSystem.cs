using System;
using System.Collections.Generic;
using DCG.Core;

namespace DCG.Gameplay
{
    public readonly struct DamageRequest
    {
        public readonly ActorId Attacker, Victim;
        public readonly ulong AttackId;
        public readonly float Amount;
        public DamageRequest(ActorId attacker, ActorId victim, ulong attackId, float amount)
        { Attacker = attacker; Victim = victim; AttackId = attackId; Amount = amount; }
    }
    public sealed class DamageSystem
    {
        readonly HashSet<(ActorId, ActorId, ulong)> applied = new HashSet<(ActorId, ActorId, ulong)>();
        readonly Queue<(ActorId, ActorId, ulong)> history = new Queue<(ActorId, ActorId, ulong)>();
        public bool Apply(DamageRequest request, ActorSimulation target)
        {
            if (target == null || !target.Initialized || request.Victim != target.Id ||
                request.Amount <= 0 || !CommandValidation.IsFinite(request.Amount)) return false;
            var key = (request.Attacker, request.Victim, request.AttackId);
            if (applied.Contains(key) || !target.Health.Apply(request.Amount)) return false;
            applied.Add(key); history.Enqueue(key);
            if (history.Count > 4096) applied.Remove(history.Dequeue());
            return true;
        }
    }
}
