using DCG.Core;
using UnityEngine;

namespace DCG.Gameplay
{
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class ActorSimulation : MonoBehaviour
    {
        public uint actorNumber = 1;
        public ClassId classId;
        public int team;
        public PrototypeTuning tuning;
        public ActorId Id => new ActorId(actorNumber);
        public HealthState Health { get; private set; }
        public CharacterMotor Motor { get; private set; }
        public CombatController Combat { get; private set; }
        public IMovementPolicy Movement { get; private set; }
        // Set by a FightReactionReceiver on the same object. While it is busy, a fighter's hit owns this
        // body: the class cannot move or act its way out of a combo.
        public DCG.Gameplay.Fighting.FightReactionReceiver Reaction { get; set; }
        public SimulationWorld World { get; private set; }
        public uint LastSequence { get; private set; }
        uint lastClientTick, lastInputTick;
        public bool Initialized => Health != null;
        public Vector3 AimPoint => transform.position + Vector3.up;
        public void Initialize(SimulationWorld world, float healthMultiplier = 1f)
        {
            if (Initialized) return;
            if (tuning == null) throw new System.InvalidOperationException("Actor needs a tuning asset.");
            World = world;
            Motor = GetComponent<CharacterMotor>();
            Health = new HealthState(tuning.maxHealth * healthMultiplier);
            Combat = new CombatController(this);
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component is IMovementPolicy policy) { Movement = policy; break; }
            Health.Died += () => { Movement?.Stop(); Combat.Cancel(); };
        }
        public void Receive(PlayerCommand command)
        {
            LastSequence = command.Envelope.Sequence;
            lastClientTick = command.Envelope.ClientTick;
            lastInputTick = World.Tick;
            Movement?.Receive(command);
        }
        public void Step(float dt, uint tick)
        {
            // A hit reaction runs before the alive check, so a body that was launched keeps falling and
            // lands instead of stopping in the air the moment it dies.
            if (Reaction != null && Reaction.Busy) { Reaction.Step(dt); return; }
            if (!Health.IsAlive) return;
            // A fighter advances itself on its own frame loop; stepping it here too would double its
            // gravity and movement in the same tick.
            if (Movement is ISelfSteppedPolicy) return;
            Vector3 desired = Movement?.DesiredVelocity(dt) ?? Vector3.zero;
            if (Movement is IActorActionPolicy policy)
            {
                if (policy.UseFullVelocity) Motor.StepFullVelocity(desired, dt);
                else Motor.Step(desired, tuning.gravity, dt);
                policy.AfterMove(dt, tick);
            }
            else
            {
                Motor.Step(desired, tuning.gravity, dt);
                if (Combat.State != ActionState.Windup) Motor.Face(desired, tuning.turnSpeed, dt);
                Combat.Step(dt, tick);
            }
        }
        public ActorSnapshot Snapshot(uint tick) => new ActorSnapshot {
            ActorId = Id, ServerTick = tick, LastProcessedSequence = LastSequence,
            LastProcessedClientTick = lastClientTick, LastProcessedServerTick = lastInputTick,
            Position = transform.position, Velocity = Motor.Velocity, Facing = transform.eulerAngles.y,
            LifeState = Health.IsAlive ? LifeState.Alive : LifeState.Dead,
            MovementState = Motor.State, ActionState = Movement is IActorActionPolicy p ? p.ActionState : Combat.State,
            Ammo = Movement is IActorAmmoSource ammoSource ? ammoSource.Ammo : Movement is IActorActionPolicy ? 0 : Combat.Ammo, Health = Health.Current
        };
    }
}
