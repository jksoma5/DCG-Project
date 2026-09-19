using DCG.Core;
using DCG.Gameplay;
using DCG.Classes.Graves;
using DCG.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DCG.Bootstrap
{
    public sealed class LabController : MonoBehaviour
    {
        public SimulationWorld world;
        public ActorSimulation player, movingTarget;
        public GravesInputReader input;
        public DebugOverlay overlay;
        public LineRenderer pathLine;
        public bool RangeIndicatorVisible => rangeIndicator != null && rangeIndicator.enabled;
        public bool ClickMarkerVisible => clickMarker != null && clickMarker.enabled;
        public Color ClickMarkerColor => clickMarker != null ? clickMarker.startColor : Color.clear;
        public Vector3 LastMarkedPoint { get; private set; }
        bool patrol;
        bool initialized;
        uint targetSequence;
        float nextOrder;
        bool patrolSide;
        LineRenderer rangeIndicator, clickMarker;
        Material indicatorMaterial, markerMaterial;
        const int CircleSegments = 64;
        void Start()
        {
            foreach (var actor in FindObjectsByType<ActorSimulation>()) world.Register(actor);
            input.IsPointerBlocked = overlay.BlocksPointer;
            input.UiClicked += overlay.HandleClick;
            input.WorldClicked += MarkWorldClick;
            rangeIndicator = CreateCircle("Attack range indicator", .055f, new Color(.2f, .88f, .73f, .9f), out indicatorMaterial);
            clickMarker = CreateCircle("Command click marker", .09f, new Color(.2f, .88f, .73f, 1), out markerMaterial);
            rangeIndicator.enabled = false;
            clickMarker.enabled = false;
            overlay.OrderDetails = () => {
                var orders = player.GetComponent<GravesOrderController>();
                return "Order " + orders.Order + "  /  Target " + orders.TargetId +
                    "\nPath " + orders.Follower.Status + "  /  Request " + orders.Follower.LatestRequest +
                    (input.AttackMoveArmed ? "  [A ready]" : "");
            };
            overlay.ResetRequested = ResetLab;
            overlay.PatrolRequested = TogglePatrol;
            overlay.PatrolActive = () => patrol;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            initialized = true;
        }
        public void ResetLab() { SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
        public void TogglePatrol()
        {
            patrol = !patrol; nextOrder = 0;
            if (!patrol && movingTarget.Health.IsAlive) SendTarget(CommandType.Stop, Vector3.zero);
        }
        void SendTarget(CommandType type, Vector3 point)
        {
            world.Session.Submit(movingTarget.Id, new PlayerCommand {
                Envelope = new CommandEnvelope { ActorId = movingTarget.Id, Sequence = ++targetSequence,
                    ClientTick = world.Tick, CommandType = type },
                Move = new MoveToCommand { Destination = point }
            });
        }
        void Update()
        {
            if (!initialized) return;
            UpdateRangeIndicator();
            if (patrol && movingTarget.Health.IsAlive && Time.time >= nextOrder)
            {
                patrolSide = !patrolSide;
                SendTarget(CommandType.MoveTo, new Vector3(patrolSide ? 7 : 12, 0, 5));
                nextOrder = Time.time + 2.5f;
            }
            var follower = player.GetComponent<GravesOrderController>().Follower;
            int remaining = follower.Points.Count - follower.Cursor;
            pathLine.positionCount = follower.HasPath ? remaining + 1 : 0;
            if (!follower.HasPath) return;
            pathLine.SetPosition(0, player.transform.position + Vector3.up * .06f);
            for (int i = 0; i < remaining; i++)
                pathLine.SetPosition(i + 1, follower.Points[follower.Cursor + i] + Vector3.up * .06f);
        }
        void UpdateRangeIndicator()
        {
            bool visible = input.AttackMoveArmed && player != null && player.Health.IsAlive;
            rangeIndicator.enabled = visible;
            if (visible) SetCircle(rangeIndicator, player.transform.position, player.tuning.attackRange, .08f);
        }
        void MarkWorldClick(Vector3 point, bool attackMove)
        {
            LastMarkedPoint = point;
            Color color = attackMove ? new Color(1f, .12f, .12f, 1f) : new Color(.2f, .88f, .73f, 1f);
            clickMarker.startColor = clickMarker.endColor = color;
            markerMaterial.color = color;
            if (markerMaterial.HasProperty("_BaseColor")) markerMaterial.SetColor("_BaseColor", color);
            SetCircle(clickMarker, point, .38f, .09f);
            clickMarker.enabled = true;
        }
        static LineRenderer CreateCircle(string name, float width, Color color, out Material material)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.positionCount = CircleSegments + 1;
            line.loop = false;
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
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
        void OnDestroy()
        {
            if (input != null)
            {
                input.UiClicked -= overlay.HandleClick;
                input.WorldClicked -= MarkWorldClick;
            }
            if (indicatorMaterial != null) Destroy(indicatorMaterial);
            if (markerMaterial != null) Destroy(markerMaterial);
        }
    }
}
