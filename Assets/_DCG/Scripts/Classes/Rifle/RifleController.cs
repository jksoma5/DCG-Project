using System;
using System.Collections.Generic;
using DCG.Core;
using DCG.Gameplay;
using UnityEngine;

namespace DCG.Classes.Rifle
{
    public enum RifleStance { Stand, Crouch, Prone }
    public enum RifleAimMode { Hip, Shoulder, Ads }
    public enum RifleFireMode { Single, Auto }

    public sealed class RifleController : MonoBehaviour, IActorActionPolicy, IActorAmmoSource
    {
        public const int ToggleCrouch = 1, ToggleProne = 2, ToggleFireMode = 3;
        public RifleTuning tuning;
        ActorSimulation actor;
        CharacterController body;
        CapsuleCollider hurtbox;
        DirectControlFrame frame;
        ControlButtons edges;
        float inputAge, shotRemaining;
        bool initialized;
        ulong attackId;
        readonly List<Bullet> bullets = new List<Bullet>();
        struct Bullet { public Vector3 position, velocity; public float distance; public ulong id; }
        public event Action<Vector3, Vector3> Tracer;
        public int Ammo { get; private set; }
        public int Reserve { get; private set; }
        public int ShotsFired { get; private set; }
        public float ReloadRemaining { get; private set; }
        public float Recoil { get; private set; }
        public float HitMarkerRemaining { get; private set; }
        public RifleStance Stance { get; private set; }
        public RifleAimMode AimMode { get; private set; }
        public RifleFireMode FireMode { get; private set; } = RifleFireMode.Auto;
        public bool Sprinting { get; private set; }
        public float Lean { get; private set; }
        public int Shoulder { get; private set; } = 1;
        public float Height => body != null ? body.height : tuning.standingHeight;
        public Vector3 Eye => transform.position + Vector3.up * (Height - .16f) + transform.right * Lean * .22f;
        public Vector3 Muzzle => Eye + transform.right * .18f + AimRotation * Vector3.forward * .5f - Vector3.up * .1f;
        public Quaternion AimRotation => Quaternion.Euler(frame.AimYawPitch.y - Recoil, frame.AimYawPitch.x, 0);
        public ActionState ActionState => ReloadRemaining > 0 ? ActionState.Reload :
            shotRemaining > 0 ? ActionState.Recovery : ActionState.Ready;
        public bool UseFullVelocity => false;

