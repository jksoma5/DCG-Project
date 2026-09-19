using System.Collections.Generic;
using UnityEngine;

namespace DCG.Gameplay.Navigation
{
    public enum PathStatus { Complete, Partial, Unreachable, BudgetExceeded }
    public sealed class PathResult
    {
        public uint RequestId;
        public PathStatus Status;
        public readonly List<Vector3> Points = new List<Vector3>();
    }
    public interface IPathService
    {
        PathResult Find(Vector3 start, Vector3 destination, uint requestId);
    }
    public sealed class AStarPathService : IPathService
    {
        readonly GridGraph grid;
        readonly int budget;
        public AStarPathService(GridGraph grid, int searchBudget = 10000)
        { this.grid = grid; budget = searchBudget; }

        public PathResult Find(Vector3 start, Vector3 destination, uint requestId)
        {
            var result = new PathResult { RequestId = requestId, Status = PathStatus.Unreachable };
            if (grid == null || !grid.IsBaked || !DCG.Core.CommandValidation.IsFinite(start) ||
                !DCG.Core.CommandValidation.IsFinite(destination)) return result;
            int from = grid.NearestWalkable(start), to = grid.NearestWalkable(destination);
            if (from < 0 || to < 0) return result;
            var parent = new int[grid.Count]; var scores = new float[grid.Count]; var closed = new bool[grid.Count];
            for (int i = 0; i < scores.Length; i++) { scores[i] = float.PositiveInfinity; parent[i] = -1; }
            var open = new MinHeap();
            scores[from] = 0; open.Push(from, Heuristic(from, to));
            int best = from, visited = 0;
            while (open.Count > 0)
            {
                int current = open.Pop();
                if (closed[current]) continue;
                if (++visited > budget) { result.Status = PathStatus.BudgetExceeded; return result; }
                closed[current] = true;
                if (Heuristic(current, to) < Heuristic(best, to)) best = current;
                if (current == to)
                {
                    result.Status = grid.Index(destination) == to ? PathStatus.Complete : PathStatus.Partial;
                    BuildPath(result, parent, from, to);
                    // End at the safe cell center; raw clicks may lie against a wall.
                    return result;
                }
                int x = current % grid.width, z = current / grid.width;
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0 || !grid.IsWalkable(x + dx, z + dz)) continue;
                    if (dx != 0 && dz != 0 &&
                        (!grid.IsWalkable(x + dx, z) || !grid.IsWalkable(x, z + dz))) continue;
                    int next = (z + dz) * grid.width + x + dx;
                    if (closed[next]) continue;
                    float cost = scores[current] + (dx != 0 && dz != 0 ? 1.41421356f : 1);
                    if (cost >= scores[next]) continue;
                    parent[next] = current; scores[next] = cost;
                    open.Push(next, cost + Heuristic(next, to));
                }
            }
            if (best != from) { result.Status = PathStatus.Partial; BuildPath(result, parent, from, best); }
            return result;
        }
        float Heuristic(int a, int b)
        {
            int dx = Mathf.Abs(a % grid.width - b % grid.width);
            int dz = Mathf.Abs(a / grid.width - b / grid.width);
            return Mathf.Max(dx, dz) + .41421356f * Mathf.Min(dx, dz);
        }
        void BuildPath(PathResult result, int[] parents, int from, int to)
        {
            for (int current = to; current >= 0; current = parents[current])
            { result.Points.Add(grid.Position(current)); if (current == from) break; }
            result.Points.Reverse();
        }
        sealed class MinHeap
        {
            readonly List<(int node, float priority)> items = new List<(int, float)>();
            public int Count => items.Count;
            public void Push(int node, float priority)
            {
                items.Add((node, priority)); int i = items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (items[parent].priority <= priority) break;
                    items[i] = items[parent]; i = parent;
                }
                items[i] = (node, priority);
            }
            public int Pop()
            {
                int node = items[0].node; var last = items[items.Count - 1];
                items.RemoveAt(items.Count - 1);
                if (items.Count == 0) return node;
                int i = 0;
                while (i * 2 + 1 < items.Count)
                {
                    int child = i * 2 + 1;
                    if (child + 1 < items.Count && items[child + 1].priority < items[child].priority) child++;
                    if (items[child].priority >= last.priority) break;
                    items[i] = items[child]; i = child;
                }
                items[i] = last; return node;
            }
        }
    }
}
