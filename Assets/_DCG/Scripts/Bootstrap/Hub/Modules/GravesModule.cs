using DCG.Core;
using DCG.Gameplay;
using DCG.Gameplay.Navigation;
using DCG.Classes.Graves;
using DCG.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCG.Bootstrap.Hub
{
    // Graves, ported from LabController. The order policy, path drawing and overlay behaviour are
    // carried over unchanged; only ownership moved. Nothing about the control itself was retuned here.
    public sealed class GravesModule : ControlModuleBase
    {
        public GameObject actorPrefab;
        public PrototypeTuning tuning;
        public GridGraph grid;
        public Material playerMaterial, targetMaterial, lineMaterial;

        // Practice targets belong to the module, not to the map (doc 14, section 4): the unified map is
        // shared, so whoever is playing creates its own set and takes it away on the way out.
        // The offsets are the ControlLab layout measured from the player, kept so the same detours,
        // path replacements and attack orders are reproducible here.
        public Vector3 movingTargetOffset = new Vector3(18, 0, 11);
        public Vector3[] staticTargetOffsets = { new Vector3(1, 0, 7), new Vector3(11, 0, 5) };
        public Vector3 patrolNearOffset = new Vector3(16, 0, 11);
        public Vector3 patrolFarOffset = new Vector3(21, 0, 11);
        public float patrolInterval = 2.5f;

        public override ClassId Id => ClassId.Graves;
        public override string DisplayName => "GRAVES  /  top-down orders";
        public override string Summary => "RMB move or attack target, A + LMB attack move, S stop";
        // The only class that points at the world with a visible cursor.
        public override HubCursorMode RequiredCursor => HubCursorMode.Free;

        ActorSimulation player, movingTarget;
        GravesInputReader input;
        DebugOverlay overlay;
        TopDownRig rig;
        LineRenderer pathLine, rangeIndicator, clickMarker;
        Material indicatorMaterial, markerMaterial;
        bool patrol, patrolSide;
        float nextPatrolOrder;
        uint patrolSequence;
        const int CircleSegments = 64;

        protected override void OnActivate()
        {
            Vector3 origin = Context.Map.gravesSpawn.position;
            player = SpawnActor(origin, 0, playerMaterial);
            movingTarget = SpawnActor(origin + movingTargetOffset, 1, targetMaterial);
            foreach (var offset in staticTargetOffsets) SpawnActor(origin + offset, 1, targetMaterial);
            patrol = false; patrolSide = false; nextPatrolOrder = 0;

            var host = Context.Camera.gameObject;
            Context.Camera.fieldOfView = tuning.cameraFov;
            Context.CameraTransform.position = player.transform.position + tuning.cameraOffset;
            Context.CameraTransform.rotation = Quaternion.LookRotation(-tuning.cameraOffset);

            rig = Attach<TopDownRig>(host);
            rig.target = player.transform; rig.tuning = tuning;

            input = Attach<GravesInputReader>(host);
            input.actor = player; input.worldCamera = Context.Camera;
            input.controls = Context.Hub.controls;

            overlay = Attach<DebugOverlay>(host);
            overlay.actor = player; overlay.worldCamera = Context.Camera;
            input.IsPointerBlocked = overlay.BlocksPointer;
            input.UiClicked += overlay.HandleClick;
            input.WorldClicked += MarkWorldClick;
            overlay.OrderDetails = OrderDetails;
            overlay.ResetRequested = ResetState;
            overlay.PatrolRequested = TogglePatrol;
            overlay.PatrolActive = () => patrol;

            pathLine = Track(NewLine("Graves active path", .055f, lineMaterial)).GetComponent<LineRenderer>();
            pathLine.positionCount = 0;
            rangeIndicator = Circle("Graves attack range", .055f, new Color(.2f, .88f, .73f, .9f), out indicatorMaterial);
            clickMarker = Circle("Graves click marker", .09f, new Color(.2f, .88f, .73f, 1f), out markerMaterial);
            rangeIndicator.enabled = false; clickMarker.enabled = false;
        }

        protected override void OnDeactivate()
        {
            if (input != null)
            {
                if (overlay != null) input.UiClicked -= overlay.HandleClick;
                input.WorldClicked -= MarkWorldClick;
            }
            if (indicatorMaterial != null) Destroy(indicatorMaterial);
            if (markerMaterial != null) Destroy(markerMaterial);
            patrol = false;
            player = null; movingTarget = null; input = null; overlay = null; rig = null;
            pathLine = null; rangeIndicator = null; clickMarker = null;
        }

        // No scene reload. The module rebuilds its own actors in place.
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
            actor.team = team; actor.tuning = tuning; actor.classId = ClassId.Graves;
            var orders = instance.GetComponent<GravesOrderController>();
            if (orders != null) orders.grid = grid;
            var view = instance.GetComponent<ActorView>();
            if (view != null && view.body != null && material != null) view.body.sharedMaterial = material;
            Register(actor);
            return actor;
        }

        string OrderDetails()
        {
            var orders = player.GetComponent<GravesOrderController>();
            if (orders == null) return string.Empty;
            return "Order " + orders.Order + "  /  Target " + orders.TargetId +
                "\nPath " + orders.Follower.Status + "  /  Request " + orders.Follower.LatestRequest +
                (input.AttackMoveArmed ? "  [A ready]" : "");
        }

        // Ported from LabController. The moving target is ordered back and forth so a path replacement
        // against a live, moving actor can be seen; the hub owns it now instead of the lab scene.
        public bool PatrolRunning => patrol;
        public ActorSimulation MovingTarget => movingTarget;

        public void TogglePatrol()
        {
            patrol = !patrol;
            nextPatrolOrder = 0;
            if (!patrol && movingTarget != null && movingTarget.Health.IsAlive)
                SendTarget(CommandType.Stop, Vector3.zero);
        }

        void SendTarget(CommandType type, Vector3 point)
        {
            Context.World.Session.Submit(movingTarget.Id, new PlayerCommand {
                Envelope = new CommandEnvelope { ActorId = movingTarget.Id, Sequence = ++patrolSequence,
                    ClientTick = Context.World.Tick, CommandType = type },
                Move = new MoveToCommand { Destination = point }
            });
        }

        void UpdatePatrol()
        {
            if (!patrol || movingTarget == null || !movingTarget.Health.IsAlive) return;
            if (Time.time < nextPatrolOrder) return;
            patrolSide = !patrolSide;
            Vector3 origin = Context.Map.gravesSpawn.position;
            SendTarget(CommandType.MoveTo, origin + (patrolSide ? patrolNearOffset : patrolFarOffset));
            nextPatrolOrder = Time.time + patrolInterval;
        }

        public override void UpdateView(float deltaTime)
        {
            if (player == null || !player.Initialized) return;
            UpdatePatrol();
            bool armed = input.AttackMoveArmed && player.Health.IsAlive;
            rangeIndicator.enabled = armed;
            if (armed) SetCircle(rangeIndicator, player.transform.position, tuning.attackRange, .08f);

            var follower = player.GetComponent<GravesOrderController>()?.Follower;
            if (follower == null) { pathLine.positionCount = 0; return; }
            int remaining = follower.Points.Count - follower.Cursor;
            pathLine.positionCount = follower.HasPath ? remaining + 1 : 0;
            if (!follower.HasPath) return;
            pathLine.SetPosition(0, player.transform.position + Vector3.up * .06f);
            for (int i = 0; i < remaining; i++)
                pathLine.SetPosition(i + 1, follower.Points[follower.Cursor + i] + Vector3.up * .06f);
        }

        void MarkWorldClick(Vector3 point, bool attackMove)
        {
            Color color = attackMove ? new Color(1f, .12f, .12f, 1f) : new Color(.2f, .88f, .73f, 1f);
            clickMarker.startColor = clickMarker.endColor = color;
            markerMaterial.color = color;
            if (markerMaterial.HasProperty("_BaseColor")) markerMaterial.SetColor("_BaseColor", color);
            SetCircle(clickMarker, point, .38f, .09f);
            clickMarker.enabled = true;
        }

        GameObject NewLine(string name, float width, Material material)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true;
            line.startWidth = line.endWidth = width;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return line.gameObject;
        }

        LineRenderer Circle(string name, float width, Color color, out Material material)
        {
            var line = Track(NewLine(name, width, null)).GetComponent<LineRenderer>();
            line.positionCount = CircleSegments + 1; line.loop = false;
            line.startColor = line.endColor = color;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { color = Color.white };
            line.sharedMaterial = material;
            return line;
        }

        static void SetCircle(LineRenderer line, Vector3 center, float radius, float height)
        {
            center.y = height;
            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
            }
        }
    }
}
