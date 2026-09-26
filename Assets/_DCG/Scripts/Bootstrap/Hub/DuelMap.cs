using DCG.Gameplay.Navigation;
using UnityEngine;

namespace DCG.Bootstrap.Hub
{
    public sealed class DuelMap : MonoBehaviour
    {
        public GridGraph Grid { get; private set; }
        Material floorMaterial, coverMaterial;
        public void Build(int id)
        {
            if (id < 0 || id > 1) throw new System.ArgumentOutOfRangeException(nameof(id));
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            floorMaterial = new Material(shader) { color = id == 0 ? new Color(.17f, .25f, .28f) : new Color(.29f, .23f, .17f) };
            coverMaterial = new Material(shader) { color = id == 0 ? new Color(.4f, .63f, .67f) : new Color(.64f, .48f, .28f) };
            Box("Floor", new Vector3(0, -.25f, 0), new Vector3(36, .5f, 28), "NavigationSurface", floorMaterial);
            Box("West", new Vector3(-18, 2, 0), new Vector3(1, 4, 29), "World", coverMaterial);
            Box("East", new Vector3(18, 2, 0), new Vector3(1, 4, 29), "World", coverMaterial);
            Box("North", new Vector3(0, 2, 14), new Vector3(36, 4, 1), "World", coverMaterial);
            Box("South", new Vector3(0, 2, -14), new Vector3(36, 4, 1), "World", coverMaterial);
            if (id == 0)
            {
                foreach (float x in new[] { -5f, 5f })
                    foreach (float z in new[] { -6f, 6f })
                        Box("Pillar", new Vector3(x, 1.5f, z), new Vector3(2, 3, 2), "World", coverMaterial);
            }
            else
            {
                Box("Middle cover", new Vector3(0, 1, 0), new Vector3(2, 2, 7), "World", coverMaterial);
                Box("North cover", new Vector3(-6, .65f, 6), new Vector3(7, 1.3f, 2), "World", coverMaterial);
                Box("South cover", new Vector3(6, .65f, -6), new Vector3(7, 1.3f, 2), "World", coverMaterial);
            }
            Grid = ScriptableObject.CreateInstance<GridGraph>();
            Grid.Initialize(72, 56, .5f, new Vector3(-18, 0, -14));
            Grid.agentRadius = .42f;
            Grid.Bake(LayerMask.GetMask("World"), LayerMask.GetMask("NavigationSurface"));
        }
        void Box(string title, Vector3 position, Vector3 scale, string layer, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = title; box.transform.SetParent(transform);
            box.transform.position = position; box.transform.localScale = scale;
            box.layer = LayerMask.NameToLayer(layer);
            box.GetComponent<Renderer>().sharedMaterial = material;
        }
        void OnDestroy()
        { if (Grid != null) Destroy(Grid); if (floorMaterial != null) Destroy(floorMaterial); if (coverMaterial != null) Destroy(coverMaterial); }
    }
}
