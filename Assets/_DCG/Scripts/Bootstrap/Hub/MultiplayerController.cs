using System;
using DCG.Core;
using DCG.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCG.Bootstrap.Hub
{
    [DefaultExecutionOrder(-50)]
    public sealed class MultiplayerController : MonoBehaviour, ICommandGateway
    {
        public ControlHubController hub;
        public DuelState State { get; private set; }
        public bool IsHost { get; private set; }
        public int LocalSlot => IsHost ? 0 : 1;
        public string Status { get; private set; } = "NGO / Unity Transport: create or join a room.";
        public bool Connected => State != null;
        public uint ReceivedTick { get; private set; }
        public ulong PingMs => transport != null ? transport.Rtt : 0;
        public double SnapshotAge => lastSnapshotAt > 0 ? Now - lastSnapshotAt : 0;
        public float FrameMs { get; private set; }
        public DuelNgoTransport Network => transport;
        DuelRoom room;
        DuelNgoTransport transport;
        DuelMap map;
        readonly DuelReplicaView[] replicas = new DuelReplicaView[2];
        readonly DuelMotionHistory history = new DuelMotionHistory();
        readonly DuelInterpolation interpolation = new DuelInterpolation();
        DuelInputBuffer[] buffers = { new DuelInputBuffer(), new DuelInputBuffer() };
        readonly uint[] serverSequences = new uint[2];
        int loadedRound = -1;
        bool inputEnabled, connecting, leaving;
        double connectStarted, nextStateSync, lastSnapshotAt;
        PlayerCommand outgoingMotion;
        bool motionPending;
        Vector3 pendingCorrection;
        DuelSnapshotPacket latestSnapshot;
        string address = "127.0.0.1", portText = "7777";
        double Now => Time.realtimeSinceStartupAsDouble;
        static readonly string[] ClassNames = { "그레이브즈", "벤데타", "M416", "TRG", "폴" };
        static readonly string[] MapNames = { "중앙 광장", "엄폐 전장" };
        Font uiFont;
        GUIStyle titleStyle, labelStyle, buttonStyle;

        void Awake()
        {
            Application.runInBackground = true;
            hub.NetworkMode = true; hub.ArenaMode = false; hub.world.CommandGateway = this;
            // A separate root prevents NGO's persistence rules from preserving the scene's whole hub.
            transport = new GameObject("DCG NGO NetworkManager").AddComponent<DuelNgoTransport>();
            transport.CanJoin = () => State != null && State.phase == DuelPhase.Characters && !State.guestConnected;
            transport.Connected += OnConnected;
            transport.Disconnected += OnDisconnected;
            transport.Control += ReceiveControl;
            transport.Input += (packet, reliable) => ReceiveInput(1, packet, reliable);
            transport.Snapshot += ReceiveSnapshot;
        }
        public bool Host(int port = 7777)
        {
            if (transport.Busy) { Status = "Leave the current session before creating another."; return false; }
            Leave();
            try
            {
                if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
                IsHost = true; room = new DuelRoom(() => UnityEngine.Random.Range(0, 2)); State = room.State;
                hub.world.Damage.Authoritative = true;
                if (!transport.StartSession(true, "127.0.0.1", (ushort)port)) throw new InvalidOperationException("NGO could not start the host.");
                Status = "Waiting for opponent on UDP port " + port; return true;
            }
            catch (Exception e) { Fail("Could not create room: " + e.Message); return false; }
        }
        public bool Join(string host, int port = 7777)
        {
            if (transport.Busy) { Status = "Leave the current session before joining another."; return false; }
            Leave();
            try
            {
                if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
                IsHost = false; hub.world.Damage.Authoritative = false;
                connecting = true; connectStarted = Now;
                if (!transport.StartSession(false, host, (ushort)port)) throw new InvalidOperationException("NGO could not start the client.");
                Status = "Connecting..."; return true;
            }
            catch (Exception e) { Fail("Could not connect: " + e.Message); return false; }
        }
        public void Leave()
        {
            leaving = true; transport?.StopSession(); connecting = false;
            room = null; State = null; inputEnabled = false;
            ResetBattle();
            Status = "Session ended. Create or join a room."; leaving = false;
        }
        void ResetBattle()
        {
            if (hub != null) hub.ClearNetworkDuel();
            if (map != null) { map.gameObject.SetActive(false); Destroy(map.gameObject); map = null; }
            loadedRound = -1; ReceivedTick = 0; lastSnapshotAt = 0;
            buffers = new[] { new DuelInputBuffer(), new DuelInputBuffer() };
            serverSequences[0] = serverSequences[1] = 0;
            history.Clear(); interpolation.Clear(); pendingCorrection = Vector3.zero; motionPending = false;
            latestSnapshot = default;
        }
        void Fail(string message) { Leave(); Status = message; Debug.Log("DCG_DUEL: " + message); }
        void OnDestroy()
        {
            Leave();
            if (transport != null) Destroy(transport.gameObject);
            if (hub != null && hub.world != null) hub.world.CommandGateway = null;
            if (uiFont != null) Destroy(uiFont);
        }
        void OnConnected()
        {
            connecting = false;
            if (IsHost)
            {
                if (room == null || !room.Join(DuelRoom.Protocol)) { transport.StopSession(); return; }
                Publish();
            }
        }
        void OnDisconnected(string reason)
        {
            if (leaving) return;
            connecting = false;
            if (IsHost && room != null && State.guestConnected)
            { room.Disconnect(); State = room.State; hub.SetNetworkInput(false); inputEnabled = false; }
            else if (!IsHost) Fail(reason);
        }
        void Update()
        {
            InputSystem.Update();
            FrameMs = Mathf.Lerp(FrameMs, Time.unscaledDeltaTime * 1000, .05f);
            try
            {
                if (connecting && Now - connectStarted > 10) { Fail("Connection timed out."); return; }
                if (State != null)
                {
                    if (IsHost && room.Advance(transport.ServerTime)) Publish();
                    ApplyState();
                    if (IsHost && Now >= nextStateSync) { nextStateSync = Now + 1; Publish(); }
                }
                UpdateRemotePose();
            }
            catch (Exception e) { Fail("Network session error: " + e.Message); }
            hub.UpdateNetworkViews(false);
        }
        void FixedUpdate()
        {
            if (State == null || State.phase != DuelPhase.Battle || loadedRound != State.round) return;
            hub.SampleNetworkInput();
            if (IsHost)
            {
                Drain(0); Drain(1); hub.StepNetworkWorld();
                bool ended = room.Finish(hub.NetworkPlayers[0].PrimaryActor.Health.IsAlive, hub.NetworkPlayers[1].PrimaryActor.Health.IsAlive);
                if (ended || hub.world.Tick % 2 == 0) transport.SendSnapshot(CaptureSnapshot(), ended);
                if (ended) Publish();
            }
            else
            {
                Drain(LocalSlot); hub.StepPredictedWorld();
                var actor = hub.ActiveModule.PrimaryActor;
                history.Record(hub.world.Tick, actor.transform.position);
                if (pendingCorrection.sqrMagnitude > .000001f)
                {
                    Vector3 correction = pendingCorrection.sqrMagnitude > 9 ? pendingCorrection : pendingCorrection * .25f;
                    Vector3 before = actor.transform.position; actor.Motor.CorrectPrediction(correction);
                    Vector3 applied = actor.transform.position - before;
                    history.Shift(applied); pendingCorrection -= applied;
                }
                if (motionPending && hub.world.Tick % 2 == 0)
                {
                    transport.SendInput(new DuelInputPacket { Round = State.round, Command = outgoingMotion }, false);
                    motionPending = false;
                }
            }
        }
        void Drain(int slot)
        {
            buffers[slot].Drain(command => {
                command.Envelope.Sequence = ++serverSequences[slot];
                hub.world.Session.Submit(hub.NetworkPlayers[slot].PrimaryActor.Id, command);
            });
        }
        void LateUpdate() { hub.UpdateNetworkViews(true); }
        DuelSnapshotPacket CaptureSnapshot() => new DuelSnapshotPacket {
            Round = State.round, Tick = hub.world.Tick, Time = transport.ServerTime,
            First = replicas[0].Capture(hub.world.Tick), Second = replicas[1].Capture(hub.world.Tick)
        };
        void ReceiveControl(DuelMessage message)
        {
            if (message == null) return;
            if (IsHost) { if (State != null && State.guestConnected) HandleRequest(1, message); }
            else if (message.kind == "state")
            {
                if (!ValidState(message.state)) { Fail("Invalid room state."); return; }
                if (State != null && message.state.round < State.round) return;
                State = message.state;
                try { ApplyState(); } catch (Exception e) { Fail("Map preparation failed: " + e.Message); }
            }
        }
        static bool ValidState(DuelState state) => state != null && state.round > 0 &&
            state.characters?.Length == 2 && state.characterLocked?.Length == 2 && state.quickStart?.Length == 2 &&
            state.maps?.Length == 2 && state.mapLocked?.Length == 2 && state.ready?.Length == 2 && state.rematch?.Length == 2 &&
            Enum.IsDefined(typeof(DuelPhase), state.phase) && state.selectedMap >= -1 && state.selectedMap < 2 &&
            state.characters[0] >= -1 && state.characters[0] < 5 && state.characters[1] >= -1 && state.characters[1] < 5 &&
            state.maps[0] >= -1 && state.maps[0] < 2 && state.maps[1] >= -1 && state.maps[1] < 2;
        void HandleRequest(int slot, DuelMessage message)
        {
            bool changed = false;
            switch (message.kind)
            {
                case "quick": changed = room.QuickStart(slot, message.round, transport.ServerTime); break;
                case "character": changed = room.Character(slot, message.round, message.value, message.confirm); break;
                case "map": changed = room.Map(slot, message.round, message.value, transport.ServerTime); break;
                case "ready": changed = room.Ready(slot, message.round, transport.ServerTime); break;
                case "rematch": changed = room.Rematch(slot, message.round); break;
            }
            if (changed) Publish();
        }
        void Request(DuelMessage message)
        {
            if (State == null) return; message.round = State.round;
            if (IsHost) HandleRequest(0, message); else transport.SendControl(message);
        }
        public void QuickStart() => Request(new DuelMessage { kind = "quick" });
        public void SelectCharacter(int id, bool confirm) => Request(new DuelMessage { kind = "character", value = id, confirm = confirm });
        public void SelectMap(int id) => Request(new DuelMessage { kind = "map", value = id });
        public void RequestRematch() => Request(new DuelMessage { kind = "rematch" });
        void Publish()
        { State = room.State; transport.SendControl(new DuelMessage { kind = "state", state = State }); }
        void ApplyState()
        {
            if (State.phase == DuelPhase.Characters && loadedRound != -1) { ResetBattle(); inputEnabled = false; }
            if (State.phase == DuelPhase.Loading && loadedRound != State.round)
            {
                if (State.selectedMap < 0 || State.characters[0] < 0 || State.characters[1] < 0) throw new InvalidOperationException("Invalid preparation state.");
                map = new GameObject("Duel map").AddComponent<DuelMap>(); map.Build(State.selectedMap);
                hub.CreateNetworkDuel((ClassId)State.characters[0], (ClassId)State.characters[1], LocalSlot, map.Grid);
                for (int i = 0; i < 2; i++)
                {
                    var actor = hub.NetworkPlayers[i].PrimaryActor;
                    replicas[i] = actor.gameObject.AddComponent<DuelReplicaView>(); replicas[i].Initialize(actor);
                    if (!IsHost && i != LocalSlot) actor.Motor.PlaceReplica(actor.transform.position, actor.transform.eulerAngles.y);
                }
                loadedRound = State.round; ReceivedTick = 0; history.Record(0, hub.ActiveModule.PrimaryActor.transform.position);
                Request(new DuelMessage { kind = "ready" });
            }
            bool enabled = State.phase == DuelPhase.Battle && loadedRound == State.round;
            if (inputEnabled != enabled) { inputEnabled = enabled; hub.SetNetworkInput(enabled); }
            if (!IsHost && State.phase == DuelPhase.Result && latestSnapshot.Round == State.round && hub.ActiveModule != null)
            {
                var final = LocalSlot == 0 ? latestSnapshot.First.Actor : latestSnapshot.Second.Actor;
                hub.ActiveModule.PrimaryActor.Motor.ApplySnapshot(final.Position, final.Facing, final.Velocity);
            }
        }
        public bool Submit(ActorId sender, PlayerCommand command)
        {
            if (State == null || State.phase != DuelPhase.Battle || hub.NetworkPlayers == null) return false;
            var actor = hub.NetworkPlayers[LocalSlot].PrimaryActor;
            if (sender != actor.Id || command.Envelope.ActorId != actor.Id || !actor.Health.IsAlive) return false;
            command.Envelope.ClientTick = hub.world.Tick;
            bool accepted = false;
            if (DuelInputBuffer.HasEvent(command))
            {
                accepted |= buffers[LocalSlot].Enqueue(command, true);
                if (!IsHost) transport.SendInput(new DuelInputPacket { Round = State.round, Command = command }, true);
            }
            if (DuelInputBuffer.IsMotion(command))
            {
                accepted |= buffers[LocalSlot].Enqueue(command, false);
                if (!IsHost) { outgoingMotion = DuelInputBuffer.WithoutEdges(command); motionPending = true; }
            }
            if (command.Envelope.CommandType == CommandType.Stop) motionPending = false;
            return accepted;
        }
        void ReceiveInput(int slot, DuelInputPacket packet, bool reliable)
        {
            if (!IsHost || State == null || State.phase != DuelPhase.Battle || packet.Round != State.round || hub.NetworkPlayers == null) return;
            if (packet.Command.Envelope.ActorId != hub.NetworkPlayers[slot].PrimaryActor.Id) return;
            buffers[slot].Enqueue(packet.Command, reliable);
        }
        void ReceiveSnapshot(DuelSnapshotPacket packet)
        {
            if (IsHost || State == null || packet.Round != loadedRound || (State.phase != DuelPhase.Battle && State.phase != DuelPhase.Result)) return;
            if (packet.First.Actor.ActorId.Value != 1 || packet.Second.Actor.ActorId.Value != 2 ||
                !CommandValidation.IsFinite(packet.First.Actor.Position) || !CommandValidation.IsFinite(packet.Second.Actor.Position)) return;
            if (!interpolation.Push(packet)) return;
            latestSnapshot = packet;
            ReceivedTick = packet.Tick; lastSnapshotAt = Now;
            replicas[0].Apply(packet.First, LocalSlot == 0); replicas[1].Apply(packet.Second, LocalSlot == 1);
            var own = LocalSlot == 0 ? packet.First.Actor : packet.Second.Actor;
            if (history.Error(own, out Vector3 correction)) pendingCorrection = correction;
            if (State.phase == DuelPhase.Result)
            {
                hub.ActiveModule.PrimaryActor.Motor.ApplySnapshot(own.Position, own.Facing, own.Velocity);
                pendingCorrection = Vector3.zero;
            }
        }
        void UpdateRemotePose()
        {
            if (IsHost || hub.NetworkPlayers == null || transport == null) return;
            int remote = 1 - LocalSlot;
            if (State != null && State.phase == DuelPhase.Result && latestSnapshot.Round == State.round)
            {
                var final = remote == 0 ? latestSnapshot.First.Actor : latestSnapshot.Second.Actor;
                hub.NetworkPlayers[remote].PrimaryActor.Motor.PlaceReplica(final.Position, final.Facing);
                return;
            }
            if (interpolation.Sample(transport.ServerTime - .075, remote, out var position, out var facing))
                hub.NetworkPlayers[remote].PrimaryActor.Motor.PlaceReplica(position, facing);
        }

        void Styles()
        {
            if (titleStyle != null) return;
            uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 18);
            titleStyle = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 25, fontStyle = FontStyle.Bold };
            labelStyle = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 16, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = uiFont, fontSize = 17 };
        }
        string CharacterName(int slot) => State.characters[slot] < 0 ? "선택 중" : ClassNames[State.characters[slot]];
        void OnGUI()
        {
            Styles();
            float width = Mathf.Min(780, Screen.width - 24);
            bool playing = State != null && State.phase == DuelPhase.Battle;
            if (playing)
            {
                GUI.Box(new Rect(12, 12, width, 92), "");
                for (int i = 0; i < 2; i++)
                {
                    int slot = i == 0 ? LocalSlot : 1 - LocalSlot;
                    var actor = hub.NetworkPlayers[slot].PrimaryActor;
                    GUI.Label(new Rect(24 + i * width / 2, 22, width / 2 - 20, 34),
                        (i == 0 ? "나 · " : "상대 · ") + CharacterName(slot) + "  " + actor.Health.Current.ToString("0") + " HP", labelStyle);
                }
                GUI.Label(new Rect(24, 60, width - 145, 32), "NGO · RTT " + PingMs + " ms · 화면 " + FrameMs.ToString("0") + " ms · 수신 " + (SnapshotAge * 1000).ToString("0") + " ms", labelStyle);
                var localActor = hub.NetworkPlayers[LocalSlot].PrimaryActor;
                var localSnapshot = localActor.Snapshot(hub.world.Tick);
                GUI.Label(new Rect(20, Screen.height - 48, 480, 36),
                    CharacterName(LocalSlot) + " · 탄약 " + localSnapshot.Ammo + " · " + localSnapshot.ActionState, labelStyle);
                if (localActor.classId == ClassId.Rifle || localActor.classId == ClassId.Sniper)
                    GUI.Label(new Rect(Screen.width / 2f - 8, Screen.height / 2f - 14, 24, 28), "+", labelStyle);
                if (GUI.Button(new Rect(width - 105, 58, 105, 32), "나가기", buttonStyle)) Leave();
                return;
            }
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2, Mathf.Max(12, (Screen.height - 520) / 2), width, Mathf.Min(520, Screen.height - 24)), GUI.skin.box);
            GUILayout.Label("DCG · 1대1 멀티플레이", titleStyle);
            GUILayout.Space(12);
            if (State == null)
            {
                GUILayout.Label("방장이 방을 만들면 상대는 방장의 IP 주소로 참가합니다.", labelStyle);
                GUILayout.Label("같은 컴퓨터: 127.0.0.1 / 같은 네트워크: 방장의 로컬 IP", labelStyle);
                address = GUILayout.TextField(address, 128);
                GUILayout.Label("포트", labelStyle); portText = GUILayout.TextField(portText, 5);
                GUI.enabled = !transport.Busy && !connecting && int.TryParse(portText, out int parsed) && parsed > 0 && parsed <= 65535;
                if (GUILayout.Button("방 만들기", buttonStyle, GUILayout.Height(44))) Host(int.Parse(portText));
                if (GUILayout.Button("방 참가", buttonStyle, GUILayout.Height(44))) Join(address.Trim(), int.Parse(portText));
                GUI.enabled = true;
                GUILayout.Label(Status, labelStyle);
                if ((connecting || transport.Busy) && GUILayout.Button("취소", buttonStyle)) Leave();
            }
            else
            {
                GUILayout.Label((IsHost ? "방장" : "참가자") + " · " + (State.guestConnected ? "2 / 2명" : "상대 입장 대기"), labelStyle);
                GUILayout.Label("나: " + CharacterName(LocalSlot) + (State.characterLocked[LocalSlot] ? " · 확정" : "") +
                    "     상대: " + CharacterName(1 - LocalSlot) + (State.characterLocked[1 - LocalSlot] ? " · 확정" : ""), labelStyle);
                GUILayout.Space(12);
                if (State.phase == DuelPhase.Characters)
                {
                    GUILayout.Label("캐릭터 선택", titleStyle);
                    GUI.enabled = !State.characterLocked[LocalSlot];
                    if (GUILayout.Button("바로 시작 · 그레이브즈 / 중앙 광장", buttonStyle, GUILayout.Height(48))) QuickStart();
                    GUI.enabled = true;
                    GUILayout.Space(8);
                    GUI.enabled = !State.characterLocked[LocalSlot];
                    GUILayout.BeginHorizontal();
                    for (int i = 0; i < 5; i++)
                        if (GUILayout.Button(ClassNames[i], buttonStyle, GUILayout.Height(52))) SelectCharacter(i, false);
                    GUILayout.EndHorizontal();
                    GUI.enabled = !State.characterLocked[LocalSlot] && State.characters[LocalSlot] >= 0;
                    if (GUILayout.Button("선택 확정", buttonStyle, GUILayout.Height(44))) SelectCharacter(State.characters[LocalSlot], true);
                    GUI.enabled = true;
                    GUILayout.Label("두 플레이어가 모두 확정하면 맵을 선택합니다.", labelStyle);
                }
                else if (State.phase == DuelPhase.Maps)
                {
                    GUILayout.Label("맵 선택", titleStyle);
                    GUILayout.Label("같은 맵이면 그대로, 서로 다르면 두 맵 중 무작위로 결정합니다.", labelStyle);
                    GUI.enabled = !State.mapLocked[LocalSlot];
                    for (int i = 0; i < 2; i++)
                        if (GUILayout.Button(MapNames[i] + (i == 0 ? " · 개방된 중앙과 기둥" : " · 중앙 벽과 낮은 엄폐물"), buttonStyle, GUILayout.Height(52))) SelectMap(i);
                    GUI.enabled = true;
                    GUILayout.Label(State.mapLocked[LocalSlot] ? "내 맵 확정 · 상대 선택 대기" : "맵 버튼을 누르면 투표가 확정됩니다.", labelStyle);
                }
                else if (State.phase == DuelPhase.Loading || State.phase == DuelPhase.Countdown)
                {
                    GUILayout.Label("선택된 맵: " + MapNames[State.selectedMap], titleStyle);
                    GUILayout.Label("나의 투표: " + MapNames[State.maps[LocalSlot]] + " / 상대의 투표: " + MapNames[State.maps[1 - LocalSlot]], labelStyle);
                    GUILayout.Label(State.phase == DuelPhase.Loading ? "양쪽 플레이어 준비 중..." :
                        "전투 시작까지 " + Mathf.CeilToInt((float)Math.Max(0, State.deadline - transport.ServerTime)) + "초", labelStyle);
                }
                else if (State.phase == DuelPhase.Result)
                {
                    GUILayout.Label(State.winner == -1 ? "무승부" : State.winner == LocalSlot ? "승리" : "패배", titleStyle);
                    GUI.enabled = State.guestConnected && !State.rematch[LocalSlot];
                    if (GUILayout.Button("재대전 · 캐릭터부터 다시 선택", buttonStyle, GUILayout.Height(48))) RequestRematch();
                    GUI.enabled = true;
                    if (State.rematch[LocalSlot]) GUILayout.Label("상대 재대전 동의 대기", labelStyle);
                }
                GUILayout.Label(State.message ?? "", labelStyle);
                GUILayout.Space(15);
                if (GUILayout.Button("방 나가기", buttonStyle, GUILayout.Height(36))) Leave();
            }
            GUILayout.EndArea();
        }
    }
}
