using System.Collections.Generic;
using DCG.Core;
using DCG.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCG.Bootstrap.Hub
{
    // One scene, every control. The hub owns the things the separate labs each owned privately:
    // the camera, the cursor, the tick policy and actor ids. A module owns only its own class.
    public sealed class ControlHubController : MonoBehaviour
    {
        public SimulationWorld world;
        public UnityEngine.InputSystem.InputActionAsset controls;
        public Camera hubCamera;
        public Transform playerSpawn;
        public Transform[] targetSpawns = new Transform[0];
        public ControlModuleBase[] modules = new ControlModuleBase[0];
        public ClassSelectScreen selectScreen;
        public HubHud hud;

        public IControlModule ActiveModule { get; private set; }
        public bool Selecting => ActiveModule == null;
        public IReadOnlyList<ControlModuleBase> Modules => modules;

        uint nextActorId = 1;
        HubContext context;

        public uint NextActorId() => nextActorId++;

        void Awake()
        {
            context = new HubContext {
                World = world, Camera = hubCamera, CameraTransform = hubCamera.transform, Hub = this,
                PlayerSpawn = playerSpawn, TargetSpawns = targetSpawns
            };
        }

        void Start() { ShowSelect(); }

        // Entering the select screen means no class is live: no actors, no input map, no rig.
        public void ShowSelect()
        {
            if (ActiveModule != null)
            {
                ((ControlModuleBase)ActiveModule).Deactivate();
                ActiveModule = null;
            }
            world.AutomaticTicks = false;
            ApplyCursor(HubCursorMode.Free);
        }

        public bool Select(ClassId id)
        {
            foreach (var module in modules)
                if (module != null && module.Id == id) { Select(module); return true; }
            return false;
        }

        public void Select(ControlModuleBase module)
        {
            if (module == null) return;
            ShowSelect();
            ActiveModule = module;
            module.Activate(context);
            world.AutomaticTicks = module.RequiredTick == TickPolicy.Automatic;
            ApplyCursor(module.RequiredCursor);
        }

        public void ResetActive() { ActiveModule?.ResetState(); }

        void ApplyCursor(HubCursorMode mode)
        {
            bool locked = mode == HubCursorMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void FixedUpdate()
        {
            if (ActiveModule != null && ActiveModule.RequiredTick == TickPolicy.HubDriven)
                ActiveModule.TickFixed();
        }

        void Update()
        {
            ReadHubKeys();
            ActiveModule?.UpdateView(Time.deltaTime);
        }

        // Esc and F5 belong to the hub, not to a class. A module's own input reader may also use Esc
        // (Vendetta releases the cursor with it), so the hub reads the keyboard directly and keeps the
        // meaning the same in every class: Esc leaves the class, F5 resets the one that is running.
        void ReadHubKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame && ActiveModule != null) ShowSelect();
            else if (keyboard.f5Key.wasPressedThisFrame) ResetActive();
        }

        void OnGUI()
        {
            if (Selecting) selectScreen?.Draw(this);
            else hud?.Draw(this);
        }
    }
}
