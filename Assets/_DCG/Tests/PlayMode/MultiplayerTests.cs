using System.Collections;
using System.Linq;
using DCG.Core;
using DCG.Bootstrap.Hub;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DCG.Tests
{
    public sealed class MultiplayerTests
    {
        ControlHubController hub;
        MultiplayerController multiplayer;
        DuelMap map;
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_DCG/Scenes/Multiplayer.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            hub = Object.FindFirstObjectByType<ControlHubController>();
            multiplayer = Object.FindFirstObjectByType<MultiplayerController>();
            map = new GameObject("Test map").AddComponent<DuelMap>(); map.Build(0);
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            hub.ClearNetworkDuel(); Object.Destroy(map.gameObject); yield return null;
        }
        [UnityTest] public IEnumerator EveryPairIncludingMirrorsHasTwoIndependentActorsAndOneInputOwner()
        {
            for (int first = 0; first < 5; first++)
                for (int second = first; second < 5; second++)
                {
                    for (int local = 0; local < 2; local++)
                    {
                        hub.CreateNetworkDuel((ClassId)first, (ClassId)second, local, map.Grid);
                        yield return null;
                        Assert.That(hub.world.Actors.Count, Is.EqualTo(2));
                        var players = hub.NetworkPlayers;
                        Assert.That(players[0], Is.Not.SameAs(players[1]));
                        Assert.That(players[0].PrimaryActor.Id.Value, Is.EqualTo(1));
                        Assert.That(players[1].PrimaryActor.Id.Value, Is.EqualTo(2));
                        Assert.That(players[0].PrimaryActor.team, Is.Not.EqualTo(players[1].PrimaryActor.team));
                        Assert.That(hub.ActiveModule, Is.SameAs(players[local]));
                        Assert.That(players.Count(p => p.Role == HubRole.Controlled), Is.EqualTo(1));
                        Assert.That(players[0].PrimaryActor.Health.Maximum, Is.EqualTo(players[0].PrimaryActor.tuning.maxHealth));
                        players[0].PrimaryActor.Health.Apply(1);
                        Assert.That(players[1].PrimaryActor.Health.Current, Is.EqualTo(players[1].PrimaryActor.Health.Maximum));
                        Assert.That(InputSystem.ListEnabledActions().Any(a => new[] { "Graves", "Vendetta", "Rifle", "Sniper", "Fighter" }.Contains(a.actionMap.name)), Is.False, "No battle input before ready: " + first + "/" + second + " local=" + local + " enabled=" + string.Join(",", InputSystem.ListEnabledActions().Select(a => a.actionMap.name + "/" + a.name)));
                        Assert.That(hub.Select(ClassId.Graves), Is.False);
                        hub.ResetActive();
                        Assert.That(hub.world.Actors.Count, Is.EqualTo(2));
                    }
                }
        }
        [UnityTest] public IEnumerator PaulMirrorAdvancesOnceAndMapGridAvoidsCover()
        {
            hub.CreateNetworkDuel(ClassId.Paul, ClassId.Paul, 1, map.Grid);
            var first = (PaulModule)hub.NetworkPlayers[0]; var second = (PaulModule)hub.NetworkPlayers[1];
            hub.StepNetworkWorld();
            Assert.That(first.Fighter.Frame, Is.EqualTo(1)); Assert.That(second.Fighter.Frame, Is.EqualTo(1));
            Assert.That(first.Match.Frame, Is.EqualTo(1)); Assert.That(second.Match.Frame, Is.EqualTo(0));
            Assert.That(multiplayer.Submit(first.PrimaryActor.Id, new PlayerCommand()), Is.False);
            Assert.That(map.Grid.IsWalkable(map.Grid.Index(new Vector3(5, 0, 6))), Is.False);
            Assert.That(map.Grid.IsWalkable(map.Grid.Index(new Vector3(-8, 0, 0))), Is.True);
            Assert.That(map.Grid.IsWalkable(map.Grid.Index(new Vector3(8, 0, 0))), Is.True);
            yield return null;
        }
    }
}
