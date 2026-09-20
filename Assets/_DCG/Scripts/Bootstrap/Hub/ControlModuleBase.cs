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
        public HubRole Role { get; private set; }
        public virtual bool CanBeOpponent => false;
        public virtual ActorSimulation PrimaryActor => null;
        protected bool Driven => Role == HubRole.Controlled;

        protected HubContext Context { get; private set; }
        readonly List<GameObject> spawned = new List<GameObject>();
        readonly List<ActorSimulation> registered = new List<ActorSimulation>();
        readonly List<Component> attached = new List<Component>();
        readonly List<GameObject> deferred = new List<GameObject>();

        public void Activate(HubContext context) => Activate(context, HubRole.Controlled);

        public void Activate(HubContext context, HubRole role)
        {
            if (Active) return;
            if (role == HubRole.Opponent && !CanBeOpponent)
                throw new System.InvalidOperationException(
                    DisplayName + " cannot stand in as an opponent yet.");
            Context = context;
            Role = role;
            Active = true;
            OnActivate();
        }

        public virtual void SetOpponent(IControlModule opponent) { }

        public void Deactivate()
        {
            if (!Active) return;
            OnDeactivate();
            // Objects holding a deferred component are switched off first, which runs their OnDisable
            // now rather than at the end of the frame: that is where an input reader gives back its
            // action map, the cursor and the input update mode.
            for (int i = deferred.Count - 1; i >= 0; i--)
            {
                if (deferred[i] == null) continue;
                deferred[i].SetActive(false);
                Destroy(deferred[i]);
            }
            deferred.Clear();
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
            Role = HubRole.Controlled;
            Context = null;
        }

        public virtual void ResetState() { }
        public virtual void TickFixed() { }
        public virtual void UpdateView(float deltaTime) { }
        public virtual void LateUpdateView(float deltaTime) { }
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

        // For a component whose OnEnable does the real work from its serialized fields - every input
        // reader binds its action map there. AddComponent runs OnEnable immediately, before a module
        // can assign anything, so the component is built on its own object while that object is still
        // inactive and only switched on once it is configured.
        protected T AttachDeferred<T>(string name, System.Action<T> configure) where T : Component
        {
            var host = new GameObject(name);
            host.SetActive(false);
            var component = host.AddComponent<T>();
            configure(component);
            deferred.Add(host);
            host.SetActive(true);
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
