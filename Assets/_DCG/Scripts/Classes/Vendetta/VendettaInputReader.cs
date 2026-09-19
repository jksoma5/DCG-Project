using System;
using DCG.Core;
using DCG.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DCG.Classes.Vendetta
{
    public sealed class VendettaInputReader : MonoBehaviour
    {
        public ActorSimulation actor;
        public VendettaTuning tuning;
        public InputActionAsset controls;
        public Vector2 Aim { get; private set; } = new Vector2(0, 12);
        public Action ResetRequested;
        InputActionAsset runtime;
        InputActionMap map;
        uint sequence;
        public bool Captured { get; private set; }
        void OnEnable()
        {
            if (controls == null) return;
            runtime = Instantiate(controls); map = runtime.FindActionMap("Vendetta",true); map.Enable();
            Capture();
        }
        void Capture() { Captured = true; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        void Update()
        {
            if (map == null || actor == null || !actor.Initialized) return;
            if (map["Reset"].WasPressedThisFrame()) { ResetRequested?.Invoke(); return; }
            if (map["ReleaseCursor"].WasPressedThisFrame()) { Release(); return; }
            if (!Captured)
            {
                if (map["CaptureCursor"].WasPressedThisFrame()) Capture();
                return;
            }
            if (!actor.Health.IsAlive) return;
            Vector2 delta = map["Look"].ReadValue<Vector2>() * tuning.mouseSensitivity;
            Aim = new Vector2(Mathf.Repeat(Aim.x + delta.x,360),Mathf.Clamp(Aim.y - delta.y,-70,75));
            Send(new PlayerCommand {
                Envelope = Envelope(CommandType.DirectControl),
                Direct = new DirectControlFrame { MoveAxes = Vector2.ClampMagnitude(map["Move"].ReadValue<Vector2>(),1),
                    AimYawPitch = Aim, PressedButtons = map["Jump"].WasPressedThisFrame() ? ControlButtons.Jump : ControlButtons.None }
            });
            if (map["Shift"].WasPressedThisFrame()) Skill(VendettaController.ShiftAction);
            else if (map["E"].WasPressedThisFrame()) Skill(VendettaController.EAction);
        }
        void Skill(int id) => Send(new PlayerCommand {
            Envelope = Envelope(CommandType.Action),
            Action = new ActionCommand { ActionId = id, TargetPoint = actor.AimPoint +
                Quaternion.Euler(Aim.y,Aim.x,0) * Vector3.forward * tuning.swordDistance }
        });
        CommandEnvelope Envelope(CommandType kind) => new CommandEnvelope {
            ActorId = actor.Id, Sequence = ++sequence, ClientTick = actor.World.Tick, CommandType = kind
        };
        void Send(PlayerCommand command) { actor.World.Session.Submit(actor.Id,command); }
        public void Release()
        {
            Captured = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (actor != null && actor.Initialized && actor.World != null)
                Send(new PlayerCommand { Envelope = Envelope(CommandType.Stop) });
        }
        void OnApplicationFocus(bool focus) { if (!focus) Release(); }
        void OnDisable() { Release(); if (runtime != null) { runtime.Disable(); Destroy(runtime); } }
    }
}
