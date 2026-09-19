using System.Collections.Generic;
using DCG.Core;
using UnityEngine;

namespace DCG.Gameplay
{
    public sealed class SimulationWorld : MonoBehaviour
    {
        readonly List<ActorSimulation> actors = new List<ActorSimulation>();
        public IReadOnlyList<ActorSimulation> Actors => actors;
        public LocalSession Session { get; private set; }
        public DamageSystem Damage { get; } = new DamageSystem();
        public uint Tick { get; private set; }
        public bool AutomaticTicks = true;
        public int WorldMask => LayerMask.GetMask("World");
        public int ShotMask => LayerMask.GetMask("World", "Hurtbox");
        void Awake() { Session = new LocalSession(this); }
        public void Register(ActorSimulation actor)
        {
            if (actors.Contains(actor)) return;
            if (!actor.Id.IsValid || Find(actor.Id) != null)
                throw new System.InvalidOperationException("Actor ID must be unique and nonzero.");
            actors.Add(actor);
            actors.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
            actor.Initialize(this);
        }
        // The hub spawns and despawns a module's actors on every class switch, so registration has to be
        // reversible. Without this the list keeps destroyed entries and an id can never be reused.
        public void Unregister(ActorSimulation actor)
        {
            actors.Remove(actor);
            actors.RemoveAll(a => a == null);
        }
        public void UnregisterAll()
        {
            actors.Clear();
        }
        public ActorSimulation Find(ActorId id) => actors.Find(a => a != null && a.Id == id);
        void FixedUpdate() { if (AutomaticTicks) Step(Time.fixedDeltaTime); }
        public void Step(float deltaTime)
        {
            Tick++;
            Session.Drain();
            foreach (var actor in actors) if (actor != null) actor.Step(deltaTime, Tick);
        }
    }
}
