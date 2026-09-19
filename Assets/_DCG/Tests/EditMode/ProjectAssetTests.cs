using DCG.Core;
using DCG.Gameplay;
using DCG.Gameplay.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace DCG.Tests
{
    public sealed class ProjectAssetTests
    {
        [Test] public void RestoredPipelineAndInputAssetsAreLoadable()
        {
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"), Is.Not.Null);
        }
        [Test] public void ClassInputMapsExistAndGravesMovementIsPointerBased()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_DCG/Input/DCGControls.inputactions");
            foreach (string name in new[] { "UI", "Graves", "Vendetta", "Rifle", "Sniper" })
                Assert.That(actions.FindActionMap(name), Is.Not.Null);
            Assert.That(actions.FindAction("Graves/Command").bindings[0].path, Is.EqualTo("<Mouse>/rightButton"));
            Assert.That(actions.FindAction("Graves/Move"), Is.Null);
        }
        [Test] public void ImplementedClassesHavePrototypeAssets()
        {
            foreach (var id in new[] { ClassId.Graves, ClassId.Vendetta, ClassId.Rifle, ClassId.Sniper })
            {
                var definition = AssetDatabase.LoadAssetAtPath<ClassDefinition>("Assets/_DCG/Data/Classes/" + id + ".asset");
                Assert.That(definition, Is.Not.Null);
                Assert.That(definition.actorPrefab, Is.Not.Null);
                Assert.That(definition.playableInLab, Is.True);
                Assert.That(definition.referenceStatus, Is.EqualTo(ReferenceStatus.TuningPending));
            }
        }
        [Test] public void LabGridIsBakedAndHasObstacles()
        {
            var grid = AssetDatabase.LoadAssetAtPath<GridGraph>("Assets/_DCG/Data/Navigation/ControlLabGrid.asset");
            Assert.That(grid.IsBaked, Is.True);
            Assert.That(grid.IsWalkable(grid.Index(new Vector3(-9, 0, -6))), Is.True);
            Assert.That(grid.IsWalkable(grid.Index(new Vector3(-3, 0, -1))), Is.False);
        }
    }
}
