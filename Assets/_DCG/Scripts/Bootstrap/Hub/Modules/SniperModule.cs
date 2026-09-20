using DCG.Core;
using DCG.Gameplay;
using DCG.Classes.Sniper;
using DCG.Presentation;
using UnityEngine;

namespace DCG.Bootstrap.Hub
{
    // TRG, ported from SniperLabController. Weapon slots, scope levels, draw and kick are unchanged.
    // The three first person view models come from the prefabs the sniper lab exports, so the hub is
    // not a second copy of that geometry.
    public sealed class SniperModule : ControlModuleBase
    {
        public GameObject actorPrefab;
        public GameObject[] weaponPrefabs = new GameObject[0];
        public SniperTuning tuning;
        public PrototypeTuning common;
        public Material targetMaterial, tracerMaterial, scopeMaterial;

        // A duel places the TRG here, measured from the plaza centre. The line to the fighter runs
        // north-south, across the map's east-west axis, on purpose: the fight plane is built from the two
        // participants, so a fight works on any bearing and this proves it.
        public Vector3 duelSpawnOffset = new Vector3(0, 0, 10);

        public override ClassId Id => ClassId.Sniper;
        // The TRG can stand in as a fighter's opponent: it is an ordinary actor with a hurtbox.
        public override bool CanBeOpponent => true;
        public override ActorSimulation PrimaryActor => player;
        public override string DisplayName => "TRG  /  first person sniper loadout";
        public override string Summary => "1 sniper, 2 pistol, 3 knife, RMB scope levels, LMB fire";

        // Down the shared lane from the firing line. The first three are the SniperLab distances
        // (20 m, 48 m and a covered one), the knife target is within reach, and the last stands at the
        // 100 m mark: the lane exists so the drop at that range can be read off the floor markers.
        public Vector3[] targetOffsets = {
            new Vector3(0, 0, 20), new Vector3(-4, 0, 48), new Vector3(-6, 0, 10),
            new Vector3(-4, 0, 3), new Vector3(2, 0, 88)
        };

        ActorSimulation player;
        SniperController sniper;
        SniperInputReader input;
        FirstPersonScopeRig rig;
        Transform[] weapons = new Transform[0];
        Vector3[] weaponRest = new Vector3[0];
        LineRenderer tracer;
        float tracerUntil;

        protected override void OnActivate()
        {
            bool duel = Role == HubRole.Opponent || Context.Hub.OpponentModule != null;
            // In a duel the TRG faces a class, not a target set: it stands on the plaza, not at the
            // firing line, and brings nothing to shoot at but the other fighter.
            Vector3 line = duel
                ? Context.Map.plazaCenter.position + duelSpawnOffset
                : Context.Map.laneStart.position;
            player = SpawnActor(line, 0, null);
            sniper = player.GetComponent<SniperController>();
            if (sniper == null) sniper = player.gameObject.AddComponent<SniperController>();
            sniper.tuning = tuning;
            if (!duel)
                foreach (var offset in targetOffsets) SpawnActor(line + offset, 1, targetMaterial);
            // Standing in for somebody else's opponent: same actor and same simulation, but the camera,
            // the input and the weapon view models belong to the class being driven.
            if (!Driven) return;

            var host = Context.Camera.gameObject;
            var camera = Context.Camera;
            camera.nearClipPlane = .025f;
            camera.fieldOfView = tuning.normalFov;

            rig = Attach<FirstPersonScopeRig>(host);
            rig.viewCamera = camera; rig.scopeMaterial = scopeMaterial;

            input = AttachDeferred<SniperInputReader>("TRG input", reader => {
                reader.actor = player; reader.sniper = sniper; reader.controls = Context.Hub.controls;
                reader.ResetRequested = ResetState;
            });

            weapons = new Transform[weaponPrefabs.Length];
            weaponRest = new Vector3[weaponPrefabs.Length];
            for (int i = 0; i < weaponPrefabs.Length; i++)
            {
                weapons[i] = Track(Instantiate(weaponPrefabs[i])).transform;
                weapons[i].SetParent(Context.CameraTransform, false);
                weapons[i].localPosition = new Vector3(.22f, -.22f, .5f);
                weaponRest[i] = weapons[i].localPosition;
            }

            tracer = Track(new GameObject("Sniper tracer")).AddComponent<LineRenderer>();
            tracer.sharedMaterial = tracerMaterial; tracer.positionCount = 2;
            tracer.startWidth = tracer.endWidth = .015f; tracer.enabled = false;
            tracerUntil = 0;
            sniper.Fired += ShowTracer;
        }

        protected override void OnDeactivate()
        {
            if (sniper != null) sniper.Fired -= ShowTracer;
            if (input != null) input.Release();
            player = null; sniper = null; input = null; rig = null; tracer = null;
            weapons = new Transform[0]; weaponRest = new Vector3[0];
        }

