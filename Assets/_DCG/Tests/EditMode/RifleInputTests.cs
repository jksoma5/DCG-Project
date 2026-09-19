using DCG.Classes.Rifle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;
namespace DCG.Tests
{
    public sealed class RifleInputTests
    {
        [Test] public void TapTogglesAdsAndHoldReleasesShoulderWithoutAds()
        {
            var aim=new RifleAimGesture();
            aim.Step(true,false,true,0,.18f);aim.Step(false,true,false,.08f,.18f);
            Assert.That(aim.Ads,Is.True);Assert.That(aim.Shoulder,Is.False);
            aim.Step(true,false,true,1,.18f);aim.Step(false,false,true,1.2f,.18f);
            Assert.That(aim.Shoulder,Is.True);Assert.That(aim.Ads,Is.False);
            aim.Step(false,true,false,1.3f,.18f);
            Assert.That(aim.Shoulder,Is.False);Assert.That(aim.Ads,Is.False);
            aim.Step(true,false,true,2,.18f);aim.Step(false,true,false,2.05f,.18f);aim.Reset();
            Assert.That(aim.Ads,Is.False);
        }
        [Test] public void RifleBindingsUsePubgKeysWithoutVendettaSkills()
        {
            var actions=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_DCG/Input/DCGControls.inputactions");
            Assert.That(actions.FindAction("Rifle/Aim").bindings[0].path,Is.EqualTo("<Mouse>/rightButton"));
            Assert.That(actions.FindAction("Rifle/Reload").bindings[0].path,Is.EqualTo("<Keyboard>/r"));
            Assert.That(actions.FindAction("Rifle/FireMode").bindings[0].path,Is.EqualTo("<Keyboard>/b"));
            Assert.That(actions.FindAction("Rifle/LeanRight").bindings[0].path,Is.EqualTo("<Keyboard>/e"));
            Assert.That(actions.FindAction("Vendetta/E"),Is.Not.Null);
        }
    }
}
