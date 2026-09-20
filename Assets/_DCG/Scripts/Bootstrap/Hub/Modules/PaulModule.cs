using System.Collections.Generic;
using DCG.Core;
using DCG.Gameplay;
using DCG.Gameplay.Fighting;
using DCG.Classes.Paul;
using DCG.Presentation;
using UnityEngine;

namespace DCG.Bootstrap.Hub
{
    // Paul, ported from PaulLabController. The input judging, frame advantage and counter branches are
    // the lab's code unchanged; what moved is the clock. The lab accumulated its own 60 Hz with
    // FrameClock inside Update, and the hub drives the match from its fixed tick instead (doc 14,
    // section 3). FrameClock stays where it is, for replay and tests.
    public sealed class PaulModule : ControlModuleBase
    {
        public GameObject actorPrefab;
        public PrototypeTuning common;
        public FightMoveSet moves;
        public Material playerMaterial, dummyMaterial;

        // Where the fighter stands in a duel, measured from the plaza centre.
        public Vector3 duelSpawnOffset = Vector3.zero;

        public override ClassId Id => ClassId.Paul;
        // A fighter can stand in as somebody else's opponent: he keeps his own frame loop, so he still
        // falls, reacts and gets up while another class is the one being played.
        public override bool CanBeOpponent => true;
        public override ActorSimulation PrimaryActor => first != null ? first.Actor : null;
        public FighterAgent Fighter => first;
        public override string DisplayName => "PAUL  /  fighting game input";
        public override string Summary => "WASD direction, U I LP RP, J K LK RK, F1-F4 dummy, F6 record, F7 replay";
        // Keyboard only, and the frame display has to stay readable: the cursor is never captured.
        public override HubCursorMode RequiredCursor => HubCursorMode.Free;
        // Two fighters have to advance together on the same frame, so the hub steps this one itself.
        public override TickPolicy RequiredTick => TickPolicy.HubDriven;

        public FighterMatch Match { get; private set; }
        public FightDummyMode DummyMode { get; set; }
        public bool Recording { get; private set; }
        public int RecordedFrames => tape.Count;

        readonly List<FightInputFrame> tape = new List<FightInputFrame>();
        FighterAgent first, second;
        FighterInputReader input;
        FightCameraRig rig;
        int replayIndex;
        uint sequence;
        double sampleTime;

        protected override void OnActivate()
        {
            // The stage axis is the map's east-west axis, so the camera looking north from the south
            // puts east on the right of the screen (doc 12, 1-1). These two anchors are the only
            // positions the class needs from the map.
            bool duel = Role == HubRole.Opponent || Context.Hub.OpponentModule != null;
            first = SpawnFighter("Paul prototype",
                duel ? Context.Map.plazaCenter.position + duelSpawnOffset : Context.Map.fightWest.position,
                0, playerMaterial);
            // The practice dummy exists for solo practice only. In a duel the far side of the plane is
            // the other class, and it is linked in by SetOpponent once both actors exist.
            second = duel ? null
                : SpawnFighter("Practice dummy", Context.Map.fightEast.position, 1, dummyMaterial);

            var matchHost = Track(new GameObject("Paul match"));
            Match = matchHost.AddComponent<FighterMatch>();
            Match.world = Context.World; Match.first = first; Match.second = second;
            // Not Initialize(): that one registers the two actors itself and takes the world's tick
            // policy with it (doc 14, conflict 3). The hub already registered them with hub-issued ids
            // and already owns the tick policy, so only the agents are initialized here.
            if (second != null) Match.InitializeAgents();
            else first.Initialize();   // the far side arrives with SetOpponent

            tape.Clear();
            Recording = false; replayIndex = 0; DummyMode = FightDummyMode.Idle;
            sampleTime = Time.realtimeSinceStartupAsDouble;
            if (!Driven) return;

            var host = Context.Camera.gameObject;
            Context.Camera.nearClipPlane = .05f;
            rig = Attach<FightCameraRig>(host);
            rig.first = first.transform; rig.second = Match.DefenderTransform; rig.viewCamera = Context.Camera;

            // The reader switches the Input System to manual event processing in OnEnable and restores
            // the previous mode in OnDisable. Attaching and detaching it per class is what makes that
            // save and restore the module switching contract (doc 14, section 3).
            input = AttachDeferred<FighterInputReader>("Paul input", reader => {
                reader.controls = Context.Hub.controls;
                // The hub owns the input update mode for the whole session now, because a fight runs
                // beside the other classes and the mode cannot belong to one class any more.
                reader.ownsUpdateMode = false;
            });
        }

