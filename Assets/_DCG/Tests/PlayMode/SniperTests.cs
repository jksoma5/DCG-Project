using System.Collections;
using DCG.Core;
using DCG.Gameplay;
using DCG.Classes.Sniper;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace DCG.Tests
{
    public sealed class SniperTests
    {
        SimulationWorld world;ActorSimulation actor;SniperController sniper;uint sequence;
        [UnitySetUp] public IEnumerator Setup()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_DCG/Scenes/SniperLab.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("SniperLab");
#endif
            yield return null;world=Object.FindFirstObjectByType<SimulationWorld>();world.AutomaticTicks=false;
            actor=world.Find(new ActorId(301));sniper=actor.GetComponent<SniperController>();
            Object.FindFirstObjectByType<SniperInputReader>().enabled=false;sequence=10000;Step(2);
        }
        PlayerCommand Packet(CommandType type)=>new PlayerCommand{Envelope=new CommandEnvelope{ActorId=actor.Id,Sequence=++sequence,CommandType=type}};
        void Frame(ControlButtons pressed=0,ControlButtons held=0,Vector2 move=default)
        {
            var p=Packet(CommandType.DirectControl);p.Direct=new DirectControlFrame{PressedButtons=pressed,HeldButtons=held,MoveAxes=move};
            Assert.That(world.Session.Submit(actor.Id,p),Is.True);
        }
        void Slot(int slot,int steps=12){var p=Packet(CommandType.Action);p.Action.ActionId=slot;Assert.That(world.Session.Submit(actor.Id,p),Is.True);Step(steps);}
        void Step(int count){for(int i=0;i<count;i++){world.Step(.02f);Physics.SyncTransforms();}}
        void Place(Vector3 position)
        {
            var cc=actor.GetComponent<CharacterController>();cc.enabled=false;actor.transform.position=position;cc.enabled=true;Physics.SyncTransforms();Step(2);
        }
        [Test] public void SlotsSelectThreeWeaponsAndKeepIndependentAmmo()
        {
            Frame(ControlButtons.Aim);Step(1);Assert.That(sniper.ScopeLevel,Is.EqualTo(1));
            Frame(ControlButtons.Fire);Step(1);Assert.That(sniper.Ammo,Is.EqualTo(4));Assert.That(sniper.ScopeLevel,Is.Zero);
            Slot(2);Assert.That(sniper.Weapon,Is.EqualTo(SniperWeapon.Pistol));
            Frame(ControlButtons.Fire);Step(1);Assert.That(sniper.Ammo,Is.EqualTo(11));
            Slot(3);Assert.That(sniper.Weapon,Is.EqualTo(SniperWeapon.Knife));Assert.That(sniper.Ammo,Is.Zero);
            Slot(1);Assert.That(sniper.Ammo,Is.EqualTo(4));Assert.That(sniper.AmmoFor(SniperWeapon.Pistol),Is.EqualTo(11));
            Assert.That(actor.Snapshot(world.Tick).Ammo,Is.EqualTo(4));
        }
        [Test] public void SniperScopeCyclesAndScopedShotHits()
        {
            for(int level=1;level<=3;level++){Frame(ControlButtons.Aim);Step(1);Assert.That(sniper.ScopeLevel,Is.EqualTo(level%3));}
            Frame(ControlButtons.Aim);Step(1);Frame(ControlButtons.Fire);Step(1);
            Assert.That(world.Find(new ActorId(302)).Health.Current,Is.EqualTo(0));Assert.That(sniper.ScopeLevel,Is.Zero);
            Frame(ControlButtons.Fire);Step(1);Assert.That(sniper.ShotsFired,Is.EqualTo(1));
            Slot(2);Slot(1);Frame(ControlButtons.Fire);Step(1);Assert.That(sniper.ShotsFired,Is.EqualTo(1),"Switching must not reset the sniper recovery.");
        }
        [Test] public void PistolRequiresNewPressAndReloadConservesAmmo()
        {
            Slot(2);Frame(ControlButtons.Fire);Step(1);
            Frame(0,ControlButtons.Fire);Step(20);Assert.That(sniper.ShotsFired,Is.EqualTo(1));
            int total=sniper.Ammo+sniper.Reserve;Frame(ControlButtons.Reload);Step(1);
            Frame(ControlButtons.Fire);Step(5);Assert.That(sniper.ShotsFired,Is.EqualTo(1));
            Step(90);Assert.That(sniper.Ammo,Is.EqualTo(12));Assert.That(sniper.Ammo+sniper.Reserve,Is.EqualTo(total));
        }
        [Test] public void WeaponSwitchCancelsReloadAndBlocksAttackDuringDraw()
        {
            Frame(ControlButtons.Fire);Step(1);Frame(ControlButtons.Reload);Step(1);Assert.That(sniper.ReloadRemaining,Is.GreaterThan(0));
            Slot(2,1);Assert.That(sniper.ReloadRemaining,Is.Zero);
            Frame(ControlButtons.Fire);Step(1);Assert.That(sniper.Ammo,Is.EqualTo(12));
            Step(15);Slot(1);Assert.That(sniper.Ammo,Is.EqualTo(4));
        }
        [Test] public void MovementIsImmediateWalkIsSlowerAndKnifeIsFaster()
        {
            Frame(0,0,Vector2.up);Step(1);float sniperSpeed=actor.Motor.Velocity.z;
            Frame(0,ControlButtons.Walk,Vector2.up);Step(1);Assert.That(actor.Motor.Velocity.z,Is.LessThan(sniperSpeed));
            Slot(3);Frame(0,0,Vector2.up);Step(1);Assert.That(actor.Motor.Velocity.z,Is.GreaterThan(sniperSpeed));
            Frame();Step(1);Assert.That(Mathf.Abs(actor.Motor.Velocity.z),Is.LessThan(.001f));
            Frame(ControlButtons.Jump);Step(3);Assert.That(actor.transform.position.y,Is.GreaterThan(.1f));
        }
        [Test] public void CrouchUsesHoldAndStandingChecksCeiling()
        {
            Frame(0,ControlButtons.Crouch);Step(1);Assert.That(sniper.Crouched,Is.True);
            Place(new Vector3(-11,0,0));Frame();Step(1);Assert.That(sniper.Crouched,Is.True);
            Place(new Vector3(0,0,-8));Frame();Step(1);Assert.That(sniper.Crouched,Is.False);
        }
        [Test] public void KnifeRangeAndCoverPreventRemoteDamage()
        {
            Slot(3);Frame(ControlButtons.Fire);Step(1);var target=world.Find(new ActorId(305));
            Assert.That(target.Health.Current,Is.EqualTo(100));
            Place(new Vector3(-4,0,-6.3f));Step(25);Frame(ControlButtons.Fire);Step(1);
            Assert.That(target.Health.Current,Is.EqualTo(100-sniper.tuning.knifeDamage));
            Step(25);Frame(ControlButtons.Aim);Step(1);Assert.That(target.Health.Current,Is.EqualTo(0));
            Place(new Vector3(6,0,3.5f));Slot(1);Frame(ControlButtons.Aim);Step(1);Frame(ControlButtons.Fire);Step(1);
            Assert.That(world.Find(new ActorId(304)).Health.Current,Is.EqualTo(100));
        }
        [Test] public void TimeoutInvalidInputAndDeathAreHandled()
        {
            Frame(0,0,Vector2.right);Step(30);float x=actor.transform.position.x;Step(20);
            Assert.That(actor.transform.position.x,Is.EqualTo(x).Within(.001f));
            var p=Packet(CommandType.DirectControl);p.Direct.AimYawPitch=new Vector2(float.NaN,0);
            Assert.That(world.Session.Submit(actor.Id,p),Is.False);
            p=Packet(CommandType.Action);p.Action.ActionId=4;Assert.That(world.Session.Submit(actor.Id,p),Is.False);
            Frame(ControlButtons.Fire);Step(1);Frame(ControlButtons.Reload);Step(1);
            world.Damage.Apply(new DamageRequest(new ActorId(302),actor.Id,999,1000),actor);Step(1);
            Assert.That(sniper.ReloadRemaining,Is.Zero);Assert.That(sniper.ScopeLevel,Is.Zero);
        }
    }
}
