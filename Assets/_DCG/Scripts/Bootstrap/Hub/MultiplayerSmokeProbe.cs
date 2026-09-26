using System;
using DCG.Core;
using DCG.Networking;
using UnityEngine;

namespace DCG.Bootstrap.Hub
{
    // Opt-in, standalone integration check. Runs two real processes through three complete matches.
    // No behavior is active in normal play; invoke with -dcgDuelSmoke host|guest.
    public sealed class MultiplayerSmokeProbe : MonoBehaviour
    {
        MultiplayerController session;
        ControlHubController hub;
        bool active, host, closing;
        double started, resultAt;
        int battleRound, resultRound;
        uint sequence = 100000;
        bool moved, damaged, rejectedForeign;
        Vector3 startPosition;
        float startHealth;
        ulong maxRtt;
        double maxSnapshotAge;
        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-dcgDuelSmoke");
            if (index < 0 || index + 1 >= args.Length) { enabled = false; return; }
            active = true; host = args[index + 1] == "host";
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
            session = GetComponent<MultiplayerController>(); hub = session.hub;
            started = Time.realtimeSinceStartupAsDouble;
            int portIndex = Array.IndexOf(args, "-dcgDuelPort");
            int port = portIndex >= 0 && portIndex + 1 < args.Length ? int.Parse(args[portIndex + 1]) : 17777;
            if (host) session.Host(port); else session.Join("127.0.0.1", port);
        }
        void Update()
        {
            if (!active) return;
            if (Time.realtimeSinceStartupAsDouble - started > 90) { Finish(false, "timeout " + session.Status); return; }
            if (closing)
            {
                if (Time.realtimeSinceStartupAsDouble - resultAt > 2) Finish(true, "three NGO matches including quick start complete");
                return;
            }
            var state = session.State;
            if (state == null) return;
            int local = session.LocalSlot;
            if (state.phase == DuelPhase.Characters && !state.characterLocked[local])
            {
                // Reverse roles on rematch to exercise both classes as host and guest.
                if (state.round == 3) session.QuickStart();
                else session.SelectCharacter((local == 0) == (state.round == 1) ? 0 : 2, true);
            }
            else if (state.phase == DuelPhase.Maps && !state.mapLocked[local])
                session.SelectMap(state.round == 1 ? 0 : local); // equal, then conflicting votes
            else if (state.phase == DuelPhase.Battle)
            {
                maxRtt = Math.Max(maxRtt, session.PingMs);
                maxSnapshotAge = Math.Max(maxSnapshotAge, session.SnapshotAge);
                var actor = hub.NetworkPlayers[local].PrimaryActor;
                if (battleRound != state.round)
                {
                    battleRound = state.round; sequence = 100000;
                    hub.SetNetworkInput(false);
                    startPosition = actor.transform.position; startHealth = actor.Health.Current;
                    var opponent = hub.NetworkPlayers[1 - local].PrimaryActor;
                    rejectedForeign = !session.Submit(opponent.Id, new PlayerCommand {
                        Envelope = new CommandEnvelope { ActorId = opponent.Id, Sequence = 500000, CommandType = CommandType.Stop }
                    });
                    Debug.Log("DCG_DUEL_SMOKE_BATTLE round=" + state.round + " map=" + state.selectedMap + " local=" + local);
                }
                if ((actor.transform.position - startPosition).sqrMagnitude > .5f) moved = true;
                if (actor.Health.Current < startHealth) damaged = true;
            }
            else if (state.phase == DuelPhase.Result && resultRound != state.round)
            {
                resultRound = state.round;
                if (!rejectedForeign) { Finish(false, "ownership check failed"); return; }
                Debug.Log("DCG_DUEL_SMOKE_RESULT round=" + state.round + " map=" + state.selectedMap + " winner=" + state.winner + " tick=" + hub.world.Tick);
                if (state.round < 3) session.RequestRematch();
                else
                {
                    if (!moved || !damaged || (!host && session.ReceivedTick == 0))
                    { Finish(false, "missing movement, damage or received frames"); return; }
                    closing = true; resultAt = Time.realtimeSinceStartupAsDouble;
                }
            }
        }
        void FixedUpdate()
        {
            if (!active || session.State == null || session.State.phase != DuelPhase.Battle || battleRound != session.State.round) return;
            var actor = hub.NetworkPlayers[session.LocalSlot].PrimaryActor;
            var target = hub.NetworkPlayers[1 - session.LocalSlot].PrimaryActor;
            var command = new PlayerCommand { Envelope = new CommandEnvelope { ActorId = actor.Id, Sequence = ++sequence } };
            if (actor.classId == ClassId.Graves)
            {
                // Only issue an order occasionally, preserving the attack windup between orders.
                if (sequence > 100001 && hub.world.Tick % 90 != 0) return;
                command.Envelope.CommandType = CommandType.Target;
                command.Target = new TargetCommand { TargetActorId = target.Id, OrderType = OrderType.AttackTarget };
            }
            else
            {
                var direction = target.AimPoint - actor.AimPoint;
                var rotation = Quaternion.LookRotation(direction).eulerAngles;
                command.Envelope.CommandType = CommandType.DirectControl;
                command.Direct = new DirectControlFrame {
                    AimYawPitch = new Vector2(rotation.y, Mathf.DeltaAngle(0, rotation.x)),
                    AimTarget = target.AimPoint,
                    // Cross the cover map along a side lane before engaging.
                    MoveAxes = actor.transform.position.z > -8 && session.State.selectedMap == 1 ? new Vector2(1, 0) : Vector2.zero,
                    HeldButtons = hub.world.Tick > 120 ? ControlButtons.Fire : 0,
                    PressedButtons = hub.world.Tick % 240 == 0 ? ControlButtons.Reload : 0
                };
            }
            session.Submit(actor.Id, command);
        }
        void Finish(bool success, string message)
        {
            active = false;
            Debug.Log((success ? "DCG_DUEL_SMOKE_OK " : "DCG_DUEL_SMOKE_FAILED ") + message);
            Debug.Log("DCG_DUEL_METRICS maxRttMs=" + maxRtt + " maxSnapshotAgeMs=" + (maxSnapshotAge * 1000).ToString("0"));
            Application.Quit(success ? 0 : 1);
        }
    }
}
