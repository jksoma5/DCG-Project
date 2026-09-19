using UnityEngine;

namespace DCG.Bootstrap.Hub
{
    // Development select screen. IMGUI on purpose: every other lab HUD in this project is IMGUI,
    // and a real UI screen is a separate decision recorded in doc 14.
    public sealed class ClassSelectScreen : MonoBehaviour
    {
        GUIStyle title, item, note;

        void Prepare()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white;
            item = new GUIStyle(GUI.skin.button) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            note = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            note.normal.textColor = new Color(.52f, .64f, .71f);
        }

        public void Draw(ControlHubController hub)
        {
            Prepare();
            // The panel grows with the class list instead of assuming a fixed number of rows.
            int count = 0;
            foreach (var module in hub.Modules) if (module != null) count++;
            float width = 460, height = 150 + count * 74;
            float x = Screen.width * .5f - width * .5f, y = Screen.height * .5f - height * .5f;
            GUI.Box(new Rect(x, y, width, height), string.Empty);
            GUI.Label(new Rect(x + 26, y + 18, width - 40, 40), "DCG / CONTROL HUB", title);
            GUI.Label(new Rect(x + 26, y + 58, width - 50, 34),
                "One scene, every control. Pick a class. Esc returns here.", note);

            float row = y + 100;
            foreach (var module in hub.Modules)
            {
                if (module == null) continue;
                if (GUI.Button(new Rect(x + 26, row, width - 52, 46), "  " + module.DisplayName, item))
                    hub.Select(module);
                GUI.Label(new Rect(x + 36, row + 46, width - 62, 18), module.Summary, note);
                row += 74;
            }
            GUI.Label(new Rect(x + 26, y + height - 34, width - 52, 20),
                "PROTOTYPE / TUNING PENDING / one unified map", note);
        }
    }
}