        // Both sides of a duel exist by now, so the plane gets its far end and the camera follows it.
        public override void SetOpponent(IControlModule opponent)
        {
            if (Match == null || opponent == null) return;
            var otherFighter = opponent as PaulModule;
            Match.SetOpponent(otherFighter != null ? otherFighter.Fighter : null, opponent.PrimaryActor);
            if (rig != null) rig.second = Match.DefenderTransform;
            if (otherFighter == null) GiveReactions(opponent.PrimaryActor);
        }

        // A class from another game has no reaction of its own, so the fighter lends it one: the same
        // state machine his own victim uses, carrying his move set's down, getup and juggle limits.
        // Without this his combos are single hits with damage, because nothing holds the other body
        // still long enough for the second hit to arrive.
        void GiveReactions(ActorSimulation opponent)
        {
            if (opponent == null) return;
            var receiver = opponent.GetComponent<FightReactionReceiver>();
            if (receiver == null) receiver = opponent.gameObject.AddComponent<FightReactionReceiver>();
            receiver.rules = first.moveSet;
        }

        protected override void OnDeactivate()
        {
            Match = null; first = null; second = null; input = null; rig = null;
            tape.Clear(); Recording = false; replayIndex = 0;
        }

        public override void ResetState()
        {
            var hub = Context.Hub;
            Deactivate();
            hub.Select(this);
        }

        FighterAgent SpawnFighter(string name, Vector3 at, int team, Material material)
        {
            var instance = Spawn(actorPrefab, at, Quaternion.identity);
            instance.name = name;
            var actor = instance.GetComponent<ActorSimulation>();
            actor.team = team; actor.tuning = common; actor.classId = ClassId.Paul;
            var fighter = instance.GetComponent<FighterAgent>();
            if (moves != null) fighter.moveSet = moves;
            Paint(instance.GetComponent<FighterView>(), material);
            Register(actor);
            return fighter;
        }

        static void Paint(FighterView view, Material material)
        {
            if (view == null || material == null) return;
            foreach (var part in new[] { view.body, view.leftHand, view.rightHand, view.leftFoot, view.rightFoot })
            {
                if (part == null) continue;
                var renderer = part.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = material;
            }
        }

        // One fight frame per hub fixed tick. The global tick is 60 Hz (stage 0), which is the rate the
        // fighting system is written for, so a fixed tick and a fight frame are the same thing here.
        public override void TickFixed()
        {
            if (Match == null || Match.DefenderTransform == null) return;
            // Standing in as an opponent: the frame loop still runs, so he falls, takes reactions and
            // gets up, but no input arrives and no dummy behaviour is driven.
            if (input == null) { Tick(new FightInputFrame { Direction = 5 }); return; }
            sampleTime += FrameClock.StepSeconds;
            input.Poll();
            if (input.Pressed("Reset")) { ResetState(); return; }
            if (input.Pressed("Idle")) DummyMode = FightDummyMode.Idle;
            if (input.Pressed("AllGuard")) DummyMode = FightDummyMode.AllGuard;
            if (input.Pressed("CrouchGuard")) DummyMode = FightDummyMode.CrouchGuard;
            if (input.Pressed("Jab")) DummyMode = FightDummyMode.Jab;
            if (input.Pressed("Record")) SetRecording(!Recording);
            if (input.Pressed("Replay")) StartReplay();
            Tick(input.Sample(sampleTime));
        }

        public void SetRecording(bool value) { Recording = value; if (value) tape.Clear(); }

