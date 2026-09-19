using System.Collections.Generic;
using DCG.Core;
using DCG.Gameplay;
using UnityEngine;

namespace DCG.Bootstrap.Hub
{
    // Shared bookkeeping for a control module: spawn tracking, registration and teardown.
    // A module that forgets to clean up is the failure this class exists to prevent, so
    // everything a module creates goes through Spawn/Track and is released in Deactivate.
    public abstract class ControlModuleBase : MonoBehaviour, IControlModule
    {
        public abstract ClassId Id { get; }
        public abstract string DisplayName { get; }
        public virtual string Summary => string.Empty;
        public virtual HubCursorMode RequiredCursor => HubCursorMode.Locked;
        public virtual TickPolicy RequiredTick => TickPolicy.Automatic;
        public bool Active { get; private set; }

        protected HubContext Context { get; private set; }
        readonly List<GameObject> spawned = new List<GameObject>();
        readonly List<ActorSimulation> registered = new List<ActorSimulation>();
        readonly List<Component> attached = new List<Component>();

        public void Activate(HubContext context)
        {
            if (Active) return;
            Context = context;
            Active = true;
            OnActivate();
        }

        public void Deactivate()
        {
            if (!Active) return;
            OnDeactivate();
            // Components on the shared camera go first and are disabled before destruction so their
            // OnDisable runs now, not at the end of the frame. Input readers release their action maps there.
            for (int i = attached.Count - 1; i >= 0; i--)
            {
                var component = attached[i];
                if (component == null) continue;
                if (component is Behaviour behaviour) behaviour.enabled = false;
                Destroy(component);
            }
            attached.Clear();
            foreach (var actor in registered)
                if (actor != null && Context != null) Context.World.Unregister(actor);
            registered.Clear();
            for (int i = spawned.Count - 1; i >= 0; i--)
                if (spawned[i] != null) Destroy(spawned[i]);
            spawned.Clear();
            Active = false;
            Context = null;
        }

        public virtual void ResetState() { }
        public virtual void TickFixed() { }
        public virtual void UpdateView(float deltaTime) { }
        public virtual void DrawHud() { }

        protected abstract void OnActivate();
        protected virtual void OnDeactivate() { }

        // Instantiates and remembers an object so Deactivate can remove it.
        protected GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var instance = Instantiate(prefab, position, rotation);
            spawned.Add(instance);
            return instance;
        }

        protected GameObject Track(GameObject instance)
        {
            if (instance != null) spawned.Add(instance);
            return instance;
        }

        // Adds a component to the shared camera (or any object the hub owns) and remembers it.
        protected T Attach<T>(GameObject host) where T : Component
        {
            var component = host.AddComponent<T>();
            attached.Add(component);
            return component;
        }

        // Registers an actor with a hub-issued id. Ids are never hard-coded in a module.
        protected void Register(ActorSimulation actor)
        {
            actor.actorNumber = Context.NextActorId();
            Context.World.Register(actor);
            registered.Add(actor);
        }
    }
}
