using System.Collections.Generic;
using UnityEngine;

namespace DCG.Gameplay.Navigation
{
    public sealed class PathFollower
    {
        readonly List<Vector3> points = new List<Vector3>();
        int cursor;
        public uint LatestRequest { get; private set; }
        public PathStatus Status { get; private set; }
        public bool HasPath => cursor < points.Count;
        public IReadOnlyList<Vector3> Points => points;
        public int Cursor => cursor;
        public uint BeginRequest() { LatestRequest++; points.Clear(); cursor = 0; return LatestRequest; }
        public bool Accept(PathResult result)
        {
            if (result.RequestId != LatestRequest) return false;
            Status = result.Status; points.Clear(); points.AddRange(result.Points); cursor = 0; return true;
        }
        public void Stop() { BeginRequest(); }
        public Vector3 Velocity(Vector3 position, float speed, float arrival, float dt)
        {
            while (HasPath)
            {
                Vector3 delta = points[cursor] - position; delta.y = 0;
                if (delta.magnitude <= arrival) { cursor++; continue; }
                return delta.normalized * Mathf.Min(speed, delta.magnitude / Mathf.Max(dt, .0001f));
            }
            return Vector3.zero;
        }
    }
}
