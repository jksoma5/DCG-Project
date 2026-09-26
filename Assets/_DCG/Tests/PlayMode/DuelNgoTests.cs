using System.Collections;
using DCG.Core;
using DCG.Networking;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCG.Tests
{
    public sealed class DuelNgoTests
    {
        [Test] public void BattlePacketsFitOneDatagramAndRoundTrip()
        {
            Assert.That(FastBufferWriter.GetWriteSize<DuelSnapshotPacket>(), Is.LessThan(1100));
            Assert.That(FastBufferWriter.GetWriteSize<DuelInputPacket>(), Is.LessThan(512));
            var value = new DuelSnapshotPacket { Round=2, Tick=80, Time=1.25,
                First=new DuelActorPacket { Actor=new ActorSnapshot { ActorId=new ActorId(1), Health=75, Position=new Vector3(2,0,4) } } };
            using (var writer = new FastBufferWriter(1200, Allocator.Temp))
            {
                writer.WriteValueSafe(value);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                { reader.ReadValueSafe(out DuelSnapshotPacket copy); Assert.That(copy.First.Actor.Position, Is.EqualTo(value.First.Actor.Position)); Assert.That(copy.Round, Is.EqualTo(2)); }
            }
        }
        [Test] public void LostSnapshotsDoNotRequireReplayAndOlderSnapshotsAreIgnored()
        {
            var buffer = new DuelInterpolation();
            buffer.Push(new DuelSnapshotPacket { Tick=1, Time=0, First=new DuelActorPacket { Actor=new ActorSnapshot { Position=Vector3.zero } } });
            Assert.That(buffer.Push(new DuelSnapshotPacket { Tick=7, Time=.1, First=new DuelActorPacket { Actor=new ActorSnapshot { Position=Vector3.right } } }), Is.True);
            Assert.That(buffer.Push(new DuelSnapshotPacket { Tick=2, Time=.02 }), Is.False);
            Assert.That(buffer.Sample(.05, 0, out var position, out _), Is.True);
            Assert.That(position.x, Is.EqualTo(.5f).Within(.01f));
        }
        [Test] public void PredictionComparesAcknowledgedHistoryRatherThanCurrentPosition()
        {
            var history = new DuelMotionHistory(); history.Record(20, new Vector3(5,0,0)); history.Record(25,new Vector3(8,0,0));
            var state = new ActorSnapshot { LastProcessedClientTick=19, LastProcessedServerTick=100, ServerTick=100, Position=new Vector3(5.1f,0,0) };
            Assert.That(history.Error(state,out var correction),Is.True);
            Assert.That(correction.x,Is.EqualTo(.1f).Within(.001f));
            history.Shift(correction); history.Error(state,out correction); Assert.That(correction.sqrMagnitude,Is.LessThan(.0001f));
        }
        [UnityTest] public IEnumerator NgoHostStartsWithoutNetworkPlayerPrefabAndShutsDown()
        {
            var go = new GameObject("NGO test"); var transport = go.AddComponent<DuelNgoTransport>();
            try
            {
                transport.CanJoin = () => true;
                Assert.That(transport.StartSession(true,"127.0.0.1",18779),Is.True);
                yield return null;
                Assert.That(transport.Manager.IsHost,Is.True);
                Assert.That(transport.Manager.NetworkConfig.PlayerPrefab,Is.Null);
                transport.StopSession();
                for(int i=0;i<30&&transport.Busy;i++) yield return null;
                Assert.That(transport.Busy,Is.False);
            }
            finally { Object.Destroy(go); }
            yield return null;
        }
    }
}
