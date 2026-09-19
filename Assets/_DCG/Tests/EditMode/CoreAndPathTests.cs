using System;
using DCG.Core;
using DCG.Gameplay;
using DCG.Gameplay.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace DCG.Tests
{
    public sealed class CoreAndPathTests
    {
        GridGraph grid;
        [SetUp] public void Setup()
        {
            grid = ScriptableObject.CreateInstance<GridGraph>();
            grid.Initialize(8, 8, 1, Vector3.zero);
        }
        [TearDown] public void Cleanup() { UnityEngine.Object.DestroyImmediate(grid); }
        [Test] public void PathGoesAroundWallAndNeverVisitsBlockedCells()
        {
            for (int z = 0; z < 6; z++) grid.SetWalkable(3, z, false);
            var result = new AStarPathService(grid).Find(grid.Position(0), grid.Position(7), 3);
            Assert.That(result.Status, Is.EqualTo(PathStatus.Complete));
            Assert.That(result.Points.Exists(p => p.z > 6), Is.True);
            foreach (var p in result.Points) Assert.That(grid.IsWalkable(grid.Index(p)), Is.True);
        }
        [Test] public void DiagonalCannotPassBetweenTwoBlockedCorners()
        {
            grid.Initialize(2, 2, 1, Vector3.zero);
            grid.SetWalkable(1, 0, false); grid.SetWalkable(0, 1, false);
            var result = new AStarPathService(grid).Find(grid.Position(0), grid.Position(3), 1);
            Assert.That(result.Status, Is.EqualTo(PathStatus.Unreachable));
            Assert.That(result.Points, Is.Empty);
        }
        [Test] public void BlockedDestinationReturnsPartialRoute()
        {
            grid.SetWalkable(7, 7, false);
            var result = new AStarPathService(grid).Find(grid.Position(0), grid.Position(63), 1);
            Assert.That(result.Status, Is.EqualTo(PathStatus.Partial));
            Assert.That(result.Points.Count, Is.GreaterThan(0));
        }
        [Test] public void EmptyGraphIsUnreachable()
        {
            for (int z = 0; z < 8; z++) for (int x = 0; x < 8; x++) grid.SetWalkable(x, z, false);
            Assert.That(new AStarPathService(grid).Find(Vector3.zero, Vector3.one, 1).Status,
                Is.EqualTo(PathStatus.Unreachable));
        }
        [Test] public void SearchBudgetStopsInsteadOfReturningFalseSuccess()
        {
            Assert.That(new AStarPathService(grid, 1).Find(grid.Position(0), grid.Position(63), 1).Status,
                Is.EqualTo(PathStatus.BudgetExceeded));
        }
        [Test] public void NonFiniteDestinationIsRejected()
        {
            Assert.That(new AStarPathService(grid).Find(Vector3.zero, new Vector3(float.NaN, 0, 1), 1).Status,
                Is.EqualTo(PathStatus.Unreachable));
        }
        [Test] public void NewOrderRejectsStalePathResultAndStopInvalidatesPendingResult()
        {
            var follower = new PathFollower();
            uint first = follower.BeginRequest();
            uint second = follower.BeginRequest();
            var old = new PathResult { RequestId = first, Status = PathStatus.Complete };
            old.Points.Add(Vector3.one);
            Assert.That(follower.Accept(old), Is.False);
            var current = new PathResult { RequestId = second, Status = PathStatus.Complete };
            current.Points.Add(Vector3.right);
            Assert.That(follower.Accept(current), Is.True);
            follower.Stop();
            Assert.That(follower.Accept(current), Is.False);
            Assert.That(follower.HasPath, Is.False);
        }
        [Test] public void HealthRejectsInvalidDamageAndDiesOnlyOnce()
        {
            var health = new HealthState(100); int deaths = 0;
            health.Died += () => deaths++;
            Assert.That(health.Apply(float.NaN), Is.False);
            Assert.That(health.Apply(float.PositiveInfinity), Is.False);
            Assert.That(health.Apply(-20), Is.False);
            health.Apply(200); health.Apply(30);
            Assert.That(health.Current, Is.Zero);
            Assert.That(deaths, Is.EqualTo(1));
        }
        [Test] public void SequenceOrderingHandlesWrapAndRejectsReplay()
        {
            Assert.That(CommandValidation.IsNewer(0, uint.MaxValue), Is.True);
            Assert.That(CommandValidation.IsNewer(uint.MaxValue, 0), Is.False);
            Assert.That(CommandValidation.IsNewer(4, 4), Is.False);
        }
    }
}
