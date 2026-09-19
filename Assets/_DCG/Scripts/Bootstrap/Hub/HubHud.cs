using UnityEngine;

namespace DCG.Bootstrap.Hub
{
    // Draws only the active module's HUD, plus the one line that belongs to the hub itself.
    // The separate labs each drew their own overlay; in one scene exactly one may draw.
    public sealed class HubHud : MonoBehaviour
    {
        GUIStyle bar;

        public void Draw(ControlHubController hub)
        {
            if (bar == null)
            {
                bar = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                bar.normal.textColor = new Color(.32f, .92f, .78f);
            }
            hub.ActiveModule?.DrawHud();
            GUI.Label(new Rect(Screen.width - 345, Screen.height - 48, 320, 20),
                hub.ActiveModule?.DisplayName + "   /   Esc  class select   /   F5  reset", bar);
            // A mouse path out of a class, not only the key. Classes that capture the cursor make the
            // key the natural exit, but a button is what can be verified without keyboard focus and is
            // what a viewer reaches for first.
            if (GUI.Button(new Rect(Screen.width - 345, Screen.height - 26, 150, 22), "Class select"))
                hub.ShowSelect();
            if (GUI.Button(new Rect(Screen.width - 185, Screen.height - 26, 150, 22), "Reset class"))
                hub.ResetActive();
        }
    }
}
