using System.Collections;
using System.Collections.Generic;
using DCG.Core;
using DCG.Gameplay;
using DCG.Bootstrap.Hub;
using DCG.Classes.Graves;
using DCG.Classes.Vendetta;
using DCG.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DCG.Tests
{
    // Stage 1 completion conditions from doc 14: switching between two classes in one scene must leave
    // exactly one input reader, one camera and one HUD alive, and must not leak actors.
    public sealed class ControlHubTests
    {
        ControlHubController hub;

        [UnitySetUp] public IEnumerator Setup()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_DCG/Scenes/ControlHub.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            hub = Object.FindFirstObjectByType<ControlHubController>();
            Assert.That(hub, Is.Not.Null, "ControlHub scene has no hub controller.");
        }

        static int Count<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;

        [UnityTest] public IEnumerator SelectScreenStartsWithNoClassLive()
        {
            Assert.That(hub.Selecting, Is.True);
            Assert.That(hub.world.Actors.Count, Is.Zero, "Select screen must not have actors in the world.");
            Assert.That(Count<ActorSimulation>(), Is.Zero);
            Assert.That(hub.world.AutomaticTicks, Is.False, "Nothing should tick while selecting.");
            Assert.That(Count<GravesInputReader>(), Is.Zero);
            Assert.That(Count<VendettaInputReader>(), Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator SelectingAClassActivatesOnlyThatClass()
        {
            Assert.That(hub.Select(ClassId.Graves), Is.True);
            yield return null;
            Assert.That(hub.ActiveModule.Id, Is.EqualTo(ClassId.Graves));
            Assert.That(Count<GravesInputReader>(), Is.EqualTo(1));
            Assert.That(Count<VendettaInputReader>(), Is.Zero);
            Assert.That(Count<DebugOverlay>(), Is.EqualTo(1));
            Assert.That(hub.world.AutomaticTicks, Is.True);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "Graves points with a free cursor.");
            int actors = hub.world.Actors.Count;
            Assert.That(actors, Is.GreaterThan(1), "Graves needs a player and at least one target.");

            hub.Select(ClassId.Vendetta);
            yield return null;
            Assert.That(hub.ActiveModule.Id, Is.EqualTo(ClassId.Vendetta));
            Assert.That(Count<GravesInputReader>(), Is.Zero, "The previous class must release its input reader.");
            Assert.That(Count<VendettaInputReader>(), Is.EqualTo(1));
            Assert.That(Count<DebugOverlay>(), Is.Zero, "Only the active class may draw its HUD.");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            Assert.That(hub.world.Actors.Count, Is.EqualTo(actors));
        }

        [UnityTest] public IEnumerator RepeatedSwitchingLeaksNothing()
        {
            hub.Select(ClassId.Graves);
            yield return null;
            int actors = hub.world.Actors.Count;
            int objects = Count<ActorSimulation>();

            for (int round = 0; round < 3; round++)
            {
                hub.Select(ClassId.Vendetta);
                yield return null;
                hub.Select(ClassId.Graves);
                yield return null;
                Assert.That(hub.world.Actors.Count, Is.EqualTo(actors), "World registration grew on round " + round);
                Assert.That(Count<ActorSimulation>(), Is.EqualTo(objects), "Actor objects leaked on round " + round);
                Assert.That(Count<GravesInputReader>(), Is.EqualTo(1));
                Assert.That(Count<VendettaInputReader>(), Is.Zero);
                Assert.That(Count<Camera>(), Is.EqualTo(1), "The hub owns exactly one camera.");
                Assert.That(Count<AudioListener>(), Is.EqualTo(1));
            }
        }

        [UnityTest] public IEnumerator ActorIdsStayUniqueAcrossSwitches()
        {
            for (int round = 0; round < 3; round++)
            {
                hub.Select(round % 2 == 0 ? ClassId.Graves : ClassId.Vendetta);
                yield return null;
                var seen = new HashSet<uint>();
                foreach (var actor in hub.world.Actors)
                    Assert.That(seen.Add(actor.Id.Value), Is.True, "Duplicate actor id on round " + round);
            }
        }

        [UnityTest] public IEnumerator LeavingAClassReturnsToAnEmptySelectScreen()
        {
            hub.Select(ClassId.Vendetta);
            yield return null;
            Assert.That(hub.Selecting, Is.False);

            hub.ShowSelect();
            yield return null;
            Assert.That(hub.Selecting, Is.True);
            Assert.That(hub.ActiveModule, Is.Null);
            Assert.That(hub.world.Actors.Count, Is.Zero);
            Assert.That(Count<ActorSimulation>(), Is.Zero, "Leaving a class must remove its actors.");
            Assert.That(Count<VendettaInputReader>(), Is.Zero);
            Assert.That(hub.world.AutomaticTicks, Is.False);
        }

        [UnityTest] public IEnumerator ResetRebuildsTheActiveClassWithoutReloadingTheScene()
        {
            hub.Select(ClassId.Graves);
            yield return null;
            var scene = SceneManager.GetActiveScene();
            int actors = hub.world.Actors.Count;

            hub.ResetActive();
            yield return null;
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene), "Reset must not reload the scene.");
            Assert.That(hub.ActiveModule.Id, Is.EqualTo(ClassId.Graves));
            Assert.That(hub.world.Actors.Count, Is.EqualTo(actors));
            Assert.That(Count<GravesInputReader>(), Is.EqualTo(1));
        }
    }
}
