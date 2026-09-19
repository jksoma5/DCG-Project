using UnityEngine;

namespace DCG.Gameplay.Navigation
{
    [CreateAssetMenu(menuName = "DCG/Navigation Grid")]
    public sealed class GridGraph : ScriptableObject
    {
        public int width = 64, height = 48;
        public float cellSize = .5f;
        public Vector3 origin = new Vector3(-16, 0, -12);
        public float agentRadius = .4f;
        [SerializeField] bool[] walkable;
        public int Count => width * height;
        public bool IsBaked => walkable != null && walkable.Length == Count;
        public void Initialize(int w, int h, float size, Vector3 bottomLeft)
        {
            width = Mathf.Max(1, w); height = Mathf.Max(1, h);
            cellSize = Mathf.Max(.01f, size); origin = bottomLeft;
            walkable = new bool[Count];
            for (int i = 0; i < Count; i++) walkable[i] = true;
        }
        public bool IsWalkable(int x, int z) =>
            IsBaked && x >= 0 && z >= 0 && x < width && z < height && walkable[z * width + x];
        public bool IsWalkable(int index) => index >= 0 && index < Count && IsBaked && walkable[index];
        public void SetWalkable(int x, int z, bool value) { walkable[z * width + x] = value; }
        public Vector3 Position(int index) =>
            origin + new Vector3((index % width + .5f) * cellSize, 0, (index / width + .5f) * cellSize);
        public int Index(Vector3 point)
        {
            int x = Mathf.FloorToInt((point.x - origin.x) / cellSize);
            int z = Mathf.FloorToInt((point.z - origin.z) / cellSize);
            return x < 0 || z < 0 || x >= width || z >= height ? -1 : z * width + x;
        }
        public int NearestWalkable(Vector3 point)
        {
            int index = Index(point);
            if (IsWalkable(index)) return index;
            float distance = float.PositiveInfinity; int best = -1;
            for (int i = 0; i < Count; i++)
                if (IsWalkable(i))
                {
                    float candidate = (Position(i) - point).sqrMagnitude;
                    if (candidate < distance) { distance = candidate; best = i; }
                }
            return best;
        }
        public void Bake(int obstacleMask, int groundMask)
        {
            Initialize(width, height, cellSize, origin);
            Physics.SyncTransforms();
            for (int i = 0; i < Count; i++)
            {
                Vector3 p = Position(i);
                bool ground = Physics.Raycast(p + Vector3.up * 2, Vector3.down, 2.1f,
                    groundMask, QueryTriggerInteraction.Ignore);
                bool blocked = Physics.CheckCapsule(p + Vector3.up * (agentRadius + .05f),
                    p + Vector3.up * (1.8f - agentRadius), agentRadius, obstacleMask,
                    QueryTriggerInteraction.Ignore);
                walkable[i] = ground && !blocked;
            }
        }
    }
}
