using System.Collections;
using System.Collections.Generic;
using DCG.Core;
using DCG.Gameplay;
using DCG.Gameplay.Navigation;
using DCG.Classes.Graves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DCG.Tests
{
    public sealed class TestMovePolicy : MonoBehaviour, IMovementPolicy
    {
        Vector3 destination; bool moving;
        public void Receive(PlayerCommand command)
        { moving = command.Envelope.CommandType == CommandType.MoveTo; destination = command.Move.Destination; }
        public Vector3 DesiredVelocity(float dt)
        {
            Vector3 delta = destination - transform.position; delta.y = 0;
            return moving && delta.magnitude > .1f ? delta.normalized * 4 : Vector3.zero;
        }
        public void Stop() { moving = false; }
    }
    public sealed class SimulationTests
    {
        GameObject root;
        SimulationWorld world;
        PrototypeTuning tuning;
        ActorSimulation one, two;
        [SetUp] public void Setup()
        {
            root = new GameObject("TestFixture");
            world = root.AddComponent<SimulationWorld>(); world.AutomaticTicks = false;
            tuning = ScriptableObject.CreateInstance<PrototypeTuning>();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform);
            ground.transform.position = new Vector3(0,-.5f,0); ground.transform.localScale = new Vector3(40,1,40);
            ground.layer = LayerMask.NameToLayer("NavigationSurface");
            one = MakeActor(10, Vector3.zero); two = MakeActor(11, new Vector3(5,0,0));
            Physics.SyncTransforms();
        }
        ActorSimulation MakeActor(uint id, Vector3 at)
        {
            var go = new GameObject("Actor " + id); go.transform.SetParent(root.transform); go.transform.position = at;
            var cc = go.AddComponent<CharacterController>(); cc.center = Vector3.up * .9f; cc.height = 1.8f; cc.radius = .35f;
            go.AddComponent<CharacterMotor>(); go.AddComponent<TestMovePolicy>();
            var actor = go.AddComponent<ActorSimulation>(); actor.actorNumber = id; actor.team = (int)id; actor.tuning = tuning;
            world.Register(actor); return actor;
        }
        PlayerCommand Move(uint sequence) => new PlayerCommand {
            Envelope = new CommandEnvelope { ActorId = one.Id, Sequence = sequence, CommandType = CommandType.MoveTo },
            Move = new MoveToCommand { Destination = new Vector3(4,0,0) }
        };
        [TearDown] public void Cleanup()
        { Object.DestroyImmediate(root); Object.DestroyImmediate(tuning); }

        [UnityTest] public IEnumerator CommandsMoveActorWithoutInputCameraOrNetwork()
        {
            Assert.That(world.Session.Submit(one.Id, Move(1)), Is.True);
            for (int i = 0; i < 30; i++) { world.Step(.02f); yield return null; }
            Assert.That(one.transform.position.x, Is.GreaterThan(1));
            Assert.That(one.LastSequence, Is.EqualTo(1));
        }
        [Test] public void SessionRejectsWrongOwnerDuplicateStaleAndInvalidPosition()
        {
            Assert.That(world.Session.Submit(two.Id, Move(1)), Is.False);
            Assert.That(world.Session.Submit(one.Id, Move(2)), Is.True);
            Assert.That(world.Session.Submit(one.Id, Move(2)), Is.False);
            Assert.That(world.Session.Submit(one.Id, Move(1)), Is.False);
            var invalid = Move(3); invalid.Move.Destination.x = float.NaN;
            Assert.That(world.Session.Submit(one.Id, invalid), Is.False);
            Assert.That(world.Session.PendingCount, Is.EqualTo(1));
        }
        [Test] public void SharedAssetDoesNotShareHealthOrAmmoAndDamageIsDeduplicated()
        {
            var hit = new DamageRequest(one.Id, two.Id, 5, 20);
            Assert.That(world.Damage.Apply(hit, two), Is.True);
            Assert.That(world.Damage.Apply(hit, two), Is.False);
            Assert.That(two.Health.Current, Is.EqualTo(80));
            Assert.That(one.Health.Current, Is.EqualTo(100));
            Assert.That(tuning.maxHealth, Is.EqualTo(100));
            Assert.That(one.Combat, Is.Not.SameAs(two.Combat));
        }
        [Test] public void GravesShotEmitsOneVisibleTracePerPellet()
        {
            CombatEvent fired = default;
            one.Combat.Fired += value => fired = value;
            Assert.That(one.Combat.TryAttack(two), Is.True);
            world.Step(tuning.windupSeconds + .01f);
            Assert.That(fired.ImpactPoints, Has.Length.EqualTo(tuning.pelletCount));
            Assert.That(fired.ImpactPoints[0], Is.Not.EqualTo(fired.ImpactPoints[fired.ImpactPoints.Length - 1]));
        }
        [Test] public void AttackClickSelectsEnemyClosestToClickedPoint()
        {
            var three = MakeActor(12, new Vector3(-5, 0, 0));
            Assert.That(TargetTracker.AcquireClosestToPoint(one, new Vector3(-4, 0, 0)), Is.EqualTo(three));
            Assert.That(TargetTracker.AcquireClosestToPoint(one, new Vector3(4, 0, 0)), Is.EqualTo(two));
        }
        [UnityTest] public IEnumerator DeathStopsMovementAndRejectsFurtherCommands()
        {
            world.Session.Submit(one.Id, Move(1)); world.Step(.02f);
            Assert.That(world.Damage.Apply(new DamageRequest(two.Id, one.Id, 1, 1000), one), Is.True);
            var position = one.transform.position;
            Assert.That(world.Session.Submit(one.Id, Move(2)), Is.False);
            for (int i = 0; i < 5; i++) { world.Step(.02f); yield return null; }
            Assert.That(one.transform.position, Is.EqualTo(position));
            Assert.That(one.Snapshot(world.Tick).LifeState, Is.EqualTo(LifeState.Dead));
        }
    }

    public sealed class SceneIntegrationTests
    {
        static AsyncOperation LoadScene(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync(path);
#endif
        }
        [UnityTest] public IEnumerator NewInputSystemMouseClickActivatesOverlayWithoutGameplayCommand()
        {
            yield return LoadScene("Assets/_DCG/Scenes/ControlLab.unity");
            yield return null;
            // Real mice must be removed first, not just disabled. "Point" binds <Mouse>/position, so
            // while the editor's physical mouse is still a device the action reads its desktop position
            // (observed: 3569, -709 on a multi-monitor desktop) instead of the synthetic device's, and the
            // click lands outside the overlay. That made this test pass in batch mode (-nographics, no real
            // mouse) and fail in an interactive editor. DisableDevice is not enough; the value still wins.
            var removed = new List<Mouse>();
            foreach (var device in InputSystem.devices)
                if (device is Mouse existing) removed.Add(existing);
            foreach (var existing in removed) InputSystem.RemoveDevice(existing);
            var mouse = InputSystem.AddDevice<Mouse>();
            var oldMode = InputSystem.settings.updateMode;
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            try
            {
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                // Batch tests have no focused Game View. Route only this synthetic test's events to play mode.
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.EnableDevice(mouse);
                var overlay = Object.FindFirstObjectByType<DCG.Presentation.DebugOverlay>();
                var reader = Object.FindFirstObjectByType<GravesInputReader>();
                reader.SendMessage("OnApplicationFocus", true);
                Vector2 click = new Vector2(250, Screen.height - 310);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = click });
                InputSystem.Update();
                reader.PollInput();
                InputSystem.QueueStateEvent(mouse, new MouseState { position = click }.WithButton(MouseButton.Left));
                InputSystem.Update();
                Assert.That(mouse.leftButton.wasPressedThisFrame, Is.True);
                Assert.That(overlay.BlocksPointer(mouse.position.ReadValue()), Is.True);
                reader.PollInput();
                Assert.That(overlay.PatrolActive(), Is.True);
                Assert.That(reader.actor.GetComponent<GravesOrderController>().Order, Is.EqualTo(OrderType.Idle));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                foreach (var existing in removed) InputSystem.AddDevice(existing);
                InputSystem.settings.updateMode = oldMode;
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
            }
        }

        [UnityTest] public IEnumerator OriginalSampleSceneEntersPlayModeWithoutMissingScripts()
        {
            yield return LoadScene("Assets/Scenes/SampleScene.unity");
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("SampleScene"));
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var component in go.GetComponentsInChildren<MonoBehaviour>(true))
                    Assert.That(component, Is.Not.Null, "Missing component in original sample");
        }
        [UnityTest] public IEnumerator AttackMoveResumesDestinationAfterTargetDies()
        {
            yield return LoadScene("Assets/_DCG/Scenes/ControlLab.unity");
            yield return null;
            var world = Object.FindFirstObjectByType<SimulationWorld>(); world.AutomaticTicks = false;
            var player = world.Find(new ActorId(1));
            world.Session.Submit(player.Id, new PlayerCommand {
                Envelope = new CommandEnvelope { ActorId = player.Id, Sequence = 1, CommandType = CommandType.AttackMove },
                Move = new MoveToCommand { Destination = new Vector3(-9,0,7) }
            });
            for (int i = 0; i < 900; i++) { world.Step(.02f); Physics.SyncTransforms(); }
            Assert.That(world.Find(new ActorId(3)).Health.IsAlive, Is.False);
            Assert.That(player.transform.position.z, Is.GreaterThan(6));
            Assert.That(player.GetComponent<GravesOrderController>().Order, Is.EqualTo(OrderType.Idle));
        }
        [UnityTest] public IEnumerator CoverBlocksDamageAndSelectedTargetCanBeApproached()
        {
            yield return LoadScene("Assets/_DCG/Scenes/ControlLab.unity");
            yield return null;
            var world = Object.FindFirstObjectByType<SimulationWorld>(); world.AutomaticTicks = false;
            var player = world.Find(new ActorId(1)); var target = world.Find(new ActorId(4));
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
            player.transform.position = new Vector3(-5,0,-1); cc.enabled = true; Physics.SyncTransforms();
            Assert.That(player.Combat.TryAttack(target), Is.False);
            world.Session.Submit(player.Id, new PlayerCommand {
                Envelope = new CommandEnvelope { ActorId = player.Id, Sequence = 1, CommandType = CommandType.Target },
                Target = new TargetCommand { TargetActorId = target.Id, OrderType = OrderType.AttackTarget }
            });
            for (int i = 0; i < 1000 && target.Health.Current == target.Health.Maximum; i++)
            { world.Step(.02f); Physics.SyncTransforms(); }
            Assert.That(target.Health.Current, Is.LessThan(target.Health.Maximum));
        }
        [UnityTest] public IEnumerator ControlLabCanMoveAttackAndStop()
        {
            yield return LoadScene("Assets/_DCG/Scenes/ControlLab.unity");
            yield return null;
            var world = Object.FindFirstObjectByType<SimulationWorld>();
            var player = world.Find(new ActorId(1)); var target = world.Find(new ActorId(3));
            world.AutomaticTicks = false;
            Assert.That(player, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<AudioListener>().Length, Is.EqualTo(1));
            world.Session.Submit(player.Id, new PlayerCommand {
                Envelope = new CommandEnvelope { ActorId = player.Id, Sequence = 1, CommandType = CommandType.Target },
                Target = new TargetCommand { TargetActorId = target.Id, OrderType = OrderType.AttackTarget }
            });
            for (int i = 0; i < 400 && target.Health.IsAlive; i++) { world.Step(.02f); Physics.SyncTransforms(); }
            Assert.That(target.Health.Current, Is.LessThan(target.Health.Maximum));
            world.Session.Submit(player.Id, new PlayerCommand {
                Envelope = new CommandEnvelope { ActorId = player.Id, Sequence = 2, CommandType = CommandType.Stop }
            });
            world.Step(.02f);
            Assert.That(player.GetComponent<GravesOrderController>().Order, Is.EqualTo(OrderType.Idle));
        }
    }
}
