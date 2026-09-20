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

        // The class that owns a fight plane. Only a fighter can start a duel from this screen.
        static ControlModuleBase FirstFighter(ControlHubController hub)
        {
            foreach (var module in hub.Modules)
                if (module is PaulModule) return module;
            return null;
        }

        public void Draw(ControlHubController hub)
        {
            Prepare();
            // The panel grows with the class list instead of assuming a fixed number of rows.
            int count = 0, duels = 0;
            foreach (var module in hub.Modules) if (module != null) count++;
            // A duel row for every class that can stand in as a fighter's opponent. This is the first
            // step of the real goal: every class fighting in one space (doc 14, section 11).
            var fighter = FirstFighter(hub);
            if (fighter != null)
                foreach (var module in hub.Modules)
                    if (module != null && module != fighter && module.CanBeOpponent) duels++;
            float width = 460, height = 150 + count * 74 + (duels > 0 ? 30 + duels * 30 : 0);
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
            if (duels > 0)
            {
                GUI.Label(new Rect(x + 26, row + 4, width - 52, 20), "ONE SPACE  /  fight another class", note);
                row += 26;
                foreach (var module in hub.Modules)
                {
                    if (module == null || module == fighter || !module.CanBeOpponent) continue;
                    if (GUI.Button(new Rect(x + 26, row, width - 52, 26),
                        "  " + fighter.Id + "  vs  " + module.Id, item))
                        hub.SelectDuel(fighter.Id, module.Id);
                    row += 30;
                }
            }
            GUI.Label(new Rect(x + 26, y + height - 34, width - 52, 20),
                "PROTOTYPE / TUNING PENDING / one unified map", note);
        }
    }
}
