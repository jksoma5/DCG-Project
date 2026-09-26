using System.Collections;
using System.Linq;
using DCG.Core;
using DCG.Gameplay;
using DCG.Bootstrap.Hub;
using DCG.Classes.Rifle;
using DCG.Classes.Sniper;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DCG.Tests
{
    public sealed class CharacterArenaTests
    {
        ControlHubController hub;

        [UnitySetUp] public IEnumerator Setup()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_DCG/Scenes/CharacterArena.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            hub = Object.FindFirstObjectByType<ControlHubController>();
            hub.EnemyAI = false;
        }

        [UnityTest] public IEnumerator FiveActorsPersistAcrossRepeatedPossession()
        {
            Assert.That(hub.ArenaMode, Is.True);
            Assert.That(hub.world.Actors.Count, Is.EqualTo(5));
            var originals = hub.world.Actors.ToArray();
            foreach (var actor in originals)
                Assert.That(actor.Health.Maximum, Is.EqualTo(actor.tuning.maxHealth * 5f));
            originals[0].Health.Apply(7);
            for (int round = 0; round < 3; round++)
                foreach (var module in hub.Modules)
                {
                    var position = module.PrimaryActor.transform.position;
                    float health = module.PrimaryActor.Health.Current;
                    Assert.That(hub.SwitchCharacter(module.Id), Is.True);
                    Assert.That(module.PrimaryActor.transform.position, Is.EqualTo(position));
                    Assert.That(module.PrimaryActor.Health.Current, Is.EqualTo(health));
                    yield return null;
                    CollectionAssert.AreEquivalent(originals, hub.world.Actors);
                    Assert.That(hub.Modules.Count(m => m.Role == HubRole.Controlled), Is.EqualTo(1));
                    Assert.That(hub.world.Actors.Count(a => a.team == 0), Is.EqualTo(1));
                    Assert.That(hub.world.Actors.Count(a => a.team == 1), Is.EqualTo(4));
                    Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                    Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                    var expected = module.Id == ClassId.Paul ? "Fighter" : module.Id.ToString();
                    var classMaps = new[] { "Graves", "Vendetta", "Rifle", "Sniper", "Fighter" };
                    var maps = InputSystem.ListEnabledActions().Select(a => a.actionMap.name).Where(classMaps.Contains).Distinct().ToArray();
                    Assert.That(maps, Does.Contain(expected));
                    Assert.That(maps.Where(m => m != "UI" && m != expected), Is.Empty);
                }
            Assert.That(originals[0].Health.Current, Is.EqualTo(originals[0].Health.Maximum - 7));
        }

        [UnityTest] public IEnumerator TransferDropsQueuedInputAndPreservesAmmo()
        {
            hub.SwitchCharacter(ClassId.Rifle);
            var actor = hub.ActiveModule.PrimaryActor;
            var rifle = actor.GetComponent<RifleController>();
            int ammo = rifle.Ammo;
            hub.world.Session.TransferControl(actor.Id);
            Assert.That(hub.world.Session.Submit(actor.Id, new PlayerCommand {
                Envelope = new CommandEnvelope { ActorId = actor.Id, Sequence = 10000, CommandType = CommandType.DirectControl },
                Direct = new DirectControlFrame { HeldButtons = ControlButtons.Fire, PressedButtons = ControlButtons.Fire }
            }), Is.True);
            hub.world.Step(1f / 60);
            Assert.That(rifle.Ammo, Is.LessThan(ammo));
            ammo = rifle.Ammo;
            hub.SwitchCharacter(ClassId.Sniper);
            hub.SwitchCharacter(ClassId.Rifle);
            Assert.That(rifle.Ammo, Is.EqualTo(ammo));
            Assert.That(hub.world.Session.Submit(actor.Id, new PlayerCommand {
                Envelope = new CommandEnvelope { ActorId = actor.Id, Sequence = 1, CommandType = CommandType.Stop }
            }), Is.True, "New input owner must not inherit old sequence numbers.");
            yield return null;
            Assert.That(rifle.Ammo, Is.EqualTo(ammo), "Old fire input survived transfer.");
        }

        [UnityTest] public IEnumerator DeadCharactersCannotBeSelectedAndResetRebuildsFive()
        {
            var actor = hub.ActiveModule.PrimaryActor;
            actor.Health.Apply(actor.Health.Maximum);
            Assert.That(hub.SwitchCharacter(actor.classId), Is.False);
            Assert.That(hub.SwitchCharacter(ClassId.Sniper), Is.True);
            hub.ResetActive();
            yield return null;
            Assert.That(hub.world.Actors.Count, Is.EqualTo(5));
            Assert.That(Object.FindObjectsByType<ActorSimulation>(FindObjectsSortMode.None).Length, Is.EqualTo(5));
            Assert.That(hub.world.Actors.All(a => a.Health.Current == a.Health.Maximum), Is.True);
        }

        [UnityTest] public IEnumerator HotkeysSwitchWithoutReloadAndMenuRestoresInput()
        {
            var priorBackground = InputSystem.settings.backgroundBehavior;
            var priorEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F11));
                InputSystem.Update();
                Assert.That(keyboard.f11Key.isPressed, Is.True, "Synthetic F11 must reach the test keyboard.");
                yield return null;
                yield return new WaitForFixedUpdate();
                Assert.That(hub.ActiveModule.Id, Is.EqualTo(ClassId.Sniper));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                hub.ShowSelect();
                Assert.That(hub.CursorMode, Is.EqualTo(HubCursorMode.Free));
                Assert.That(Object.FindFirstObjectByType<SniperInputReader>(), Is.Null);
                hub.ShowSelect();
                Assert.That(hub.CursorMode, Is.EqualTo(HubCursorMode.Locked));
                Assert.That(Object.FindFirstObjectByType<SniperInputReader>(), Is.Not.Null);
                Assert.That(hub.world.Actors.Count, Is.EqualTo(5));
            }
            finally {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = priorBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = priorEditor;
            }
        }

        [UnityTest] public IEnumerator EnemiesAttackTheControlledCharacter()
        {
            hub.EnemyAI = true;
            var target = hub.ActiveModule.PrimaryActor;
            for (int i = 0; i < 480 && target.Health.Current == target.Health.Maximum; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(target.Health.Current, Is.LessThan(target.Health.Maximum));
            Assert.That(hub.Modules.Single(m => m.Id == ClassId.Rifle).PrimaryActor.GetComponent<RifleController>().ShotsFired,
                Is.GreaterThan(0));
        }
    }
}
