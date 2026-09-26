using System;
using System.Text;
using DCG.Core;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace DCG.Networking
{
    public struct DuelInputPacket : INetworkSerializeByMemcpy
    {
        public int Round;
        public PlayerCommand Command;
    }
    public struct DuelActorPacket
    {
        public ActorSnapshot Actor;
        public ReplicaState View, Reaction;
        public uint Effect;
        public Vector3 ShotOrigin, ShotImpact;
    }
    public struct DuelSnapshotPacket : INetworkSerializeByMemcpy
    {
        public int Round;
        public uint Tick;
        public double Time;
        public DuelActorPacket First, Second;
    }

    // NGO owns connection approval, timing, reliable delivery, UDP sequencing and RTT measurement.
    // Existing character simulation uses NGO custom messages rather than a second NetworkTransform.
    public sealed class DuelNgoTransport : MonoBehaviour
    {
        const string ControlChannel = "dcg/control", MotionChannel = "dcg/motion", EventChannel = "dcg/event", SnapshotChannel = "dcg/snapshot";
        public NetworkManager Manager { get; private set; }
        public UnityTransport Transport { get; private set; }
        public event Action Connected;
        public event Action<string> Disconnected;
        public event Action<DuelMessage> Control;
        public event Action<DuelInputPacket, bool> Input;
        public event Action<DuelSnapshotPacket> Snapshot;
        public Func<bool> CanJoin;
        ulong? remote;
        bool stopping, reserved;
        public bool HasPeer => remote.HasValue && Manager.IsListening;
        public bool Busy => Manager != null && (Manager.IsListening || Manager.ShutdownInProgress);
        public double ServerTime => Manager != null && Manager.IsListening ? Manager.ServerTime.Time : Time.realtimeSinceStartupAsDouble;
        public ulong Rtt => HasPeer ? Transport.GetCurrentRtt(remote.Value) : 0;

        void Awake()
        {
            Transport = gameObject.AddComponent<UnityTransport>();
            Manager = gameObject.AddComponent<NetworkManager>();
            Manager.NetworkConfig = new NetworkConfig {
                NetworkTransport = Transport, TickRate = 30, EnableSceneManagement = false,
                ConnectionApproval = true, PlayerPrefab = null,
                ConnectionData = Encoding.UTF8.GetBytes(DuelRoom.Protocol)
            };
            Transport.ConnectTimeoutMS = 1000; Transport.MaxConnectAttempts = 5;
            Transport.DisconnectTimeoutMS = 10000;
            Manager.ConnectionApprovalCallback = Approve;
            Manager.OnClientConnectedCallback += OnConnected;
            Manager.OnClientDisconnectCallback += OnDisconnected;
        }
        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool localHost = request.ClientNetworkId == NetworkManager.ServerClientId;
            bool approved = localHost || (!reserved && !remote.HasValue && CanJoin?.Invoke() == true &&
                request.Payload != null && Encoding.UTF8.GetString(request.Payload) == DuelRoom.Protocol);
            if (approved && !localHost) reserved = true;
            response.Approved = approved; response.CreatePlayerObject = false; response.Pending = false;
            response.Reason = approved ? "" : "Room is full, already playing, or game versions differ.";
        }
        public bool StartSession(bool host, string address, ushort port)
        {
            if (Busy) return false;
            stopping = reserved = false; remote = null;
            Transport.SetConnectionData(address, port, host ? "0.0.0.0" : null);
            bool started = host ? Manager.StartHost() : Manager.StartClient();
            if (!started) return false;
            var messages = Manager.CustomMessagingManager;
            messages.RegisterNamedMessageHandler(ControlChannel, ReceiveControl);
            messages.RegisterNamedMessageHandler(MotionChannel, (sender, reader) => ReceiveInput(sender, reader, false));
            messages.RegisterNamedMessageHandler(EventChannel, (sender, reader) => ReceiveInput(sender, reader, true));
            messages.RegisterNamedMessageHandler(SnapshotChannel, ReceiveSnapshot);
            return true;
        }
        void OnConnected(ulong id)
        {
            if (Manager.IsHost && id == NetworkManager.ServerClientId) return;
            remote = Manager.IsServer ? id : NetworkManager.ServerClientId;
            Connected?.Invoke();
        }
        void OnDisconnected(ulong id)
        {
            if (stopping || (Manager.IsServer && remote.HasValue && remote.Value != id)) return;
            if (Manager.IsServer && !remote.HasValue) { reserved = false; return; }
            remote = null; reserved = false;
            Disconnected?.Invoke(string.IsNullOrEmpty(Manager.DisconnectReason) ? "Connection closed or timed out." : Manager.DisconnectReason);
        }
        bool IsPeer(ulong sender) => remote.HasValue && sender == remote.Value;
        void ReceiveControl(ulong sender, FastBufferReader reader)
        {
            if (!IsPeer(sender) || reader.Length - reader.Position > 4096) return;
            try { reader.ReadValueSafe(out string json); Control?.Invoke(JsonUtility.FromJson<DuelMessage>(json)); }
            catch (Exception e) when (e is OverflowException || e is ArgumentException) { Debug.LogWarning("Rejected malformed NGO control message."); }
        }
        void ReceiveInput(ulong sender, FastBufferReader reader, bool reliable)
        {
            if (!Manager.IsServer || !IsPeer(sender) || reader.Length - reader.Position != FastBufferWriter.GetWriteSize<DuelInputPacket>()) return;
            reader.ReadValueSafe(out DuelInputPacket packet); Input?.Invoke(packet, reliable);
        }
        void ReceiveSnapshot(ulong sender, FastBufferReader reader)
        {
            if (Manager.IsServer || !IsPeer(sender) || reader.Length - reader.Position != FastBufferWriter.GetWriteSize<DuelSnapshotPacket>()) return;
            reader.ReadValueSafe(out DuelSnapshotPacket packet); Snapshot?.Invoke(packet);
        }
        public void SendControl(DuelMessage message)
        {
            if (!HasPeer) return;
            using (var writer = new FastBufferWriter(4096, Allocator.Temp))
            {
                writer.WriteValueSafe(JsonUtility.ToJson(message));
                Manager.CustomMessagingManager.SendNamedMessage(ControlChannel, remote.Value, writer, NetworkDelivery.ReliableSequenced);
            }
        }
        public void SendInput(DuelInputPacket packet, bool reliable)
        {
            if (!HasPeer) return;
            using (var writer = new FastBufferWriter(512, Allocator.Temp))
            {
                writer.WriteValueSafe(packet);
                Manager.CustomMessagingManager.SendNamedMessage(reliable ? EventChannel : MotionChannel, remote.Value, writer,
                    reliable ? NetworkDelivery.ReliableSequenced : NetworkDelivery.UnreliableSequenced);
            }
        }
        public void SendSnapshot(DuelSnapshotPacket packet, bool final = false)
        {
            if (!HasPeer) return;
            using (var writer = new FastBufferWriter(1200, Allocator.Temp))
            {
                writer.WriteValueSafe(packet);
                Manager.CustomMessagingManager.SendNamedMessage(SnapshotChannel, remote.Value, writer,
                    final ? NetworkDelivery.ReliableSequenced : NetworkDelivery.UnreliableSequenced);
            }
        }
        public void StopSession()
        {
            stopping = true; remote = null; reserved = false;
            if (Manager != null && Manager.IsListening) Manager.Shutdown();
        }
        void OnDestroy() { StopSession(); }
    }
}
