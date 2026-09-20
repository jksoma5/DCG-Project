using System.Collections;
using System.Collections.Generic;
using DCG.Core;
using DCG.Gameplay;
using DCG.Bootstrap;
using DCG.Bootstrap.Hub;
using DCG.Classes.Graves;
using DCG.Classes.Vendetta;
using DCG.Classes.Rifle;
using DCG.Classes.Sniper;
using DCG.Classes.Paul;
using DCG.Gameplay.Fighting;
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
    // Stage 3 adds the M416 and the TRG, so the switching rules are now checked over four classes and
    // over the first person view models and camera lens those two bring with them.
    // Stage 4 adds Paul, the one class the hub steps itself: the tick policy, the input update mode and
    // the frame counter all have to survive leaving him and coming back.
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
            // The hub steps the world itself, so that a fight frame and the world step keep a fixed
            // order even when two classes are live.
            Assert.That(hub.world.AutomaticTicks, Is.False);
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

        [UnityTest] public IEnumerator EveryPortedClassIsSelectable()
        {
            var expected = new[] { ClassId.Graves, ClassId.Vendetta, ClassId.Rifle, ClassId.Sniper, ClassId.Paul };
            foreach (var id in expected)
            {
                Assert.That(hub.Select(id), Is.True, "The hub does not offer " + id);
                yield return null;
                Assert.That(hub.ActiveModule.Id, Is.EqualTo(id));
                Assert.That(TeamCount(1), Is.GreaterThan(0), id + " has no practice targets.");
                Assert.That(Count<Camera>(), Is.EqualTo(1), id + " brought a second camera.");
                Assert.That(Count<AudioListener>(), Is.EqualTo(1));
            }
        }

        [UnityTest] public IEnumerator ShootingClassesStandOnTheLaneAndFaceTheTargets()
        {
            foreach (var id in new[] { ClassId.Rifle, ClassId.Sniper })
            {
                hub.Select(id);
                yield return null;
                var player = PlayerOf(0);
                Assert.That((player.transform.position - hub.map.laneStart.position).magnitude, Is.LessThan(1.5f),
                    id + " must start at the lane firing line.");
                // Every target is further down the lane than the firing line, so the lane is what is
                // being shot down rather than the plaza the other classes use.
                foreach (var actor in hub.world.Actors)
                    if (actor.team == 1)
                        Assert.That(actor.transform.position.z, Is.GreaterThan(player.transform.position.z),
                            id + " has a target behind the firing line.");
            }
        }

        [UnityTest] public IEnumerator FirstPersonViewModelsAndLensDoNotSurviveTheSwitch()
        {
            hub.Select(ClassId.Sniper);
            yield return null;
            Assert.That(Count<SniperInputReader>(), Is.EqualTo(1));
            Assert.That(Count<FirstPersonScopeRig>(), Is.EqualTo(1));
            int cameraChildren = hub.hubCamera.transform.childCount;
            Assert.That(cameraChildren, Is.GreaterThan(0), "The TRG carries view models on the camera.");

            hub.Select(ClassId.Rifle);
            yield return null;
            Assert.That(Count<SniperInputReader>(), Is.Zero);
            Assert.That(Count<FirstPersonScopeRig>(), Is.Zero);
            Assert.That(Count<ShoulderCameraRig>(), Is.EqualTo(1));
            Assert.That(Count<RifleInputReader>(), Is.EqualTo(1));

            hub.ShowSelect();
            yield return null;
            // Nothing a class hung on the shared camera may outlive it: not its weapons, not its
            // scope mask, not the lens it set.
            Assert.That(hub.hubCamera.transform.childCount, Is.Zero,
                "A class left view models on the shared camera.");
            Assert.That(Count<ShoulderCameraRig>(), Is.Zero);
            Assert.That(Count<RifleInputReader>(), Is.Zero);
        }

        [UnityTest] public IEnumerator SwitchingThroughEveryClassLeaksNothing()
        {
            var order = new[] { ClassId.Graves, ClassId.Rifle, ClassId.Paul, ClassId.Sniper, ClassId.Vendetta };
            hub.Select(ClassId.Graves);
            yield return null;
            int cameras = Count<Camera>();

            for (int round = 0; round < 2; round++)
                foreach (var id in order)
                {
                    hub.Select(id);
                    yield return null;
                    Assert.That(Count<Camera>(), Is.EqualTo(cameras), "Camera count grew at " + id);
                    Assert.That(Count<AudioListener>(), Is.EqualTo(1), "AudioListener count grew at " + id);
                    Assert.That(Count<GravesInputReader>() + Count<VendettaInputReader>() +
                        Count<RifleInputReader>() + Count<SniperInputReader>() +
                        Count<FighterInputReader>(), Is.EqualTo(1),
                        "Exactly one input reader may be alive, at " + id);
                    var seen = new HashSet<uint>();
                    foreach (var actor in hub.world.Actors)
                        Assert.That(seen.Add(actor.Id.Value), Is.True, "Duplicate actor id at " + id);
                }

            hub.ShowSelect();
            yield return null;
            Assert.That(Count<ActorSimulation>(), Is.Zero);
            Assert.That(hub.hubCamera.transform.childCount, Is.Zero);
        }

        [UnityTest] public IEnumerator PaulRunsOnTheHubFixedTickAndHisStageIsTheEastWestAxis()
        {
            hub.Select(ClassId.Paul);
            yield return null;
            var paul = (PaulModule)hub.ActiveModule;
            Assert.That(paul.Match, Is.Not.Null);
            Assert.That(hub.world.AutomaticTicks, Is.False, "The hub drives the fighting loop itself.");
            Assert.That(paul.Match.second, Is.Not.Null, "Practising alone still puts a dummy on the stage.");
            Assert.That(hub.world.Actors.Count, Is.EqualTo(2), "A fight is always two fighters.");

            // Facing positions straddle the plaza centre along X, so screen right is east (doc 12, 1-1).
            var west = paul.Match.first.transform.position;
            var east = paul.Match.second.transform.position;
            Assert.That(west.x, Is.LessThan(east.x));
            Assert.That(west.z, Is.EqualTo(east.z).Within(.01f));
            Assert.That(paul.Match.first.Side, Is.Not.EqualTo(paul.Match.second.Side),
                "The two fighters must face each other.");

            int start = paul.Match.Frame;
            for (int step = 0; step < 30; step++) yield return new WaitForFixedUpdate();
            int advanced = paul.Match.Frame - start;
            // One fight frame per fixed tick, no FrameClock accumulation of its own.
            Assert.That(advanced, Is.InRange(25, 35), "The match advanced " + advanced + " frames in 30 ticks.");
        }

        // The hub owns the tick and the input update mode for the whole session, so leaving Paul is no
        // longer a handover: it is just a class ending. What has to hold is that the world keeps
        // advancing for whoever comes next (doc 14, conflict 3).
        [UnityTest] public IEnumerator LeavingPaulLeavesTheWorldAdvancingForTheNextClass()
        {
            var mode = UnityEngine.InputSystem.InputSystem.settings.updateMode;
            Assert.That(mode, Is.EqualTo(UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsManually),
                "The hub processes input events itself so a fight frame can sample them.");

            hub.Select(ClassId.Paul);
            yield return null;
            Assert.That(Count<FighterInputReader>(), Is.EqualTo(1));

            hub.Select(ClassId.Rifle);
            yield return null;
            Assert.That(Count<FighterInputReader>(), Is.Zero);
            Assert.That(UnityEngine.InputSystem.InputSystem.settings.updateMode, Is.EqualTo(mode),
                "A class must not take the input update mode from the hub.");

            uint tick = hub.world.Tick;
            for (int step = 0; step < 10; step++) yield return new WaitForFixedUpdate();
            Assert.That(hub.world.Tick, Is.GreaterThan(tick), "The following class did not advance.");

            hub.ShowSelect();
            yield return null;
            Assert.That(UnityEngine.InputSystem.InputSystem.settings.updateMode, Is.EqualTo(mode));
        }

        [UnityTest] public IEnumerator ComingBackToPaulStartsHisFramesOverCleanly()
        {
            hub.Select(ClassId.Paul);
            yield return null;
            for (int step = 0; step < 20; step++) yield return new WaitForFixedUpdate();
            var paul = (PaulModule)hub.ActiveModule;
            Assert.That(paul.Match.Frame, Is.GreaterThan(0));

            hub.Select(ClassId.Graves);
            yield return null;
            hub.Select(ClassId.Paul);
            yield return null;
            paul = (PaulModule)hub.ActiveModule;
            Assert.That(paul.Match.Frame, Is.LessThan(3), "A returning fight must start from frame zero.");
            Assert.That(paul.Match.first.State.Phase, Is.EqualTo(paul.Match.second.State.Phase),
                "Neither fighter may return mid-move.");
            Assert.That(paul.RecordedFrames, Is.Zero, "The tape belongs to the session that recorded it.");
            Assert.That(paul.Match.first.Actor.Health.Current,
                Is.EqualTo(paul.Match.second.Actor.Health.Current), "Both fighters start at full health.");

            // The frames keep coming after the round trip, at the same one-per-tick rate.
            int frame = paul.Match.Frame;
            for (int step = 0; step < 20; step++) yield return new WaitForFixedUpdate();
            Assert.That(paul.Match.Frame - frame, Is.InRange(15, 25));
        }

        [UnityTest] public IEnumerator PaulRecordsAndReplaysOnTheDummy()
        {
            hub.Select(ClassId.Paul);
            yield return null;
            var paul = (PaulModule)hub.ActiveModule;

            paul.SetRecording(true);
            for (int step = 0; step < 20; step++) yield return new WaitForFixedUpdate();
            Assert.That(paul.Recording, Is.True);
            Assert.That(paul.RecordedFrames, Is.GreaterThan(0), "Recording must fill the tape.");

            paul.StartReplay();
            Assert.That(paul.Recording, Is.False);
            Assert.That(paul.DummyMode, Is.EqualTo(FightDummyMode.Replay));
            // A replaying dummy is no longer held in place; that is what makes it play back movement.
            for (int step = 0; step < 5; step++) yield return new WaitForFixedUpdate();
            Assert.That(paul.Match.second.HoldPosition, Is.False);
        }

        // The condition doc 14 wrote down for stage 1 and nothing had actually been checking: the input
        // maps that are enabled are exactly the active class's, and nothing is enabled on the select
        // screen. Counting reader components was not enough - a reader whose OnEnable ran before its
        // asset was assigned is present and bound to nothing.
        [UnityTest] public IEnumerator EnabledInputMapsAreExactlyTheActiveClasses()
        {
            Assert.That(EnabledMaps(), Is.Empty, "The select screen must leave every game map disabled.");

            var expected = new Dictionary<ClassId, string[]> {
                { ClassId.Graves, new[] { "Graves", "UI" } },
                { ClassId.Vendetta, new[] { "Vendetta" } },
                { ClassId.Rifle, new[] { "Rifle" } },
                { ClassId.Sniper, new[] { "Sniper" } },
                { ClassId.Paul, new[] { "Fighter" } }
            };
            foreach (var pair in expected)
            {
                hub.Select(pair.Key);
                yield return null;
                var maps = EnabledMaps();
                Assert.That(maps, Is.EquivalentTo(pair.Value),
                    pair.Key + " has these maps enabled instead: " + string.Join(", ", maps));
            }

            hub.ShowSelect();
            yield return null;
            Assert.That(EnabledMaps(), Is.Empty, "A class left its input map enabled behind it.");
        }

        static List<string> EnabledMaps()
        {
            var names = new List<string>();
            foreach (var asset in Resources.FindObjectsOfTypeAll<UnityEngine.InputSystem.InputActionAsset>())
                foreach (var map in asset.actionMaps)
                    if (map.enabled) names.Add(map.name);
            return names;
        }

        // The goal of doc 14 section 11: two classes from different games alive in the same space, one
        // driven and one standing in as its opponent.
        [UnityTest] public IEnumerator PaulAndTheTrgFightInOneSpace()
        {
            Assert.That(hub.SelectDuel(ClassId.Paul, ClassId.Sniper), Is.True);
            yield return null;
            var paul = (PaulModule)hub.ActiveModule;
            Assert.That(hub.OpponentModule, Is.Not.Null);
            Assert.That(hub.OpponentModule.Id, Is.EqualTo(ClassId.Sniper));

            // Both actors are live in one world, and neither brings a practice target of its own.
            Assert.That(hub.world.Actors.Count, Is.EqualTo(2), "A duel is two actors, no target sets.");
            Assert.That(paul.Match.second, Is.Null, "The far side is not a fighter.");
            Assert.That(paul.Match.opponentActor, Is.EqualTo(hub.OpponentModule.PrimaryActor));

            // Only the driven class has a camera rig, an input reader and a HUD.
            Assert.That(Count<FighterInputReader>(), Is.EqualTo(1));
            Assert.That(Count<SniperInputReader>(), Is.Zero, "The opponent is not being driven.");
            Assert.That(Count<FirstPersonScopeRig>(), Is.Zero);
            Assert.That(Count<FightCameraRig>(), Is.EqualTo(1));
            Assert.That(Count<Camera>(), Is.EqualTo(1));

            // The plane runs between the two, on the bearing they happen to be on - here north-south,
            // across the map's east-west axis.
            Vector3 between = hub.OpponentModule.PrimaryActor.transform.position - paul.Match.first.transform.position;
            between.y = 0;
            Assert.That(Vector3.Dot(paul.Match.Plane.Axis, between.normalized), Is.GreaterThan(.95f));

            // The fight loop and the world step both run, in one tick, without either side being
            // stepped twice: the fighter advances his own frames, the TRG is stepped by the world.
            int frame = paul.Match.Frame;
            uint tick = hub.world.Tick;
            float groundedY = hub.OpponentModule.PrimaryActor.transform.position.y;
            for (int step = 0; step < 30; step++) yield return new WaitForFixedUpdate();
            Assert.That(paul.Match.Frame - frame, Is.InRange(25, 35), "The fight frames stopped.");
            Assert.That(hub.world.Tick - tick, Is.InRange(25u, 35u), "The world stopped.");
            Assert.That(hub.OpponentModule.PrimaryActor.transform.position.y,
                Is.EqualTo(groundedY).Within(.05f), "The opponent should be standing, not sinking.");
            Assert.That(paul.Match.first.transform.position.y, Is.EqualTo(0).Within(.2f),
                "A fighter stepped twice in one tick falls through his own gravity.");

            hub.ShowSelect();
            yield return null;
            Assert.That(Count<ActorSimulation>(), Is.Zero, "Leaving a duel must remove both sides.");
            Assert.That(Count<FightCameraRig>(), Is.Zero);
        }

        [UnityTest] public IEnumerator DrivingTheTrgAgainstPaulIsTheSameSpaceTheOtherWayRound()
        {
            Assert.That(hub.SelectDuel(ClassId.Sniper, ClassId.Paul), Is.True);
            yield return null;
            Assert.That(hub.ActiveModule.Id, Is.EqualTo(ClassId.Sniper));
            Assert.That(hub.OpponentModule.Id, Is.EqualTo(ClassId.Paul));
            Assert.That(hub.world.Actors.Count, Is.EqualTo(2));

            // The TRG is driven, so it owns the camera and the input; Paul stands in without either but
            // keeps his own frame loop running.
            Assert.That(Count<SniperInputReader>(), Is.EqualTo(1));
            Assert.That(Count<FighterInputReader>(), Is.Zero);
            Assert.That(Count<FirstPersonScopeRig>(), Is.EqualTo(1));
            Assert.That(Count<FightCameraRig>(), Is.Zero);

            var paul = (PaulModule)hub.OpponentModule;
            int frame = paul.Match.Frame;
            for (int step = 0; step < 20; step++) yield return new WaitForFixedUpdate();
            Assert.That(paul.Match.Frame - frame, Is.GreaterThan(10),
                "A standing fighter still needs his frames, or he cannot fall or react.");
            // He is shootable: the gun classes look for a hurtbox.
            var hurtbox = paul.Match.first.transform.Find("Hurtbox");
            Assert.That(hurtbox, Is.Not.Null, "A fighter must have a hurtbox to be shot at.");
            Assert.That(hurtbox.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Hurtbox")));
        }

        [UnityTest] public IEnumerator ClassesThatCannotStandInAsOpponentsAreRefused()
        {
            // Only the classes prepared for it can be an opponent yet; the hub says no rather than
            // building a half-live class.
            Assert.That(hub.SelectDuel(ClassId.Paul, ClassId.Graves), Is.False);
            Assert.That(hub.SelectDuel(ClassId.Paul, ClassId.Paul), Is.False);
            Assert.That(hub.Selecting, Is.True, "A refused duel must not start anything.");
            yield return null;
        }

        ActorSimulation PlayerOf(int team)
        {
            foreach (var actor in hub.world.Actors) if (actor.team == team) return actor;
            return null;
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
