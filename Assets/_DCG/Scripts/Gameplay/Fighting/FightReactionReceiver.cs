using DCG.Core;
using UnityEngine;

namespace DCG.Gameplay.Fighting
{
    // Lets a class that is not a fighter be on the receiving end of a fighter's combo.
    //
    // Damage alone is not a combo. A combo exists because a hit takes the body away from its owner for a
    // known number of frames: it staggers, it launches, it stays in the air long enough to be hit again,
    // it lands, it gets up. Without that, a fighter hitting a sniper is just chip damage and the second
    // hit of a chain lands on someone who already walked away.
    //
    // So the reaction half of the fighting system is lifted out of FighterAgent and put on any actor:
    // the same FighterStateMachine, the same launch and juggle arithmetic, the same getup. What this
    // actor does not get is the acting half - no move list, no guard, no input. It is a victim only.
    //
    // While a reaction owns the body, the actor's own policy is skipped (see ActorSimulation.Step), so a
    // juggled sniper cannot walk or shoot out of the air.
    [RequireComponent(typeof(ActorSimulation))]
    public sealed class FightReactionReceiver : MonoBehaviour
    {
        // Down, getup and juggle limits. These belong to the attacker's move set, so whoever attaches
        // this hands over the rules its combos were written against.
        public FightMoveSet rules;

        public FighterStateMachine State { get; } = new FighterStateMachine();
        // True while the reaction, rather than the class itself, is moving the body.
        public bool Busy => State.Busy;

        ActorSimulation actor;

        void Awake()
        {
            actor = GetComponent<ActorSimulation>();
            actor.Reaction = this;
        }

        void OnDestroy()
        {
            if (actor != null && actor.Reaction == this) actor.Reaction = null;
        }

        // What the attacker's move selection sees. A fighter reports a lot more than this: guard
        // directions, sidesteps, crush windows. A class from another game has none of those, which is
        // exactly why it cannot block - it can only be hit, countered, juggled or hit on the ground.
        public FighterTags Tags
        {
            get
            {
                FighterTags tags = FighterTags.Standing;
                if (State.IsAirborne || (actor.Motor != null && actor.Motor.State == MovementState.Airborne))
                    tags |= FighterTags.Airborne;
                if (State.IsDown) tags = (tags & ~FighterTags.Airborne) | FighterTags.Down;
                if (State.Backturned) tags |= FighterTags.Backturned;
                if (State.Stun > 0) tags |= FighterTags.Hitstun;
                // Being caught mid-action is a counter hit. Every class already publishes that much
                // through its action policy, so a reload or a shot being wound up counts the same way a
                // fighter's startup frames do.
                if (actor.Movement is IActorActionPolicy policy)
                    switch (policy.ActionState)
                    {
                        case ActionState.Windup: tags |= FighterTags.AttackStartup; break;
                        case ActionState.Active: tags |= FighterTags.AttackActive; break;
                        case ActionState.Recovery: tags |= FighterTags.AttackRecovery; break;
                        case ActionState.Reload: tags |= FighterTags.AttackRecovery; break;
                    }
                return tags;
            }
        }

        public void Receive(HitOutcome hit, int remaining, Vector3 away, bool airborneHit)
        {
            if (hit == null || rules == null) return;
            State.React(hit, remaining, away, actor.tuning.gravity, rules, airborneHit);
        }

        public void Defeat() { State.Defeat(); }

        // One frame of being hit. Mirrors the airborne and pushback handling in FighterAgent.Prepare, so
        // a launched sniper describes the same arc a launched fighter does.
        public void Step(float deltaTime)
        {
            State.Advance();
            float gravity = actor.tuning != null ? actor.tuning.gravity : -20;
            if (State.IsAirborne)
            {
                var motion = State.StepReaction(gravity, deltaTime);
                actor.Motor.StepFullVelocity(motion, deltaTime);
                if (motion.y <= 0 && actor.Motor.State == MovementState.Grounded) State.Land();
                else if (motion.y > 0 && actor.Motor.Velocity.y < .001f) State.HitCeiling();
            }
            else actor.Motor.Step(State.StepReaction(gravity, deltaTime), gravity, deltaTime);
        }
    }
}
