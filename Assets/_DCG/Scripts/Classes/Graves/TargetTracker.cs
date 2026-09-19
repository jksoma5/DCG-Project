using DCG.Gameplay;
using UnityEngine;

namespace DCG.Classes.Graves
{
    public static class TargetTracker
    {
        public static ActorSimulation Acquire(ActorSimulation owner)
        {
            ActorSimulation best = null;
            float distance = owner.tuning.acquireRange * owner.tuning.acquireRange;
            foreach (var candidate in owner.World.Actors)
            {
                if (candidate == null || candidate == owner || candidate.team == owner.team || !candidate.Health.IsAlive) continue;
                float d = (candidate.transform.position - owner.transform.position).sqrMagnitude;
                if (d >= distance || Physics.Linecast(owner.AimPoint, candidate.AimPoint,
                    owner.World.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                best = candidate; distance = d;
            }
            return best;
        }
        public static ActorSimulation AcquireClosestToPoint(ActorSimulation owner, Vector3 point)
        {
            ActorSimulation best = null;
            float distance = float.PositiveInfinity;
            point.y = 0;
            foreach (var candidate in owner.World.Actors)
            {
                if (candidate == null || candidate == owner || candidate.team == owner.team || !candidate.Health.IsAlive)
                    continue;
                Vector3 candidatePoint = candidate.transform.position; candidatePoint.y = 0;
                float d = (candidatePoint - point).sqrMagnitude;
                if (d >= distance) continue;
                best = candidate; distance = d;
            }
            return best;
        }
    }
}