        public override void ResetState()
        {
            var hub = Context.Hub;
            Deactivate();
            hub.Select(this);
        }

        ActorSimulation SpawnActor(Vector3 at, int team, Material material)
        {
            var instance = Spawn(actorPrefab, at, Quaternion.identity);
            var actor = instance.GetComponent<ActorSimulation>();
            actor.team = team; actor.tuning = common; actor.classId = ClassId.Sniper;
            var view = instance.GetComponent<ActorView>();
            if (team != 0)
            {
                // Same rule as the other modules: a target must not carry the player's weapon policy,
                // and it has to be gone before Register looks for policies on the object.
                var policy = instance.GetComponent<SniperController>();
                if (policy != null) DestroyImmediate(policy);
                // The sniper prefab hides its own body because the player never sees it. A target is
                // nothing but a body, so it is turned back on.
                if (view != null && view.body != null)
                {
                    view.body.enabled = true;
                    if (material != null) view.body.sharedMaterial = material;
                }
            }
            Register(actor);
            return actor;
        }

        void ShowTracer(Vector3 start, Vector3 end)
        {
            tracer.SetPosition(0, start); tracer.SetPosition(1, end); tracerUntil = Time.time + .06f;
        }

        // Ported from SniperLabController.LateUpdate.
        public override void LateUpdateView(float deltaTime)
        {
            if (!Driven || player == null || !player.Initialized || rig == null) return;
            var tune = sniper.tuning;
            float fov = sniper.ScopeLevel == 2 ? tune.deepScopeFov :
                sniper.ScopeLevel == 1 ? tune.scopeFov : tune.normalFov;
            rig.Place(sniper.Eye, sniper.AimRotation, fov, sniper.ScopeLevel > 0);
            for (int i = 0; i < weapons.Length; i++)
            {
                bool active = i + 1 == (int)sniper.Weapon && sniper.ScopeLevel == 0;
                weapons[i].gameObject.SetActive(active);
                float draw = tune.drawSeconds > 0 ? sniper.DrawRemaining / tune.drawSeconds : 0;
                weapons[i].localPosition = weaponRest[i] + new Vector3(0, -draw * .25f, -sniper.Kick * .07f);
                weapons[i].localRotation = Quaternion.Euler(-sniper.Kick * 7, 0, i == 2 ? -sniper.Kick * 40 : 0);
            }
            tracer.enabled = Time.time < tracerUntil;
        }

        public override void DrawHud()
        {
            if (!Driven || sniper == null) return;
            GUI.Box(new Rect(20, 20, 425, 145), "FIRST PERSON / SNIPER LOADOUT");
            GUI.Label(new Rect(34, 49, 395, 115),
                "1 Sniper | 2 Pistol | 3 Knife\nWASD Move | Shift Walk | Ctrl Crouch | Space Jump\nLMB Fire / Slash | RMB Scope / Stab | R Reload\nEsc Class select | Click Capture | F5 Reset\nLane markers every 10 m to 100 m");
            GUI.Box(new Rect(20, Screen.height - 102, 360, 82), string.Empty);
            GUI.Label(new Rect(34, Screen.height - 92, 340, 75),
                sniper.Weapon + "   " + (sniper.Weapon == SniperWeapon.Knife ? "MELEE" :
                    sniper.Ammo + " / " + sniper.Reserve) +
                "\n" + (sniper.Crouched ? "CROUCH" : "STAND") + "  Scope " + sniper.ScopeLevel +
                (sniper.ReloadRemaining > 0 ? "  RELOAD " + sniper.ReloadRemaining.ToString("0.0") :
                 sniper.DrawRemaining > 0 ? "  EQUIP" :
                 sniper.RecoveryRemaining > 0 ? "  RECOVERY" : "  READY"));
            if (sniper.ScopeLevel == 0 && sniper.Weapon != SniperWeapon.Sniper)
                GUI.Label(new Rect(Screen.width * .5f - 5, Screen.height * .5f - 12, 20, 25), "+");
            if (sniper.HitMarkerRemaining > 0)
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(Screen.width * .5f - 5, Screen.height * .5f - 12, 20, 25), "X");
                GUI.color = Color.white;
            }
            if (!player.Initialized || sniper.ScopeLevel > 0) return;
            foreach (var actor in Context.World.Actors)
            {
                if (actor == player) continue;
                var point = Context.Camera.WorldToScreenPoint(actor.AimPoint + Vector3.up);
                if (point.z > 0)
                    GUI.Label(new Rect(point.x - 30, Screen.height - point.y, 100, 25),
                        actor.Health.Current.ToString("0") + " HP");
            }
        }
    }
}
