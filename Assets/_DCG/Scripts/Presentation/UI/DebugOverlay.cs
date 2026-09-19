using DCG.Gameplay;
using UnityEngine;

namespace DCG.Presentation
{
    // Development overlay. Receives text through a delegate to avoid a Presentation -> Classes dependency.
    public sealed class DebugOverlay : MonoBehaviour
    {
        public ActorSimulation actor;
        public Camera worldCamera;
        public System.Func<string> OrderDetails;
        public System.Action ResetRequested, PatrolRequested;
        public System.Func<bool> PatrolActive;
        GUIStyle title, text, small, badge, ammo;
        Texture2D pixel;
        public Rect Panel => new Rect(20, 20, 330, 325);
        public bool BlocksPointer(Vector2 screen) => Panel.Contains(new Vector2(screen.x, Screen.height - screen.y));
        public void HandleClick(Vector2 screen)
        {
            var point = new Vector2(screen.x, Screen.height - screen.y);
            if (new Rect(38, 295, 136, 30).Contains(point)) ResetRequested?.Invoke();
            else if (new Rect(184, 295, 148, 30).Contains(point)) PatrolRequested?.Invoke();
        }
        void Prepare()
        {
            if (pixel != null) return;
            pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply();
            title = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
            text = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            small = new GUIStyle(text) { fontSize = 12 };
            badge = new GUIStyle(text) { alignment = TextAnchor.MiddleRight };
            ammo = new GUIStyle(small) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white;
            text.normal.textColor = new Color(.8f, .85f, .9f);
            small.normal.textColor = new Color(.52f, .64f, .71f);
            badge.normal.textColor = new Color(.32f, .92f, .78f);
            ammo.normal.textColor = new Color(1f, .86f, .32f);
        }
        void Fill(Rect rect, Color color)
        { GUI.color = color; GUI.DrawTexture(rect, pixel); GUI.color = Color.white; }
        void OnGUI()
        {
            Prepare();
            Fill(Panel, new Color(.035f, .055f, .085f, .96f));
            Fill(new Rect(20, 20, 4, 325), new Color(.25f, .92f, .78f));
            GUI.Label(new Rect(38, 32, 250, 36), "DCG / CONTROL LAB", title);
            GUI.Label(new Rect(38, 70, 285, 24), "OFFLINE  /  GRAVES FOUNDATION", small);
            GUI.Label(new Rect(38, 103, 290, 63), "RMB  Move / attack target\nA + LMB  Attack move\nS  Stop", text);
            Fill(new Rect(38, 177, 294, 1), new Color(.18f, .25f, .31f));
            if (actor != null && actor.Initialized)
            {
                var state = actor.Snapshot(actor.World.Tick);
                GUI.Label(new Rect(38, 189, 295, 24), "HP " + state.Health.ToString("0") + "  |  AMMO " + state.Ammo +
                    "  |  " + state.ActionState, text);
                GUI.Label(new Rect(38, 218, 295, 54), OrderDetails?.Invoke() ?? "", small);
                GUI.Label(new Rect(38, 260, 295, 20), "Tick " + state.ServerTick + "  /  Ack " + state.LastProcessedSequence +
                    "  /  local authority", small);
            }
            // IMGUI draws only. Clicks are routed by the new Input System UI map.
            GUI.Box(new Rect(38, 295, 136, 30), "Reset lab", GUI.skin.button);
            GUI.Box(new Rect(184, 295, 148, 30), PatrolActive?.Invoke() == true ? "Stop moving target" : "Move target", GUI.skin.button);
            GUI.Label(new Rect(Screen.width - 345, 24, 320, 25), "PROTOTYPE  /  TUNING PENDING", badge);
            GUI.Label(new Rect(Screen.width - 345, 52, 320, 42), "Other classes and networking are not enabled.", small);
            if (worldCamera == null || actor == null || !actor.Initialized) return;
            foreach (var item in actor.World.Actors)
            {
                Vector3 p = worldCamera.WorldToScreenPoint(item.AimPoint + Vector3.up * .65f);
                if (p.z <= 0) continue;
                Rect bg = new Rect(p.x - 35, Screen.height - p.y, 70, 5);
                Fill(bg, new Color(.1f, .13f, .18f));
                bg.width *= item.Health.Current / item.Health.Maximum;
                Fill(bg, item.team == actor.team ? new Color(.3f, .94f, .78f) : new Color(1, .43f, .36f));
                if (item == actor)
                {
                    var state = item.Snapshot(item.World.Tick);
                    GUI.Label(new Rect(p.x - 45, Screen.height - p.y + 7, 90, 18),
                        state.Ammo + " / " + item.tuning.magazineSize, ammo);
                }
            }
        }
        void OnDestroy() { if (pixel != null) Destroy(pixel); }
    }
}
