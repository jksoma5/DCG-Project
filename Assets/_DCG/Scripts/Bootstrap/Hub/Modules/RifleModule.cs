using DCG.Core;
using DCG.Gameplay;
using DCG.Classes.Rifle;
using DCG.Presentation;
using UnityEngine;

namespace DCG.Bootstrap.Hub
{
    // M416, ported from RifleLabController. Stance, lean, shoulder aim, ADS, fire mode and the tracer
    // are carried over as they were; what changed is who owns the camera, the view models and the
    // practice targets. No gunplay value was touched.
    public sealed class RifleModule : ControlModuleBase
    {
        public GameObject actorPrefab, adsGunPrefab;
        public RifleTuning tuning;
        public PrototypeTuning common;
        public Material targetMaterial, tracerMaterial;

        public override ClassId Id => ClassId.Rifle;
        public override string DisplayName => "M416  /  shoulder aim and ADS";
        public override string Summary => "WASD move, C crouch, Z prone, Q E lean, RMB shoulder or ADS, B fire mode";

        // Targets stand down the shared lane, measured from the firing line. The first two are the
        // RifleLab near and far distances (16 m and 36 m ahead); the third stands behind the tall
        // cover at the lane mouth, which is what the lab's covered target was for.
        public Vector3[] targetOffsets = { new Vector3(0, 0, 16), new Vector3(-5, 0, 36) };
        public Vector3 coveredTargetOffset = new Vector3(-6, 0, 8);

        ActorSimulation player;
        RifleController rifle;
        RifleInputReader input;
        ShoulderCameraRig rig;
        Transform model, gun, adsGun;
        LineRenderer tracer;
        float tracerUntil;

        protected override void OnActivate()
        {
            Vector3 line = Context.Map.laneStart.position;
            player = SpawnActor(line, 0, null);
            rifle = player.GetComponent<RifleController>();
            if (rifle == null) rifle = player.gameObject.AddComponent<RifleController>();
            rifle.tuning = tuning;
            model = player.transform.Find("Body");
            gun = player.transform.Find("M416 placeholder");
            foreach (var offset in targetOffsets) SpawnActor(line + offset, 1, targetMaterial);
            SpawnActor(line + coveredTargetOffset, 1, targetMaterial);

            var host = Context.Camera.gameObject;
            var camera = Context.Camera;
            camera.nearClipPlane = .03f;
            camera.fieldOfView = tuning.hipFov;
            Context.CameraTransform.position = player.transform.position +
                new Vector3(tuning.shoulderOffset, 1.64f, -tuning.hipDistance);

            rig = Attach<ShoulderCameraRig>(host);
            rig.viewCamera = camera;

            input = Attach<RifleInputReader>(host);
            input.actor = player; input.rifle = rifle; input.worldCamera = camera;
            input.controls = Context.Hub.controls;
            input.ResetRequested = ResetState;

            adsGun = Track(Instantiate(adsGunPrefab)).transform;
            adsGun.SetParent(Context.CameraTransform, false);
            adsGun.localPosition = new Vector3(0, -.09425f, .32f);
            adsGun.localScale = Vector3.one * .65f;
            adsGun.gameObject.SetActive(false);

            tracer = Track(new GameObject("Rifle tracer")).AddComponent<LineRenderer>();
            tracer.sharedMaterial = tracerMaterial; tracer.positionCount = 2;
            tracer.startWidth = tracer.endWidth = .025f; tracer.enabled = false;
            tracerUntil = 0;
            rifle.Tracer += ShowTracer;
        }

        protected override void OnDeactivate()
        {
            if (rifle != null) rifle.Tracer -= ShowTracer;
            if (input != null) input.Release();
            player = null; rifle = null; input = null; rig = null;
            model = null; gun = null; adsGun = null; tracer = null;
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
            actor.team = team; actor.tuning = common; actor.classId = ClassId.Rifle;
            if (team != 0)
            {
                // A target may not run the player's weapon policy. DestroyImmediate for the same reason
                // as in VendettaModule: Register picks up policies on the object at that moment.
                var policy = instance.GetComponent<RifleController>();
                if (policy != null) DestroyImmediate(policy);
                var carried = instance.transform.Find("M416 placeholder");
                if (carried != null) DestroyImmediate(carried.gameObject);
            }
            var view = instance.GetComponent<ActorView>();
            if (view != null && view.body != null && material != null) view.body.sharedMaterial = material;
            Register(actor);
            return actor;
        }

