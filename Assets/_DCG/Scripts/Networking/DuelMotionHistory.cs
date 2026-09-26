using DCG.Core;
using UnityEngine;

namespace DCG.Networking
{
    // Bounded, allocation-free prediction history. Correct the acknowledged position, not today's pose
    // against a stale server pose (which repeatedly drags a moving player backwards by their ping).
    public sealed class DuelMotionHistory
    {
        const int Capacity = 256;
        readonly uint[] ticks = new uint[Capacity];
        readonly Vector3[] positions = new Vector3[Capacity];
        readonly bool[] used = new bool[Capacity];
        public void Record(uint tick, Vector3 position)
        { int i = (int)(tick % Capacity); ticks[i] = tick; positions[i] = position; used[i] = true; }
        public bool Error(ActorSnapshot snapshot, out Vector3 correction)
        {
            uint predictedTick = snapshot.LastProcessedClientTick + 1 + (snapshot.ServerTick - snapshot.LastProcessedServerTick);
            int i = (int)(predictedTick % Capacity);
            correction = Vector3.zero;
            if (!used[i] || ticks[i] != predictedTick) return false;
            correction = snapshot.Position - positions[i];
            return CommandValidation.IsFinite(correction);
        }
        public void Shift(Vector3 correction)
        { for (int i = 0; i < Capacity; i++) if (used[i]) positions[i] += correction; }
        public void Clear() { System.Array.Clear(used, 0, Capacity); }
    }
    public sealed class DuelInterpolation
    {
        readonly System.Collections.Generic.List<DuelSnapshotPacket> samples = new System.Collections.Generic.List<DuelSnapshotPacket>(16);
        public void Clear() { samples.Clear(); }
        public bool Push(DuelSnapshotPacket value)
        {
            if (samples.Count > 0 && !CommandValidation.IsNewer(value.Tick, samples[samples.Count - 1].Tick)) return false;
            if (samples.Count == 16) samples.RemoveAt(0);
            samples.Add(value); return true;
        }
        public bool Sample(double time, int slot, out Vector3 position, out float facing)
        {
            position = default; facing = 0; if (samples.Count == 0) return false;
            int next = 0;
            while (next < samples.Count - 1 && samples[next].Time < time) next++;
            var current = samples[next]; var previous = samples[Mathf.Max(0, next - 1)];
            var a = slot == 0 ? previous.First.Actor : previous.Second.Actor;
            var b = slot == 0 ? current.First.Actor : current.Second.Actor;
            float t = current.Time > previous.Time ? Mathf.Clamp01((float)((time - previous.Time) / (current.Time - previous.Time))) : 1;
            position = Vector3.Lerp(a.Position, b.Position, t); facing = Mathf.LerpAngle(a.Facing, b.Facing, t);
            // Small bounded extrapolation bridges jitter; packet loss never starts a catch-up replay loop.
            if (time > current.Time) position += b.Velocity * Mathf.Min(.1f, (float)(time - current.Time));
            return true;
        }
    }
}
