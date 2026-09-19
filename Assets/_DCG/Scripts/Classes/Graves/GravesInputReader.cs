using System;
using DCG.Core;
using DCG.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCG.Classes.Graves
{
    public sealed class GravesInputReader : MonoBehaviour
    {
        public ActorSimulation actor;
        public Camera worldCamera;
        public InputActionAsset controls;
        public Func<Vector2, bool> IsPointerBlocked;
        public event Action<Vector2> UiClicked;
        public event Action<Vector3, bool> WorldClicked;
        public bool AttackMoveArmed { get; private set; }
        public Vector3 LastDestination { get; private set; }
        public bool HasDestination { get; private set; }
        InputActionAsset runtime;
        InputActionMap map;
        InputAction point, command, arm, confirm, stop, uiClick, uiCancel;
        uint sequence;
        bool focused = true;
        void OnEnable()
        {
            if (controls == null) return;
            runtime = Instantiate(controls);
            map = runtime.FindActionMap("Graves", true);
            point = map.FindAction("Point", true); command = map.FindAction("Command", true);
            arm = map.FindAction("AttackMove", true); confirm = map.FindAction("Confirm", true);
            stop = map.FindAction("Stop", true); map.Enable();
            var ui = runtime.FindActionMap("UI", true);
            uiClick = ui.FindAction("Click", true); uiCancel = ui.FindAction("Cancel", true); ui.Enable();
        }
        void OnDisable()
        {
            StopOrder();
            if (runtime != null) { runtime.Disable(); Destroy(runtime); }
        }
        void Update() => PollInput();
        public void PollInput()
        {
            if (!focused || map == null || actor == null || !actor.Initialized) return;
            Vector2 screen = point.ReadValue<Vector2>();
            if (uiCancel.WasPressedThisFrame()) { StopOrder(); return; }
            if (uiClick.WasPressedThisFrame() && IsPointerBlocked?.Invoke(screen) == true)
            { UiClicked?.Invoke(screen); return; }
            if (!actor.Health.IsAlive) return;
            if (stop.WasPressedThisFrame()) { StopOrder(); return; }
            if (arm.WasPressedThisFrame()) AttackMoveArmed = true;
            bool rightClick = command.WasPressedThisFrame();
            bool attackClick = AttackMoveArmed && confirm.WasPressedThisFrame();
            if (!rightClick && !attackClick) return;
            if (IsPointerBlocked?.Invoke(screen) == true) return;
            Ray ray = worldCamera.ScreenPointToRay(screen);
            int mask = LayerMask.GetMask("NavigationSurface", "World", "Hurtbox");
            if (!Physics.Raycast(ray, out var hit, 300, mask, QueryTriggerInteraction.Collide)) return;
            Vector3 clickedPoint = hit.point; clickedPoint.y = 0;
            var target = hit.collider.GetComponentInParent<ActorSimulation>();
            if (target != null && target != actor && target.team != actor.team && target.Health.IsAlive)
            {
                Send(new PlayerCommand { Envelope = Envelope(CommandType.Target),
                    Target = new TargetCommand { TargetActorId = target.Id, OrderType = OrderType.AttackTarget } });
            }
            else
            {
                var nearest = attackClick ? TargetTracker.AcquireClosestToPoint(actor, clickedPoint) : null;
                if (nearest != null)
                    Send(new PlayerCommand { Envelope = Envelope(CommandType.Target),
                        Target = new TargetCommand { TargetActorId = nearest.Id, OrderType = OrderType.AttackTarget } });
                else
                    Send(new PlayerCommand { Envelope = Envelope(attackClick ? CommandType.AttackMove : CommandType.MoveTo),
                        Move = new MoveToCommand { Destination = clickedPoint } });
                LastDestination = clickedPoint; HasDestination = true;
            }
            WorldClicked?.Invoke(clickedPoint, attackClick);
            AttackMoveArmed = false;
        }
        CommandEnvelope Envelope(CommandType type) => new CommandEnvelope {
            ActorId = actor.Id, Sequence = ++sequence, ClientTick = actor.World.Tick, CommandType = type
        };
        void Send(PlayerCommand value) { actor.World.Session.Submit(actor.Id, value); }
        public void StopOrder()
        {
            AttackMoveArmed = false; HasDestination = false;
            if (actor != null && actor.Initialized && actor.World != null)
                Send(new PlayerCommand { Envelope = Envelope(CommandType.Stop) });
        }
        void OnApplicationFocus(bool value) { focused = value; if (!value) StopOrder(); }
        void OnApplicationPause(bool value) { if (value) StopOrder(); }
    }
}
