using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;
namespace DCG.Tests
{
    public sealed class SniperInputTests
    {
        [Test] public void SniperUses123AndShiftWalkCtrlCrouch()
        {
            var actions=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_DCG/Input/DCGControls.inputactions");
            for(int i=1;i<=3;i++)Assert.That(actions.FindAction("Sniper/Slot"+i).bindings[0].path,Is.EqualTo("<Keyboard>/"+i));
            Assert.That(actions.FindAction("Sniper/Walk").bindings[0].path,Is.EqualTo("<Keyboard>/leftShift"));
            Assert.That(actions.FindAction("Sniper/Crouch").bindings[0].path,Is.EqualTo("<Keyboard>/leftCtrl"));
            Assert.That(actions.FindAction("Sniper/Sprint"),Is.Null);
            Assert.That(actions.FindAction("Rifle/Sprint"),Is.Not.Null);
        }
    }
}