        public void StartReplay()
        {
            Recording = false; DummyMode = FightDummyMode.Replay; replayIndex = 0;
        }

        // Ported from PaulLabController.Tick, unchanged.
        public void Tick(FightInputFrame player)
        {
            if (Recording && tape.Count < 3600)
            {
                var saved = player;
                saved.Direction = FightDirections.Relative(saved.Direction, Match.first.Side);
                tape.Add(saved);
            }
            // No practice dummy in a duel: the far side is a live class driving itself.
            if (Match.second == null)
            {
                Send(Match.first, player);
                Match.StepFrame();
                return;
            }
            var dummy = new FightInputFrame { Direction = 5 };
            Match.second.HoldPosition = DummyMode != FightDummyMode.Replay;
            if (DummyMode == FightDummyMode.AllGuard)
                dummy.Direction = Match.first.State.Move != null &&
                    Match.first.State.Move.level == HitLevel.Low ? 1 : 4;
            if (DummyMode == FightDummyMode.CrouchGuard) dummy.Direction = 1;
            if (DummyMode == FightDummyMode.Jab && Match.Frame % 40 == 0) dummy.Pressed = FightButtons.LP;
            if (DummyMode == FightDummyMode.Replay && tape.Count > 0) dummy = tape[replayIndex++ % tape.Count];
            dummy.Direction = FightDirections.Relative(dummy.Direction, Match.second.Side);
            Send(Match.first, player); Send(Match.second, dummy);
            Match.StepFrame();
        }

        void Send(FighterAgent fighter, FightInputFrame frame)
        {
            Context.World.Session.Submit(fighter.Actor.Id, new PlayerCommand {
                Envelope = new CommandEnvelope { ActorId = fighter.Actor.Id, Sequence = ++sequence,
                    CommandType = CommandType.FightInput },
                Fight = frame
            });
        }

        public override void DrawHud()
        {
            if (!Driven || Match == null || Match.first == null || Match.first.Actor == null) return;
            GUI.Box(new Rect(20, 20, 500, 150), "PAUL / TEKKEN 7 INPUT / 60 Hz hub tick");
            GUI.Label(new Rect(34, 48, 470, 115),
                "WASD Direction | U/I LP/RP | J/K LK/RK\nO Both hands | L Both feet | S, S+D, D+I Phoenix\nF1 Idle | F2 All guard | F3 Crouch guard | F4 Jab\nF5 Reset | F6 Record player | F7 Replay on dummy\nPrototype moves / no Heat, Rage, throws or air combos");
            GUI.Label(new Rect(Screen.width - 280, 20, 265, 100), "FRAME " + Match.Frame + " / " + DummyMode +
                "\n" + (Recording ? "REC " : "TAPE ") + tape.Count + " frames\n" +
                Match.first.Actor.Health.Current + " HP  vs  " +
                (Match.Defender != null ? Match.Defender.Health.Current.ToString("0") : "-") + " HP");
            var a = Match.first;
            GUI.Box(new Rect(20, Screen.height - 125, 600, 105), string.Empty);
            GUI.Label(new Rect(34, Screen.height - 115, 570, 95), a.Side + " | " + a.State.Phase + " | " + a.LastCommand +
                "\n" + a.LastResult + " | advantage " + a.LastAdvantage + "f | move frame " + a.State.Age +
                " | stun " + a.State.Stun +
                "\nDefender: " + (Match.second != null
                    ? Match.second.State.Phase + " | juggle " + Match.second.State.JuggleCost
                    : "not a fighter") +
                "\nInput: " + History(a));
        }

        static string History(FighterAgent agent)
        {
            string value = string.Empty; int previous = -1;
            for (int i = System.Math.Min(15, agent.Buffer.Count) - 1; i >= 0; i--)
            {
                var frame = agent.Buffer.Recent(i);
                if (frame.Relative != previous || frame.Raw.Pressed != 0)
                    value += frame.Relative + (frame.Raw.Pressed != 0 ? "+" + frame.Raw.Pressed : string.Empty) + "  ";
                previous = frame.Relative;
            }
            return value;
        }
    }
}
