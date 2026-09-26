using UnityEngine;

namespace DCG.Core
{
    // Fixed-size presentation state; no scene references, arrays or strings on the frequent wire path.
    public struct ReplicaState
    {
        public int Mode, Stance, AimMode, Scope, Reserve, MoveIndex, Age, Stun, Reaction, ReactionKind, Side;
        public float Reload, Recovery, Kick, Lean, Height, PhaseTime, Cooldown, Cooldown2;
        public Vector2 Aim;
        public Vector3 SwordPosition, SwordDestination, ReactionVelocity;
        public bool Crouched, GuardStun, ForceCrouch, Backturned;
    }
    public interface IReplicaStateSource
    {
        ReplicaState CaptureReplica();
        void ApplyReplica(ReplicaState state);
    }
}
