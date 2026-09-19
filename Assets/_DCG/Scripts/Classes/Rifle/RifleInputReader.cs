using System;
using DCG.Core;
using DCG.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DCG.Classes.Rifle
{
    public sealed class RifleInputReader : MonoBehaviour
    {
        public ActorSimulation actor;
        public RifleController rifle;
        public Camera worldCamera;
        public InputActionAsset controls;
        public Action ResetRequested;
        public Vector2 Aim { get; private set; }
        public Vector2 FreeLook { get; private set; }
        public bool Captured { get; private set; }
        public RifleAimGesture Gesture { get; } = new RifleAimGesture();
        InputActionAsset runtime;
        InputActionMap map;
        uint sequence;
        void OnEnable()
        {
            if (controls == null) return;
            runtime = Instantiate(controls); map = runtime.FindActionMap("Rifle", true); map.Enable();
            Capture();
        }
        void Capture() { Captured = true; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        CommandEnvelope Envelope(CommandType type) => new CommandEnvelope {
            ActorId = actor.Id, Sequence = ++sequence, ClientTick = actor.World.Tick, CommandType = type };
        void Update()
        {
            if (map == null || actor == null || !actor.Initialized) return;
            if (map["Reset"].WasPressedThisFrame()) { ResetRequested?.Invoke(); return; }
            if (map["ReleaseCursor"].WasPressedThisFrame()) { Release(); return; }
            if (!Captured)
            {
                if (map["Fire"].WasPressedThisFrame()) Capture();
                return;
            }
            if (!actor.Health.IsAlive) return;
            var tune = rifle.tuning;
            bool free = map["FreeLook"].IsPressed() && !Gesture.Ads && !Gesture.Shoulder;
            Vector2 delta = map["Look"].ReadValue<Vector2>() * (Gesture.Ads ? tune.adsSensitivity : tune.mouseSensitivity);
            if (free) FreeLook = new Vector2(Mathf.Clamp(FreeLook.x + delta.x, -130, 130), Mathf.Clamp(FreeLook.y - delta.y, -45, 45));
            else
            {
                FreeLook = Vector2.MoveTowards(FreeLook, Vector2.zero, Time.deltaTime * 360);
                Aim = new Vector2(Mathf.Repeat(Aim.x + delta.x, 360), Mathf.Clamp(Aim.y - delta.y, -70, 75));
            }
            Gesture.Step(map["Aim"].WasPressedThisFrame(), map["Aim"].WasReleasedThisFrame(),
                map["Aim"].IsPressed(), Time.unscaledTime, tune.aimHoldThreshold);
            ControlButtons held = 0, pressed = 0;
            if (!free && map["Fire"].IsPressed()) held |= ControlButtons.Fire;
            if (!free && map["Fire"].WasPressedThisFrame()) pressed |= ControlButtons.Fire;
            if (Gesture.Ads) held |= ControlButtons.Ads;
            else if (Gesture.Shoulder) held |= ControlButtons.Aim;
            if (map["Sprint"].IsPressed()) held |= ControlButtons.Sprint;
            if (map["Walk"].IsPressed()) held |= ControlButtons.Walk;
            if (map["LeanLeft"].IsPressed()) held |= ControlButtons.LeanLeft;
            if (map["LeanRight"].IsPressed()) held |= ControlButtons.LeanRight;
            if (map["Jump"].WasPressedThisFrame()) pressed |= ControlButtons.Jump;
            if (map["Reload"].WasPressedThisFrame()) pressed |= ControlButtons.Reload;
            Vector3 target = worldCamera.transform.position + worldCamera.transform.forward * (tune.range - 5);
            float closest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(worldCamera.transform.position, worldCamera.transform.forward, tune.range - 5,
                LayerMask.GetMask("World", "NavigationSurface", "Hurtbox"), QueryTriggerInteraction.Collide))
            {
                if (hit.collider.GetComponentInParent<ActorSimulation>() == actor || hit.distance >= closest) continue;
                target = hit.point; closest = hit.distance;
            }
            actor.World.Session.Submit(actor.Id, new PlayerCommand {
                Envelope = Envelope(CommandType.DirectControl),
                Direct = new DirectControlFrame { MoveAxes = Vector2.ClampMagnitude(map["Move"].ReadValue<Vector2>(), 1),
                    AimYawPitch = Aim, AimTarget = target, HeldButtons = held, PressedButtons = pressed }
            });
            if (map["Crouch"].WasPressedThisFrame()) Action(RifleController.ToggleCrouch);
            if (map["Prone"].WasPressedThisFrame()) Action(RifleController.ToggleProne);
            if (map["FireMode"].WasPressedThisFrame()) Action(RifleController.ToggleFireMode);
        }
        void Action(int id) => actor.World.Session.Submit(actor.Id, new PlayerCommand {
            Envelope = Envelope(CommandType.Action), Action = new ActionCommand { ActionId = id } });
        public void Release()
        {
            Captured = false; Gesture.Reset(); FreeLook = Vector2.zero;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (actor != null && actor.Initialized && actor.World != null)
                actor.World.Session.Submit(actor.Id, new PlayerCommand { Envelope = Envelope(CommandType.Stop) });
        }
        void OnApplicationFocus(bool focus) { if (!focus) Release(); }
        void OnDisable() { Release(); if (runtime != null) { runtime.Disable(); Destroy(runtime); } }
    }
}
