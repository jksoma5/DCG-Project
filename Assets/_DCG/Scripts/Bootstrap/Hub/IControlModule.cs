using DCG.Core;
using DCG.Gameplay;
using UnityEngine;

namespace DCG.Bootstrap.Hub
{
    // How the hub must drive the simulation while this module is active.
    // Automatic: SimulationWorld ticks itself in FixedUpdate, like every existing lab.
    // HubDriven: the module steps the world itself (the fighting loop needs both actors advanced together).
    public enum TickPolicy { Automatic, HubDriven }

    // Why a class is in the scene.
    // Controlled: the player is driving it, so it gets the camera, the input reader and the HUD.
    // Opponent: it stands in the same space as somebody else's opponent. Same actor, same simulation,
    // but no camera, no input and no HUD - those belong to the class being driven.
    public enum HubRole { Controlled, Opponent }

    // Cursor state this module needs. Graves points at the world with a free cursor;
    // the first and third person classes capture it.
    public enum HubCursorMode { Free, Locked }

    // The anchors of the unified map (doc 14, section 4). One map serves every class, so the places
    // that matter are named here instead of each module hunting the scene for geometry.
    // A module reads the anchors it needs and spawns its own practice targets relative to them;
    // the map itself holds no targets.
    [System.Serializable]
    public sealed class HubMapAnchors
    {
        public Transform plazaCenter;      // Flat centre of the plaza. Paul's stage, Graves' open ground.
        public Transform fightWest;        // Paul's two facing positions. The stage axis is the map's
        public Transform fightEast;        // east-west axis so screen right is always east (doc 12, 1-1).
        public Transform gravesSpawn;      // West of the plaza, inside the cover cluster and the nav grid.
        public Transform vendettaSpawn;    // Plaza centre-east, within dash reach of the collision wall.
        public Transform laneStart;        // Firing line at the mouth of the long lane. M416 and TRG.
        public Transform laneEnd;          // Backstop end of the lane. laneEnd.z is the lane's length.
        public Transform highGround;       // Raised platform south of the plaza, opposite the lane.
                                           // Vendetta's flight destination and the TRG's firing position.

        public bool Complete =>
            plazaCenter != null && fightWest != null && fightEast != null && gravesSpawn != null &&
            vendettaSpawn != null && laneStart != null && laneEnd != null && highGround != null;
    }

    // Everything a module is allowed to know about the scene it lives in.
    // Modules never reference each other and never look the scene up themselves.
    public sealed class HubContext
    {
        public SimulationWorld World;
        public Camera Camera;
        public Transform CameraTransform;
        public ControlHubController Hub;
        public HubMapAnchors Map;

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
        HubRole Role { get; }
        // Whether this class can stand in as somebody else's opponent.
        bool CanBeOpponent { get; }
        // The actor the class is playing as, which is what an opponent aims at or closes in on.
        ActorSimulation PrimaryActor { get; }

        void Activate(HubContext context, HubRole role);
        // Called by the hub once both sides of a duel exist.
        void SetOpponent(IControlModule opponent);
        void Deactivate();
        void ResetState();
        void TickFixed();
        void UpdateView(float deltaTime);
        void LateUpdateView(float deltaTime);
        void DrawHud();
    }
}
