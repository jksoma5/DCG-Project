using System;
using System.Collections.Generic;
using DCG.Core;
using UnityEngine;

namespace DCG.Gameplay
{
    public sealed class CombatController
    {
        readonly ActorSimulation owner;
        ActorSimulation target;
        float remaining;
        ulong shotSequence;
        public int Ammo { get; private set; }
        public ActionState State { get; private set; } = ActionState.Ready;
        public event Action<CombatEvent> Fired;
        public CombatController(ActorSimulation owner)
        { this.owner = owner; Ammo = owner.tuning.magazineSize; }
        public bool CanReach(ActorSimulation candidate)
        {
            if (candidate == null || !candidate.Health.IsAlive || candidate.team == owner.team) return false;
            Vector3 delta = candidate.AimPoint - owner.AimPoint;
            return delta.sqrMagnitude <= owner.tuning.attackRange * owner.tuning.attackRange &&
                !Physics.Linecast(owner.AimPoint, candidate.AimPoint, owner.World.WorldMask,
                    QueryTriggerInteraction.Ignore);
        }
        public bool TryAttack(ActorSimulation candidate)
        {
            if (State != ActionState.Ready || !owner.Health.IsAlive || !CanReach(candidate)) return false;
            if (Ammo <= 0) { BeginReload(); return false; }
            target = candidate; State = ActionState.Windup; remaining = owner.tuning.windupSeconds;
            return true;
        }
        public void CancelWindup()
        { if (State == ActionState.Windup) { State = ActionState.Ready; target = null; } }
        public void Cancel() { target = null; remaining = 0; State = ActionState.Ready; }
        void BeginReload() { State = ActionState.Reload; remaining = owner.tuning.reloadSeconds; }
        public void Step(float dt, uint tick)
        {
            if (State == ActionState.Ready) return;
            remaining -= dt;
            if (remaining > 0) return;
            switch (State)
            {
                case ActionState.Windup:
                    if (CanReach(target)) Fire(tick);
                    else { State = ActionState.Ready; target = null; break; }
                    State = ActionState.Recovery; remaining = owner.tuning.recoverySeconds; break;
                case ActionState.Recovery:
                    if (Ammo <= 0) BeginReload(); else State = ActionState.Ready;
                    break;
                case ActionState.Reload:
                    Ammo = owner.tuning.magazineSize; State = ActionState.Ready; break;
            }
        }
        void Fire(uint tick)
        {
            Ammo--; shotSequence++;
            Vector3 origin = owner.AimPoint;
            Vector3 direction = (target.AimPoint - origin).normalized;
            owner.Motor.Face(direction, 100000, 1);
            var damageByTarget = new Dictionary<ActorSimulation, float>();
            int pellets = Mathf.Max(1, owner.tuning.pelletCount);
            var impacts = new Vector3[pellets];
            for (int i = 0; i < pellets; i++)
            {
                float angle = pellets == 1 ? 0 : ((float)i / (pellets - 1) - .5f) * owner.tuning.spreadDegrees;
                Vector3 ray = Quaternion.AngleAxis(angle, Vector3.up) * direction;
                impacts[i] = origin + ray * owner.tuning.attackRange;
                if (!Physics.Raycast(origin, ray, out var hit, owner.tuning.attackRange,
                    owner.World.ShotMask, QueryTriggerInteraction.Collide)) continue;
                impacts[i] = hit.point;
                var victim = hit.collider.GetComponentInParent<ActorSimulation>();
                if (victim == null || victim == owner || victim.team == owner.team) continue;
                damageByTarget.TryGetValue(victim, out float damage);
                damageByTarget[victim] = damage + owner.tuning.damagePerPellet;
            }
            foreach (var entry in damageByTarget)
                owner.World.Damage.Apply(new DamageRequest(owner.Id, entry.Key.Id, shotSequence, entry.Value), entry.Key);
            Fired?.Invoke(new CombatEvent { EventId = shotSequence, ActorId = owner.Id,
                ActionId = 0, StartTick = tick, Origin = origin,
                ImpactPoint = impacts[(pellets - 1) / 2], ImpactPoints = impacts });
        }
    }
}