        void ShowTracer(Vector3 start, Vector3 end)
        {
            tracer.SetPosition(0, start); tracer.SetPosition(1, end); tracerUntil = Time.time + .06f;
        }

        // Ported from RifleLabController.LateUpdate. The hub calls this in LateUpdate so the camera is
        // still placed after the actor has moved, exactly as it was in the lab.
        public override void LateUpdateView(float deltaTime)
        {
            if (player == null || !player.Initialized) return;
            var tune = rifle.tuning;
            bool ads = rifle.AimMode == RifleAimMode.Ads;
            bool aim = rifle.AimMode == RifleAimMode.Shoulder;
            Quaternion rotation = rifle.AimRotation * Quaternion.Euler(input.FreeLook.y, input.FreeLook.x, 0);
            rig.Place(rifle.Eye, rotation, ads ? 0 : tune.shoulderOffset * rifle.Shoulder,
                ads ? 0 : aim ? tune.aimDistance : tune.hipDistance,
                ads ? tune.adsFov : aim ? tune.shoulderFov : tune.hipFov, ads ? -rifle.Lean * 8 : 0, deltaTime);
            if (model != null) model.gameObject.SetActive(!ads);
            if (gun != null) gun.gameObject.SetActive(!ads);
            adsGun.gameObject.SetActive(ads);
            if (model != null)
            {
                model.localPosition = new Vector3(rifle.Lean * .22f, rifle.Height * .5f, 0);
                model.localScale = rifle.Stance == RifleStance.Prone
                    ? new Vector3(.65f, .3f, 1.5f) : new Vector3(.7f, rifle.Height * .5f, .7f);
                model.localRotation = Quaternion.Euler(0, 0, -rifle.Lean * 12);
            }
            if (gun != null) gun.SetPositionAndRotation(rifle.Muzzle, rifle.AimRotation);
            tracer.enabled = Time.time < tracerUntil;
        }

        public override void DrawHud()
        {
            if (rifle == null) return;
            GUI.Box(new Rect(20, 20, 410, 174), "M416 / SHOULDER + ADS");
            GUI.Label(new Rect(34, 48, 390, 140),
                "WASD Move | Shift Sprint | Ctrl Walk | Space Jump\nC Crouch | Z Prone | Q / E Lean + shoulder\nLMB Fire | RMB hold Shoulder / tap ADS\nR Reload | B Single / Auto | Alt Free look\nEsc Class select | Click Capture | F5 Reset\nPrototype tuning / unified map lane");
            GUI.Box(new Rect(20, Screen.height - 95, 350, 75), string.Empty);
            GUI.Label(new Rect(34, Screen.height - 84, 325, 64),
                rifle.Ammo + " / " + rifle.Reserve + "   " + rifle.FireMode + "\n" +
                rifle.Stance + " / " + rifle.AimMode + (rifle.Sprinting ? " / SPRINT" : "") +
                (rifle.ReloadRemaining > 0 ? "\nRELOADING " + rifle.ReloadRemaining.ToString("0.0") : ""));
            float x = Screen.width * .5f, y = Screen.height * .5f;
            GUI.color = rifle.HitMarkerRemaining > 0 ? Color.red : Color.white;
            GUI.Label(new Rect(x - 5, y - 12, 25, 25), rifle.HitMarkerRemaining > 0 ? "X" :
                rifle.AimMode == RifleAimMode.Ads ? "." : "+");
            GUI.color = Color.white;
            if (!player.Initialized) return;
            foreach (var actor in Context.World.Actors)
            {
                if (actor == player) continue;
                var point = Context.Camera.WorldToScreenPoint(actor.AimPoint + Vector3.up);
                if (point.z > 0)
                    GUI.Label(new Rect(point.x - 35, Screen.height - point.y, 100, 25),
                        actor.Health.Current.ToString("0") + " HP");
            }
        }
    }
}
