using DCG.Core;
using DCG.Gameplay;
using DCG.Classes.Vendetta;
using DCG.Presentation;
using UnityEngine;

namespace DCG.Bootstrap.Hub
{
    // Vendetta, ported from VendettaLabController. Shift dash into spinning slash and E sword throw
    // into flight, unchanged. Only ownership moved into the hub.
    public sealed class VendettaModule : ControlModuleBase
    {
        public GameObject actorPrefab;
        public VendettaTuning tuning;
        public PrototypeTuning common;
        public Material playerMaterial, targetMaterial, bladeMaterial;

        public override ClassId Id => ClassId.Vendetta;
        public override string DisplayName => "VENDETTA  /  dash and sword throw";
        public override string Summary => "WASD move, Space jump, Shift dash into spin, E throw and fly";

        ActorSimulation player;
        VendettaController controller;
        VendettaInputReader input;
        ThirdPersonRig rig;
        Transform heldSword, thrownSword;
        LineRenderer spinRing, tether;
        Vector3 swordLocal;
        Quaternion swordRotation;

        protected override void OnActivate()
        {
            player = SpawnActor(Context.PlayerSpawn.position, 0, playerMaterial);
            controller = player.GetComponent<VendettaController>();
            if (controller == null) controller = player.gameObject.AddComponent<VendettaController>();
            controller.tuning = tuning;
            foreach (var spawn in Context.TargetSpawns)
                if (spawn != null) SpawnActor(spawn.position, 1, targetMaterial);

            heldSword = FindSword(player.transform);
            if (heldSword != null) { swordLocal = heldSword.localPosition; swordRotation = heldSword.localRotation; }

            var host = Context.Camera.gameObject;
            Context.Camera.fieldOfView = 65;
            rig = Attach<ThirdPersonRig>(host);
            rig.target = player.transform; rig.pivotOffset = tuning.cameraPivot;
            rig.distance = tuning.cameraDistance; rig.radius = tuning.cameraRadius;

            input = Attach<VendettaInputReader>(host);
            input.actor = player; input.tuning = tuning; input.controls = Context.Hub.controls;
            input.ResetRequested = ResetState;

            thrownSword = Track(Blade("Vendetta thrown sword")).transform;
            thrownSword.gameObject.SetActive(false);
            spinRing = Line("Vendetta spin area", 65, .045f);
            tether = Line("Vendetta sword path", 2, .018f);
        }

        protected override void OnDeactivate()
        {
            if (input != null) input.Release();
            player = null; controller = null; input = null; rig = null;
            heldSword = null; thrownSword = null; spinRing = null; tether = null;
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
            actor.team = team; actor.tuning = common; actor.classId = ClassId.Vendetta;
            if (team != 0)
            {
                // A target must not run the player's skill policy. This has to be DestroyImmediate:
                // ActorSimulation.Initialize picks the first IMovementPolicy on the object during
                // Register, and a deferred Destroy would still be found there, leaving the world
                // stepping a destroyed component.
                var policy = instance.GetComponent<VendettaController>();
                if (policy != null) DestroyImmediate(policy);
            }
            var view = instance.GetComponent<ActorView>();
            if (view != null && view.body != null && material != null) view.body.sharedMaterial = material;
            Register(actor);
            return actor;
        }

        static Transform FindSword(Transform root)
        {
            foreach (Transform child in root)
                if (child.name.Contains("Palatine") || child.name.Contains("sword") || child.name.Contains("Sword"))
                    return child;
            return null;
        }

        GameObject Blade(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.localScale = new Vector3(.2f, 1.8f, .12f);
            go.layer = LayerMask.NameToLayer("LocalViewModel");
            Destroy(go.GetComponent<Collider>());
            if (bladeMaterial != null) go.GetComponent<Renderer>().sharedMaterial = bladeMaterial;
            return go;
        }

        LineRenderer Line(string name, int count, float width)
        {
            var line = Track(new GameObject(name)).AddComponent<LineRenderer>();
            line.sharedMaterial = bladeMaterial; line.positionCount = count;
            line.startWidth = line.endWidth = width; line.enabled = false;
            return line;
        }

        public override void UpdateView(float deltaTime)
        {
            if (player == null || controller == null) return;
            rig.aim = input.Aim;
            bool airborneSword = controller.Phase == VendettaPhase.SwordThrow || controller.Phase == VendettaPhase.Flight;
            if (heldSword != null) heldSword.gameObject.SetActive(!airborneSword);
            thrownSword.gameObject.SetActive(airborneSword);
            if (airborneSword)
            {
                thrownSword.position = controller.SwordPosition;
                var direction = controller.SwordDestination - player.AimPoint;
                if (direction.sqrMagnitude > .001f)
                    thrownSword.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90, 0, 0);
            }
            bool spinning = controller.Phase == VendettaPhase.Spin;
            if (heldSword != null)
            {
                heldSword.localPosition = spinning
                    ? Quaternion.Euler(0, controller.PhaseProgress * 360, 0) * new Vector3(0, 1, 1.2f) : swordLocal;
                heldSword.localRotation = spinning
                    ? Quaternion.Euler(90, controller.PhaseProgress * 360, 0) : swordRotation;
            }
            spinRing.enabled = spinning;
            if (spinning)
                for (int i = 0; i < spinRing.positionCount; i++)
                {
                    float angle = i * 2 * Mathf.PI / (spinRing.positionCount - 1);
                    spinRing.SetPosition(i, player.transform.position +
                        new Vector3(Mathf.Cos(angle), .12f, Mathf.Sin(angle)) * tuning.spinRadius);
                }
            tether.enabled = airborneSword;
            if (airborneSword) { tether.SetPosition(0, player.AimPoint); tether.SetPosition(1, controller.SwordPosition); }
        }

        public override void DrawHud()
        {
            if (controller == null) return;
            GUI.Box(new Rect(20, 20, 355, 116), "VENDETTA");
            GUI.Label(new Rect(35, 48, 330, 80),
                "WASD  Move    Mouse  Look    Space  Jump\nShift  Dash -> spinning slash\nE  Throw sword -> fly to sword -> stop");
            GUI.Box(new Rect(20, Screen.height - 100, 355, 80), string.Empty);
            GUI.Label(new Rect(35, Screen.height - 90, 330, 65),
                "SHIFT  " + controller.ShiftRemaining.ToString("0.0") + "s     E  " +
                controller.ERemaining.ToString("0.0") + "s\nState: " + controller.Phase +
                "\nTUNING PENDING / no primary, block or ultimate");
            GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 12, 20, 25), "+");
        }
    }
}
