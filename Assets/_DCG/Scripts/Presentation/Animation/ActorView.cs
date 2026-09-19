using DCG.Core;
using DCG.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace DCG.Presentation
{
    public sealed class ActorView : MonoBehaviour
    {
        public ActorSimulation actor;
        public Renderer body;
        public LineRenderer tracer;
        MaterialPropertyBlock block;
        readonly List<LineRenderer> pelletTracers = new List<LineRenderer>();
        Color original;
        float tracerUntil, lastHealth;
        bool subscribed;
        void Start()
        {
            block = new MaterialPropertyBlock();
            original = body.sharedMaterial.GetColor("_BaseColor");
        }
        void Update()
        {
            if (actor == null || !actor.Initialized) return;
            if (!subscribed) { actor.Combat.Fired += Fired; subscribed = true; lastHealth = actor.Health.Current; }
            Color color = actor.Health.IsAlive ? original : new Color(.18f, .21f, .26f);
            if (actor.Health.Current < lastHealth) color = Color.white;
            body.GetPropertyBlock(block); block.SetColor("_BaseColor", color); body.SetPropertyBlock(block);
            lastHealth = actor.Health.Current;
            bool showTracers = Time.time < tracerUntil;
            for (int i = 0; i < pelletTracers.Count; i++) pelletTracers[i].enabled = showTracers;
        }
        void Fired(CombatEvent shot)
        {
            if (tracer == null) return;
            Vector3[] impacts = shot.ImpactPoints;
            int count = impacts != null && impacts.Length > 0 ? impacts.Length : 1;
            EnsureTracers(count);
            for (int i = 0; i < count; i++)
            {
                pelletTracers[i].SetPosition(0, shot.Origin);
                pelletTracers[i].SetPosition(1, impacts != null && impacts.Length > 0 ? impacts[i] : shot.ImpactPoint);
                pelletTracers[i].enabled = true;
            }
            for (int i = count; i < pelletTracers.Count; i++) pelletTracers[i].enabled = false;
            tracerUntil = Time.time + .09f; tracer.enabled = true;
        }
        void EnsureTracers(int count)
        {
            if (pelletTracers.Count == 0) pelletTracers.Add(tracer);
            while (pelletTracers.Count < count)
            {
                var copy = Instantiate(tracer.gameObject, tracer.transform.parent);
                copy.name = "Shot tracer pellet " + (pelletTracers.Count + 1);
                pelletTracers.Add(copy.GetComponent<LineRenderer>());
            }
        }
        void OnDestroy() { if (subscribed && actor != null) actor.Combat.Fired -= Fired; }
    }
}
