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
    // Stage 2 adds the unified map: the anchors a module builds on, a nav grid that covers the plaza
    // and stops at the lane, and target sets that belong to the module rather than to the scene.
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
            Assert.That(hub.CursorMode, Is.EqualTo(HubCursorMode.Free), "Graves points with a free cursor.");
            int actors = hub.world.Actors.Count;
            Assert.That(actors, Is.GreaterThan(1), "Graves needs a player and at least one target.");

            hub.Select(ClassId.Vendetta);
            yield return null;
            Assert.That(hub.ActiveModule.Id, Is.EqualTo(ClassId.Vendetta));
            Assert.That(Count<GravesInputReader>(), Is.Zero, "The previous class must release its input reader.");
            Assert.That(Count<VendettaInputReader>(), Is.EqualTo(1));
            Assert.That(Count<DebugOverlay>(), Is.Zero, "Only the active class may draw its HUD.");
            Assert.That(hub.CursorMode, Is.EqualTo(HubCursorMode.Locked));
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

        [UnityTest] public IEnumerator UnifiedMapExposesEveryAnchor()
        {
            var map = hub.map;
            Assert.That(map.Complete, Is.True, "A module builds on the anchors; none may be missing.");
            // Paul's stage lies on the map's east-west axis so screen right is always east (doc 12, 1-1).
            Assert.That(map.fightWest.position.z, Is.EqualTo(map.fightEast.position.z).Within(.001f));
            Assert.That(map.fightWest.position.x, Is.LessThan(map.fightEast.position.x));
            // The long lane runs north-south, across the fighting axis rather than along it.
            Assert.That(map.laneEnd.position.z, Is.GreaterThan(map.laneStart.position.z + 20));
            Assert.That(map.laneEnd.position.x, Is.EqualTo(map.laneStart.position.x).Within(.001f));
            // High ground is raised and sits opposite the lane.
            Assert.That(map.highGround.position.y, Is.GreaterThan(1));
            Assert.That(map.highGround.position.z, Is.LessThan(map.plazaCenter.position.z));
            yield return null;
        }

        [UnityTest] public IEnumerator NavGridCoversThePlazaAndStopsAtTheLane()
        {
            var graves = Object.FindFirstObjectByType<GravesModule>();
            Assert.That(graves.grid, Is.Not.Null);
            Assert.That(graves.grid.IsBaked, Is.True, "The hub grid must be baked by the generator.");
            Assert.That(graves.grid.Index(hub.map.plazaCenter.position), Is.Not.EqualTo(-1));
            Assert.That(graves.grid.Index(hub.map.gravesSpawn.position), Is.Not.EqualTo(-1),
                "Graves must spawn inside the grid.");
            Assert.That(graves.grid.Index(hub.map.laneEnd.position), Is.EqualTo(-1),
                "The lane is deliberately outside the grid; its boundary is drawn on the floor.");
            yield return null;
        }

        [UnityTest] public IEnumerator ModulesBringTheirOwnTargetsAndTakeThemAway()
        {
            // The shared map holds no targets, so the count before any class is chosen is zero.
            Assert.That(Count<ActorSimulation>(), Is.Zero);

            hub.Select(ClassId.Graves);
            yield return null;
            int gravesTargets = TeamCount(1);
            Assert.That(gravesTargets, Is.GreaterThan(1), "Graves practises against a moving and static targets.");

            hub.Select(ClassId.Vendetta);
            yield return null;
            Assert.That(TeamCount(1), Is.GreaterThan(1));
            // Vendetta's sword-throw destination is the shared high ground, so one target stands on it.
            bool onHighGround = false;
            foreach (var actor in hub.world.Actors)
                if (actor.team == 1 && actor.transform.position.y > hub.map.highGround.position.y - .5f)
                    onHighGround = true;
            Assert.That(onHighGround, Is.True, "Vendetta needs a target on the high ground to fly to.");

            hub.ShowSelect();
            yield return null;
            Assert.That(Count<ActorSimulation>(), Is.Zero, "Leaving a class must take its target set with it.");
        }

        [UnityTest] public IEnumerator GravesPatrolMovesItsOwnTarget()
        {
            hub.Select(ClassId.Graves);
            yield return null;
            var graves = (GravesModule)hub.ActiveModule;
            Assert.That(graves.MovingTarget, Is.Not.Null);
            Assert.That(graves.PatrolRunning, Is.False, "A class starts with the patrol off.");

            Vector3 start = graves.MovingTarget.transform.position;
            graves.TogglePatrol();
            Assert.That(graves.PatrolRunning, Is.True);
            // Wait on fixed steps, not rendered frames: a batch run with no graphics renders frames far
            // faster than real time, so 120 rendered frames advance the simulation by about two ticks.
            for (int step = 0; step < 180; step++) yield return new WaitForFixedUpdate();
            Assert.That((graves.MovingTarget.transform.position - start).magnitude, Is.GreaterThan(.5f),
                "The patrol must actually order the moving target somewhere.");

            graves.TogglePatrol();
            Assert.That(graves.PatrolRunning, Is.False);
        }

        int TeamCount(int team)
        {
            int count = 0;
            foreach (var actor in hub.world.Actors) if (actor.team == team) count++;
            return count;
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
