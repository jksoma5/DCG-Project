using System.Collections.Generic;
using DCG.Core;
using DCG.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCG.Bootstrap.Hub
{
    // One scene, every control. The hub owns the things the separate labs each owned privately:
    // the camera, the cursor, the tick, the input update mode and actor ids. A module owns only its
    // own class.
    //
    // Two classes can be live at once: one the player drives, one standing in as its opponent. That is
    // what makes a fight against a class from another game possible, so the hub - not a class - drives
    // the world tick for everyone.
    public sealed partial class ControlHubController : MonoBehaviour
    {
        public SimulationWorld world;
        public UnityEngine.InputSystem.InputActionAsset controls;
        public Camera hubCamera;
        public HubMapAnchors map = new HubMapAnchors();
        public ControlModuleBase[] modules = new ControlModuleBase[0];
        public ClassSelectScreen selectScreen;
        public HubHud hud;
        public bool NetworkMode;

        public IControlModule ActiveModule { get; private set; }
        public IControlModule OpponentModule { get; private set; }
        public bool Selecting => ActiveModule == null;
        public IReadOnlyList<ControlModuleBase> Modules => modules;
        // The cursor mode the hub last applied. Cursor.lockState itself cannot be read back in a
        // batch-mode run with no window, so the declared-and-applied mode is recorded here.
        public HubCursorMode CursorMode { get; private set; }

        uint nextActorId = 1;
        HubContext context;
        float defaultFov, defaultNear, defaultFar;
        InputSettings.UpdateMode previousUpdateMode;
        bool ownsUpdateMode;

        public uint NextActorId() => nextActorId++;

        void Awake()
        {
            defaultFov = hubCamera.fieldOfView;
            defaultNear = hubCamera.nearClipPlane;
            defaultFar = hubCamera.farClipPlane;
            context = new HubContext {
                World = world, Camera = hubCamera, CameraTransform = hubCamera.transform, Hub = this,
                Map = map
            };
            if (!map.Complete)
                Debug.LogError("Control hub map anchors are incomplete. Run DCG/Generate Control Hub.", this);
            // The fighting loop needs input sampled on its own frame, which means manual event
            // processing. The hub takes that decision for the whole session instead of letting one class
            // flip it: a fight can now be running while another class is being played, so the mode
            // cannot belong to a class any more. The cost is that the classes with mouse aim are also
            // sampled on the hub's schedule, which is the trade this project chose (doc 14, section 3).
            previousUpdateMode = InputSystem.settings.updateMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            ownsUpdateMode = true;
            // Nothing in the hub scene may tick itself. The hub steps the world in its own FixedUpdate so
            // the order of the world step and a fight frame is fixed rather than left to Unity.
            world.AutomaticTicks = false;
        }

        void OnDestroy()
        {
            arenaActions?.Dispose();
            if (ownsUpdateMode) InputSystem.settings.updateMode = previousUpdateMode;
        }

        void Start() { if (NetworkMode) { ApplyCursor(HubCursorMode.Free); return; } if (ArenaMode) StartArena(ClassId.Graves); else ShowSelect(); }

        // Entering the select screen means nothing is live: no actors, no input map, no rig.
        public void ShowSelect()
        {
            if (NetworkMode) return;
            if (ArenaMode) { arenaMenu = !arenaMenu; ((ControlModuleBase)ActiveModule).SetInputEnabled(!arenaMenu); ApplyCursor(arenaMenu ? HubCursorMode.Free : ActiveModule.RequiredCursor); return; }
            if (ActiveModule != null) ((ControlModuleBase)ActiveModule).Deactivate();
            if (OpponentModule != null) ((ControlModuleBase)OpponentModule).Deactivate();
            ActiveModule = null;
            OpponentModule = null;
            // Modules set the lens they need (ADS fov, scope fov, near clip for a first person weapon).
            // The select screen gets the camera back as the generator left it.
            hubCamera.fieldOfView = defaultFov;
            hubCamera.nearClipPlane = defaultNear;
            hubCamera.farClipPlane = defaultFar;
            ApplyCursor(HubCursorMode.Free);
        }

        public bool Select(ClassId id)
        {
            if (NetworkMode) return false;
            if (ArenaMode) return SwitchCharacter(id);
            var module = Find(id);
            if (module == null) return false;
            Select(module);
            return true;
        }

        public void Select(ControlModuleBase module) { if (NetworkMode) return; if (ArenaMode) SwitchCharacter(module.Id); else Select(module, null); }

        // One class against another, in the same space. The opponent is activated first so the driven
        // class can be pointed at an actor that already exists.
        public bool SelectDuel(ClassId controlled, ClassId opponent)
        {
            if (NetworkMode) return false;
            if (ArenaMode) return false;
            var driven = Find(controlled);
            var standing = Find(opponent);
            if (driven == null || standing == null || driven == standing) return false;
            if (!standing.CanBeOpponent) return false;
            Select(driven, standing);
            return true;
        }

        void Select(ControlModuleBase module, ControlModuleBase opponent)
        {
            if (module == null) return;
            ShowSelect();
            if (opponent != null)
            {
                OpponentModule = opponent;
                opponent.Activate(context, HubRole.Opponent);
            }
            ActiveModule = module;
            module.Activate(context, HubRole.Controlled);
            if (opponent != null)
            {
                // Each side learns who it is up against only now, when both actors exist.
                module.SetOpponent(opponent);
                opponent.SetOpponent(module);
            }
            ApplyCursor(module.RequiredCursor);
        }

        ControlModuleBase Find(ClassId id)
        {
            foreach (var module in modules)
                if (module != null && module.Id == id) return module;
            return null;
        }

        public void ResetActive()
        {
            if (NetworkMode) return;
            if (ArenaMode) { ResetArena(); return; }
            var opponent = OpponentModule;
            if (opponent == null) { ActiveModule?.ResetState(); return; }
            // A duel resets as a pair: rebuilding one side alone would leave the other pointing at a
            // destroyed actor.
            var controlled = (ControlModuleBase)ActiveModule;
            SelectDuel(controlled.Id, ((ControlModuleBase)opponent).Id);
        }

        void ApplyCursor(HubCursorMode mode)
        {
            CursorMode = mode;
            bool locked = mode == HubCursorMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void FixedUpdate()
        {
            if (NetworkMode) return;
            if (ActiveModule == null) return;
            // Fresh input events for this fight frame, then the world, then whatever a class has to do
            // on its own frame loop. Fighters are skipped by the world step (ISelfSteppedPolicy) and
            // advance inside their own TickFixed, so nothing is stepped twice.
            InputSystem.Update();
            if (ArenaMode) { ReadHubKeys(); ReadArenaKeys(); }
            if (ArenaMode)
            {
                UpdateArenaEnemies();
                world.Step(Time.fixedDeltaTime);
                foreach (var module in modules) module.TickFixed();
                return;
            }
            world.Step(Time.fixedDeltaTime);
            OpponentModule?.TickFixed();
            ActiveModule.TickFixed();
        }

        void Update()
        {
            if (NetworkMode) return;
            // The readers that do their work in Update expect one input update per rendered frame, which
            // manual mode otherwise does not give them.
            InputSystem.Update();
            ReadHubKeys();
            if (ArenaMode) ReadArenaKeys();
            if (ArenaMode) { foreach (var module in modules) module.UpdateView(Time.deltaTime); }
            else { ActiveModule?.UpdateView(Time.deltaTime); OpponentModule?.UpdateView(Time.deltaTime); }
        }

        void LateUpdate()
        {
            if (NetworkMode) return;
            if (ArenaMode) { foreach (var module in modules) module.LateUpdateView(Time.deltaTime); }
            else { ActiveModule?.LateUpdateView(Time.deltaTime); OpponentModule?.LateUpdateView(Time.deltaTime); }
        }

        // Esc and F5 belong to the hub, not to a class. A module's own input reader may also use Esc
        // (Vendetta releases the cursor with it), so the hub reads the keyboard directly and keeps the
        // meaning the same in every class: Esc leaves the class, F5 resets the one that is running.
        void ReadHubKeys()
        {
            if (ArenaMode) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame && ActiveModule != null) ShowSelect();
            else if (keyboard.f5Key.wasPressedThisFrame) ResetActive();
        }

        void OnGUI()
        {
            if (NetworkMode) return;
            if (ArenaMode) { DrawArena(); return; }
            if (Selecting) selectScreen?.Draw(this);
            else hud?.Draw(this);
        }
    }
}