        void Ensure()
        {
            if (initialized || tuning == null) return;
            actor = GetComponent<ActorSimulation>(); body = GetComponent<CharacterController>();
            hurtbox = GetComponentInChildren<CapsuleCollider>();
            Ammo = tuning.magazineSize; Reserve = tuning.reserveAmmo; initialized = true;
        }
        void Awake() { Ensure(); }
        public bool Accepts(PlayerCommand command)
        {
            if (tuning == null) return false;
            if (command.Envelope.CommandType == CommandType.Stop) return true;
            if (command.Envelope.CommandType == CommandType.Action)
                return command.Action.ActionId >= ToggleCrouch && command.Action.ActionId <= ToggleFireMode;
            if (command.Envelope.CommandType != CommandType.DirectControl) return false;
            var d = command.Direct;
            const ControlButtons allowed = ControlButtons.Fire | ControlButtons.Aim | ControlButtons.Ads |
                ControlButtons.Reload | ControlButtons.Jump | ControlButtons.Sprint | ControlButtons.Walk |
                ControlButtons.LeanLeft | ControlButtons.LeanRight;
            return CommandValidation.IsFinite(d.MoveAxes.x) && CommandValidation.IsFinite(d.MoveAxes.y) &&
                d.MoveAxes.sqrMagnitude <= 1.01f && CommandValidation.IsFinite(d.AimYawPitch.x) &&
                CommandValidation.IsFinite(d.AimYawPitch.y) && Mathf.Abs(d.AimYawPitch.y) <= 85 &&
                CommandValidation.IsFinite(d.AimTarget) &&
                (d.AimTarget == Vector3.zero || Vector3.Distance(d.AimTarget, transform.position) <= tuning.range + 10) &&
                (d.HeldButtons & ~allowed) == 0 && (d.PressedButtons & ~allowed) == 0;
        }
        public void Receive(PlayerCommand command)
        {
            Ensure();
            if (!Accepts(command)) return;
            if (command.Envelope.CommandType == CommandType.Stop) { ClearInput(); return; }
            if (command.Envelope.CommandType == CommandType.DirectControl)
            {
                frame = command.Direct; edges |= frame.PressedButtons; inputAge = 0; return;
            }
            switch (command.Action.ActionId)
            {
                case ToggleCrouch: SetStance(Stance == RifleStance.Crouch ? RifleStance.Stand : RifleStance.Crouch); break;
                case ToggleProne: SetStance(Stance == RifleStance.Prone ? RifleStance.Stand : RifleStance.Prone); break;
                case ToggleFireMode: FireMode = FireMode == RifleFireMode.Auto ? RifleFireMode.Single : RifleFireMode.Auto; break;
            }
        }
        void SetStance(RifleStance next)
        {
            float height = next == RifleStance.Stand ? tuning.standingHeight :
                next == RifleStance.Crouch ? tuning.crouchHeight : tuning.proneHeight;
            // Check only the volume above the existing capsule before standing up.
            if (height > body.height)
            {
                float radius = body.radius * .95f;
                Vector3 lower = transform.position + Vector3.up * (body.height - radius + .03f);
                Vector3 upper = transform.position + Vector3.up * (height - radius);
                if (Physics.CheckCapsule(lower, upper, radius, LayerMask.GetMask("World"),
                    QueryTriggerInteraction.Ignore)) return;
            }
            body.height = height; body.center = Vector3.up * height * .5f;
            if (hurtbox != null) { hurtbox.height = height; hurtbox.center = body.center; }
            Stance = next;
        }
        bool Held(ControlButtons button) => (frame.HeldButtons & button) != 0;
        bool Pressed(ControlButtons button) => (edges & button) != 0;
        public Vector3 DesiredVelocity(float dt)
        {
            Ensure();
            inputAge += dt;
            if (inputAge > tuning.inputTimeout) ClearInput();
            shotRemaining = Mathf.Max(-dt, shotRemaining - dt);
            Recoil = Mathf.MoveTowards(Recoil, 0, tuning.recoilRecovery * dt);
            HitMarkerRemaining = Mathf.Max(0, HitMarkerRemaining - dt);
            if (ReloadRemaining > 0)
            {
                ReloadRemaining = Mathf.Max(0, ReloadRemaining - dt);
                if (ReloadRemaining <= 0)
                {
                    int amount = Mathf.Min(tuning.magazineSize - Ammo, Reserve);
                    Ammo += amount; Reserve -= amount;
                }
            }
            AimMode = Held(ControlButtons.Ads) ? RifleAimMode.Ads :
                Held(ControlButtons.Aim) ? RifleAimMode.Shoulder : RifleAimMode.Hip;
            Sprinting = Stance == RifleStance.Stand && AimMode == RifleAimMode.Hip &&
                Held(ControlButtons.Sprint) && frame.MoveAxes.y > .1f && !Held(ControlButtons.Fire);
            Lean = Sprinting ? 0 : (Held(ControlButtons.LeanRight) ? 1 : 0) - (Held(ControlButtons.LeanLeft) ? 1 : 0);
            Vector3 uprightEye = transform.position + Vector3.up * (Height - .16f);
            if (Lean != 0 && Physics.SphereCast(uprightEye, .15f, transform.right * Lean, out _, .25f,
                LayerMask.GetMask("World"), QueryTriggerInteraction.Ignore)) Lean = 0;
            if (hurtbox != null) hurtbox.center = body.center + Vector3.right * Lean * .22f;
            if (AimMode == RifleAimMode.Shoulder && Lean != 0) Shoulder = Lean > 0 ? 1 : -1;
            actor.Motor.Face(Quaternion.Euler(0, frame.AimYawPitch.x, 0) * Vector3.forward, 100000, dt);
            if (Pressed(ControlButtons.Jump) && Stance != RifleStance.Prone) actor.Motor.Jump(tuning.jumpSpeed);
            if (Pressed(ControlButtons.Reload) && ReloadRemaining <= 0 && Ammo < tuning.magazineSize && Reserve > 0)
                ReloadRemaining = tuning.reloadSeconds;
            float speed = Stance == RifleStance.Prone ? tuning.proneSpeed : Stance == RifleStance.Crouch ? tuning.crouchSpeed :
                Sprinting ? tuning.sprintSpeed : Held(ControlButtons.Walk) ? tuning.walkSpeed : tuning.moveSpeed;
            if (AimMode != RifleAimMode.Hip) speed = Mathf.Min(speed, tuning.aimSpeed);
            return Quaternion.Euler(0, frame.AimYawPitch.x, 0) * new Vector3(frame.MoveAxes.x, 0, frame.MoveAxes.y) * speed;
        }
        public void AfterMove(float dt, uint tick)
        {
            bool trigger = FireMode == RifleFireMode.Auto ? Held(ControlButtons.Fire) : Pressed(ControlButtons.Fire);
            if (trigger && ReloadRemaining <= 0 && shotRemaining <= 0 && Ammo > 0 && !Sprinting) Fire();
            edges = ControlButtons.None;
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                Bullet b = bullets[i];
                Vector3 segment = b.velocity * dt + Vector3.down * (.5f * tuning.bulletGravity * dt * dt);
                float length = Mathf.Min(segment.magnitude, tuning.range - b.distance);
                Vector3 end = b.position + segment.normalized * length;
                bool hit = Cast(b.position, segment.normalized, length, out var impact);
                if (hit)
                {
                    end = impact.point;
                    var target = impact.collider.GetComponentInParent<ActorSimulation>();
                    if (target != null && target != actor && target.team != actor.team &&
                        actor.World.Damage.Apply(new DamageRequest(actor.Id, target.Id, b.id, tuning.damage), target))
                        HitMarkerRemaining = .15f;
                }
                Tracer?.Invoke(b.position, end);
                if (hit || b.distance + length >= tuning.range) { bullets.RemoveAt(i); continue; }
                b.position = end; b.distance += length; b.velocity += Vector3.down * tuning.bulletGravity * dt;
                bullets[i] = b;
            }
        }
        void Fire()
        {
            Ammo--; ShotsFired++; attackId++; shotRemaining += tuning.shotInterval;
            Vector3 origin = Muzzle;
            // The muzzle must not protrude through a nearby wall.
            Vector3 muzzleOffset = origin - Eye;
            if (Cast(Eye, muzzleOffset.normalized, muzzleOffset.magnitude, out var obstruction))
            {
                Tracer?.Invoke(Eye, obstruction.point); AddRecoil(); return;
            }
            Vector3 aim = frame.AimTarget == Vector3.zero ? AimRotation * Vector3.forward : (frame.AimTarget - origin).normalized;
            float spread = AimMode == RifleAimMode.Ads ? tuning.adsSpread :
                AimMode == RifleAimMode.Shoulder ? tuning.aimSpread : tuning.hipSpread;
            if (Stance != RifleStance.Stand) spread *= .65f;
            if (frame.MoveAxes.sqrMagnitude > .01f) spread *= 1.6f;
            // Deterministic disk sample avoids changing Unity's global random state.
            float angle = ShotsFired * 2.39996323f;
            float radius = spread * Mathf.Sqrt(((ShotsFired * 37) % 101) / 100f);
            aim = Quaternion.LookRotation(aim) * Quaternion.Euler(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius, 0) * Vector3.forward;
            bullets.Add(new Bullet { position = origin, velocity = aim * tuning.bulletSpeed, id = attackId });
            AddRecoil();
        }
        void AddRecoil() { Recoil = Mathf.Min(tuning.maxRecoil, Recoil + tuning.recoilPerShot * (Stance == RifleStance.Stand ? 1 : .7f)); }
        bool Cast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default; float best = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(origin, direction, distance,
                LayerMask.GetMask("World", "NavigationSurface", "Hurtbox"), QueryTriggerInteraction.Collide))
            {
                if (hit.collider.GetComponentInParent<ActorSimulation>() == actor || hit.distance >= best) continue;
                nearest = hit; best = hit.distance;
            }
            return best < float.PositiveInfinity;
        }
        void ClearInput() { frame.MoveAxes = Vector2.zero; frame.HeldButtons = ControlButtons.None; edges = ControlButtons.None; }
        public void Stop()
        {
            ClearInput(); ReloadRemaining = 0; Recoil = 0; Sprinting = false; Lean = 0; AimMode = RifleAimMode.Hip;
            bullets.Clear();
        }
    }
}
