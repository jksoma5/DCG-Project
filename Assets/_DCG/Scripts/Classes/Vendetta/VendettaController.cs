using System.Collections.Generic;
using DCG.Core;
using DCG.Gameplay;
using UnityEngine;
namespace DCG.Classes.Vendetta
{
    public enum VendettaPhase { Ready, Dash, Spin, SwordThrow, Flight }
    public sealed class VendettaController : MonoBehaviour, IActorActionPolicy
    {
        public const int ShiftAction = 1, EAction = 2;
        public VendettaTuning tuning;
        ActorSimulation actor;
        Vector2 move, aim;
        Vector3 dashDirection, swordStart, flightDestination, previousPosition;
        float phaseTime, inputAge, blockedTime;
        bool jump;
        ulong attackId;
        readonly HashSet<ActorId> hitActors = new HashSet<ActorId>();
        public VendettaPhase Phase { get; private set; }
        public float ShiftRemaining { get; private set; }
        public float ERemaining { get; private set; }
        public Vector3 SwordPosition { get; private set; }
        public Vector3 SwordDestination { get; private set; }
        public Vector3 FlightDestination => flightDestination;
        public float PhaseProgress => Phase == VendettaPhase.SwordThrow ? Mathf.Clamp01(phaseTime / tuning.swordThrowSeconds) :
            Phase == VendettaPhase.Spin ? Mathf.Clamp01(phaseTime / tuning.spinSeconds) : 0;
        public ActionState ActionState => Phase == VendettaPhase.Ready ? ActionState.Ready :
            Phase == VendettaPhase.SwordThrow ? ActionState.Windup : ActionState.Active;
        public bool UseFullVelocity => Phase == VendettaPhase.Flight;
        void Ready() { if (actor == null) actor = GetComponent<ActorSimulation>(); }
        public bool Accepts(PlayerCommand command)
        {
            Ready();
            if (tuning == null) return false;
            if (command.Envelope.CommandType == CommandType.DirectControl)
            {
                var d = command.Direct;
                return CommandValidation.IsFinite(d.MoveAxes.x) && CommandValidation.IsFinite(d.MoveAxes.y) &&
                    d.MoveAxes.sqrMagnitude <= 1.01f && CommandValidation.IsFinite(d.AimYawPitch.x) &&
                    CommandValidation.IsFinite(d.AimYawPitch.y) && Mathf.Abs(d.AimYawPitch.y) <= 85 &&
                    (d.HeldButtons & ~ControlButtons.Jump) == 0 && (d.PressedButtons & ~ControlButtons.Jump) == 0;
            }
            if (command.Envelope.CommandType == CommandType.Stop) return true;
            return command.Envelope.CommandType == CommandType.Action &&
                CommandValidation.IsFinite(command.Action.TargetPoint) &&
                (command.Action.ActionId == ShiftAction || command.Action.ActionId == EAction);
        }
        public void Receive(PlayerCommand command)
        {
            Ready();
            if (!Accepts(command)) return;
            if (command.Envelope.CommandType == CommandType.Stop) { move = Vector2.zero; jump = false; return; }
            if (command.Envelope.CommandType == CommandType.DirectControl)
            {
                move = command.Direct.MoveAxes; aim = command.Direct.AimYawPitch;
                jump |= (command.Direct.PressedButtons & ControlButtons.Jump) != 0;
                inputAge = 0; return;
            }
            if (Phase != VendettaPhase.Ready) return;
            if (command.Action.ActionId == ShiftAction && ShiftRemaining <= 0)
            {
                dashDirection = Quaternion.Euler(0, aim.x, 0) * Vector3.forward;
                Phase = VendettaPhase.Dash; phaseTime = 0; ShiftRemaining = tuning.shiftCooldown;
                attackId++; hitActors.Clear(); jump = false;
            }
            else if (command.Action.ActionId == EAction && ERemaining <= 0)
            {
                Vector3 direction = command.Action.TargetPoint - actor.AimPoint;
                if (direction.sqrMagnitude < .001f) return;
                direction.Normalize();
                // Sweep the character capsule to choose a reachable flight endpoint; never teleport through cover.
                var body = GetComponent<CharacterController>();
                Vector3 center = transform.position + body.center;
                float half = Mathf.Max(0, body.height * .5f - body.radius);
                int mask = LayerMask.GetMask("World", "NavigationSurface");
                float distance = tuning.swordDistance;
                if (Physics.CapsuleCast(center + Vector3.up * half, center - Vector3.up * half,
                    body.radius + .03f, direction, out var hit, distance, mask, QueryTriggerInteraction.Ignore))
                    distance = Mathf.Max(0, hit.distance - .05f);
                flightDestination = transform.position + direction * distance;
                swordStart = actor.AimPoint;
                // The actor's hand/aim anchor meets the sword, while its capsule base stays on the ground.
                SwordDestination = flightDestination + Vector3.up;
                SwordPosition = swordStart;
                Phase = VendettaPhase.SwordThrow; phaseTime = 0; blockedTime = 0;
                ERemaining = tuning.eCooldown; jump = false;
            }
        }
        public Vector3 DesiredVelocity(float dt)
        {
            Ready();
            ShiftRemaining = Mathf.Max(0, ShiftRemaining - dt);
            ERemaining = Mathf.Max(0, ERemaining - dt);
            inputAge += dt; phaseTime += dt;
            if (inputAge > tuning.inputTimeout) move = Vector2.zero;
            actor.Motor.Face(Quaternion.Euler(0, aim.x, 0) * Vector3.forward, 100000, dt);
            if (Phase == VendettaPhase.Dash) return dashDirection * tuning.dashDistance / Mathf.Max(.02f,tuning.dashSeconds);
            if (Phase == VendettaPhase.Spin) return Vector3.zero;
            if (Phase == VendettaPhase.SwordThrow)
            {
                SwordPosition = Vector3.Lerp(swordStart, SwordDestination, PhaseProgress);
                if (phaseTime >= tuning.swordThrowSeconds)
                {
                    SwordPosition = SwordDestination; Phase = VendettaPhase.Flight; phaseTime = 0;
                    previousPosition = transform.position;
                }
                else return Vector3.zero;
            }
            if (Phase == VendettaPhase.Flight)
            {
                Vector3 delta = flightDestination - transform.position;
                if (delta.magnitude <= tuning.arrivalDistance) { FinishFlight(); return Vector3.zero; }
                return delta.normalized * Mathf.Min(tuning.flightSpeed, delta.magnitude / Mathf.Max(dt,.0001f));
            }
            if (jump) { actor.Motor.Jump(tuning.jumpSpeed); jump = false; }
            return Quaternion.Euler(0, aim.x, 0) * new Vector3(move.x, 0, move.y) * tuning.moveSpeed;
        }
        public void AfterMove(float dt, uint tick)
        {
            if (Phase == VendettaPhase.Dash && phaseTime >= tuning.dashSeconds)
            { Phase = VendettaPhase.Spin; phaseTime = 0; }
            if (Phase == VendettaPhase.Spin)
            {
                SpinHit();
                if (phaseTime >= tuning.spinSeconds) { Phase = VendettaPhase.Ready; phaseTime = 0; }
            }
            if (Phase != VendettaPhase.Flight) return;
            if ((transform.position - previousPosition).sqrMagnitude < .00001f) blockedTime += dt; else blockedTime = 0;
            previousPosition = transform.position;
            if (Vector3.Distance(transform.position, flightDestination) <= tuning.arrivalDistance ||
                blockedTime > .15f || phaseTime > tuning.swordDistance / Mathf.Max(.1f,tuning.flightSpeed) + 1)
                FinishFlight();
        }
        void FinishFlight()
        {
            Phase = VendettaPhase.Ready; phaseTime = 0; actor.Motor.ClearVerticalVelocity();
            // No landing damage, secondary slash, stun or forced jump has been specified.
        }
        void SpinHit()
        {
            foreach (var target in actor.World.Actors)
            {
                if (target == null || target == actor || !target.Health.IsAlive || target.team == actor.team ||
                    hitActors.Contains(target.Id)) continue;
                Vector3 delta = target.AimPoint - actor.AimPoint;
                if (delta.sqrMagnitude > tuning.spinRadius * tuning.spinRadius ||
                    Physics.Linecast(actor.AimPoint, target.AimPoint, actor.World.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                hitActors.Add(target.Id);
                actor.World.Damage.Apply(new DamageRequest(actor.Id,target.Id,attackId,tuning.spinDamage),target);
            }
        }
        public void Stop()
        {
            move = Vector2.zero; jump = false;
            Phase = VendettaPhase.Ready; phaseTime = 0;
            if (actor != null && actor.Motor != null) actor.Motor.ClearVerticalVelocity();
        }
    }
}
