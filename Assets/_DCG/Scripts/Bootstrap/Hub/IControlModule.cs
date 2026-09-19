using DCG.Core;
using DCG.Gameplay;
using UnityEngine;

namespace DCG.Bootstrap.Hub
{
    // How the hub must drive the simulation while this module is active.
    // Automatic: SimulationWorld ticks itself in FixedUpdate, like every existing lab.
    // HubDriven: the module steps the world itself (the fighting loop needs both actors advanced together).
    public enum TickPolicy { Automatic, HubDriven }

    // Cursor state this module needs. Graves points at the world with a free cursor;
    // the first and third person classes capture it.
    public enum HubCursorMode { Free, Locked }

    // Everything a module is allowed to know about the scene it lives in.
    // Modules never reference each other and never look the scene up themselves.
    public sealed class HubContext
    {
        public SimulationWorld World;
        public Camera Camera;
        public Transform CameraTransform;
        public ControlHubController Hub;

        // Shared map anchors. Stage 1 uses placeholder geometry; stage 2 replaces the map
        // without changing this contract.
        public Transform PlayerSpawn;
        public Transform[] TargetSpawns = new Transform[0];

        // The hub owns actor ids so two modules can never collide in SimulationWorld.
        public uint NextActorId() => Hub.NextActorId();
    }

    public interface IControlModule
    {
        ClassId Id { get; }
        string DisplayName { get; }
        string Summary { get; }
        HubCursorMode RequiredCursor { get; }
        TickPolicy RequiredTick { get; }
        bool Active { get; }

        void Activate(HubContext context);
        void Deactivate();
        void ResetState();
        void TickFixed();
        void UpdateView(float deltaTime);
        void DrawHud();
    }
}
