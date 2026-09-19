using System;
using UnityEngine;

namespace DCG.Core
{
    [Serializable]
    public struct ActorId : IEquatable<ActorId>
    {
        public uint Value;
        public ActorId(uint value) { Value = value; }
        public bool IsValid => Value != 0;
        public bool Equals(ActorId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ActorId other && Equals(other);
        public override int GetHashCode() => (int)Value;
        public override string ToString() => Value.ToString();
        public static bool operator ==(ActorId a, ActorId b) => a.Equals(b);
        public static bool operator !=(ActorId a, ActorId b) => !a.Equals(b);
    }
    public enum ClassId { Graves, Vendetta, Rifle, Sniper, Paul }
    public enum ReferenceStatus { TuningPending, Verified }
    public enum LifeState { Alive, Dead }
    public enum MovementState { Grounded, Airborne }
    public enum ActionState { Ready, Windup, Active, Recovery, Reload }
    public enum OrderType { Idle, MoveTo, AttackTarget, AttackMove, Stop }
    public enum CommandType { MoveTo, Target, AttackMove, Stop, DirectControl, Action, FightInput }
    [Flags] public enum ControlButtons { None = 0, Fire = 1, Aim = 2, Reload = 4, Jump = 8, Sprint = 16, Walk = 32, LeanLeft = 64, LeanRight = 128, Ads = 256, Crouch = 512 }

    [Serializable] public struct CommandEnvelope
    {
        public ActorId ActorId;
        public uint Sequence;
        public uint ClientTick;
        public CommandType CommandType;
    }
    [Serializable] public struct MoveToCommand { public Vector3 Destination; }
    [Serializable] public struct TargetCommand { public ActorId TargetActorId; public OrderType OrderType; }
    [Serializable] public struct DirectControlFrame
    {
        public Vector2 MoveAxes;
        public Vector2 AimYawPitch;
        public Vector3 AimTarget;
        public ControlButtons HeldButtons;
        public ControlButtons PressedButtons;
    }
    [Serializable] public struct ActionCommand
    {
        public int ActionId;
        public ActorId TargetId;
        public Vector3 TargetPoint;
    }
    // Tagged value packet: contains no scene or presentation object references.
    [Flags] public enum FightButtons { None=0, LP=1, RP=2, LK=4, RK=8 }
    [Serializable] public struct FightInputFrame
    {
        public int Direction;
        public FightButtons Held, Pressed, Released;
    }
    [Serializable] public struct PlayerCommand
    {
        public CommandEnvelope Envelope;
        public MoveToCommand Move;
        public TargetCommand Target;
        public DirectControlFrame Direct;
        public ActionCommand Action;
        public FightInputFrame Fight;
    }
    [Serializable] public struct ActorSnapshot
    {
        public uint ServerTick, LastProcessedSequence;
        public ActorId ActorId;
        public Vector3 Position, Velocity;
        public float Facing;
        public LifeState LifeState;
        public MovementState MovementState;
        public ActionState ActionState;
        public int Ammo;
        public float Health;
    }
    [Serializable] public struct CombatEvent
    {
        public ulong EventId;
        public ActorId ActorId;
        public int ActionId;
        public uint StartTick;
        public Vector3 Origin, ImpactPoint;
        public Vector3[] ImpactPoints;
    }
    public interface ICommandGateway
    {
        bool Submit(ActorId sender, PlayerCommand command);
    }
    public static class CommandValidation
    {
        // RFC-style serial arithmetic; supports sequence wrap while rejecting replay.
        public static bool IsNewer(uint value, uint previous) => unchecked((int)(value - previous)) > 0;
        public static bool IsFinite(Vector3 v) =>
            IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        public static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
