using System.Collections;
using DCG.Core;
using DCG.Gameplay;
using DCG.Classes.Rifle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DCG.Tests
{
    public sealed class RifleTests
    {
        SimulationWorld world; ActorSimulation actor; RifleController rifle; uint sequence;
        [UnitySetUp] public IEnumerator Setup()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_DCG/Scenes/RifleLab.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("RifleLab");
#endif
            yield return null;
            world=Object.FindFirstObjectByType<SimulationWorld>();world.AutomaticTicks=false;
            actor=world.Find(new ActorId(201));rifle=actor.GetComponent<RifleController>();
            Object.FindFirstObjectByType<RifleInputReader>().enabled=false;sequence=10000;
            Step(2);
        }
        PlayerCommand Packet(CommandType type)=>new PlayerCommand{
            Envelope=new CommandEnvelope{ActorId=actor.Id,Sequence=++sequence,CommandType=type}};
        void Input(ControlButtons held=0,ControlButtons pressed=0,Vector2 move=default,Vector3 target=default)
        {
            var p=Packet(CommandType.DirectControl);
            p.Direct=new DirectControlFrame{HeldButtons=held,PressedButtons=pressed,MoveAxes=move,
                AimTarget=target==Vector3.zero?world.Find(new ActorId(202)).AimPoint:target};
            Assert.That(world.Session.Submit(actor.Id,p),Is.True);
        }
        void Action(int id)
        {
            var p=Packet(CommandType.Action);p.Action.ActionId=id;Assert.That(world.Session.Submit(actor.Id,p),Is.True);Step(1);
        }
        void Step(int count){for(int i=0;i<count;i++){world.Step(.02f);Physics.SyncTransforms();}}
        void Place(Vector3 at)
        {
            var cc=actor.GetComponent<CharacterController>();cc.enabled=false;actor.transform.position=at;cc.enabled=true;Physics.SyncTransforms();Step(2);
        }
        [Test] public void AutomaticFireDamagesAndConsumesAmmo()
        {
            for(int i=0;i<25;i++){Input(ControlButtons.Fire|ControlButtons.Ads);Step(1);}
            Assert.That(rifle.ShotsFired,Is.InRange(4,7));
            Assert.That(rifle.Ammo,Is.EqualTo(rifle.tuning.magazineSize-rifle.ShotsFired));
            Assert.That(world.Find(new ActorId(202)).Health.Current,Is.LessThan(100));
            Assert.That(rifle.Recoil,Is.GreaterThan(0));
            Assert.That(actor.Snapshot(world.Tick).Ammo,Is.EqualTo(rifle.Ammo));
        }
        [Test] public void SingleRequiresFreshPressAndReloadConservesAmmo()
        {
            Action(RifleController.ToggleFireMode);
            Input(ControlButtons.Fire,ControlButtons.Fire);Step(1);
            for(int i=0;i<20;i++){Input(ControlButtons.Fire);Step(1);}
            Assert.That(rifle.ShotsFired,Is.EqualTo(1));
            int total=rifle.Ammo+rifle.Reserve;
            Input(0,ControlButtons.Reload);Step(1);
            Assert.That(rifle.ReloadRemaining,Is.GreaterThan(0));
            Input(ControlButtons.Fire,ControlButtons.Fire);Step(10);
            Assert.That(rifle.ShotsFired,Is.EqualTo(1));
            Step(130);
            Assert.That(rifle.Ammo,Is.EqualTo(rifle.tuning.magazineSize));
            Assert.That(rifle.Ammo+rifle.Reserve,Is.EqualTo(total));
        }
        [Test] public void ShoulderAdsAndSprintRespectMovementModes()
        {
            Input(ControlButtons.Sprint,0,Vector2.up);Step(1);
            Assert.That(rifle.Sprinting,Is.True);
            float fast=actor.Motor.Velocity.z;
            Input(ControlButtons.Sprint|ControlButtons.Aim,0,Vector2.up);Step(1);
            Assert.That(rifle.Sprinting,Is.False);Assert.That(rifle.AimMode,Is.EqualTo(RifleAimMode.Shoulder));
            Assert.That(actor.Motor.Velocity.z,Is.LessThan(fast));
            Input(ControlButtons.Ads|ControlButtons.LeanLeft);Step(1);
            Assert.That(rifle.AimMode,Is.EqualTo(RifleAimMode.Ads));Assert.That(rifle.Lean,Is.EqualTo(-1));
        }
        [Test] public void StanceChangesCapsuleAndCeilingBlocksStanding()
        {
            Action(RifleController.ToggleCrouch);Assert.That(rifle.Height,Is.EqualTo(rifle.tuning.crouchHeight));
            Place(new Vector3(-11,0,0));
            Action(RifleController.ToggleCrouch);Assert.That(rifle.Stance,Is.EqualTo(RifleStance.Crouch));
            Place(new Vector3(0,0,-8));
            Action(RifleController.ToggleProne);Assert.That(rifle.Stance,Is.EqualTo(RifleStance.Prone));
            Assert.That(rifle.Height,Is.EqualTo(rifle.tuning.proneHeight));
            Action(RifleController.ToggleProne);Assert.That(rifle.Stance,Is.EqualTo(RifleStance.Stand));
        }
        [Test] public void CoverBlocksBulletsEvenWhenTargetIsSupplied()
        {
            Place(new Vector3(5,0,-3));var target=world.Find(new ActorId(204));
            for(int i=0;i<25;i++){Input(ControlButtons.Fire|ControlButtons.Ads,0,default,target.AimPoint);Step(1);}
            Assert.That(rifle.ShotsFired,Is.GreaterThan(0));Assert.That(target.Health.Current,Is.EqualTo(100));
        }
        [Test] public void TimeoutStopsHeldFireAndDeathCancelsReload()
        {
            Input(ControlButtons.Fire,0,Vector2.right);Step(30);
            int shots=rifle.ShotsFired;Vector3 pos=actor.transform.position;Step(30);
            Assert.That(rifle.ShotsFired,Is.EqualTo(shots));Assert.That(actor.transform.position.x,Is.EqualTo(pos.x).Within(.01));
            Input(0,ControlButtons.Reload);Step(1);
            world.Damage.Apply(new DamageRequest(new ActorId(202),actor.Id,999,10000),actor);Step(10);
            Assert.That(rifle.ReloadRemaining,Is.Zero);
        }
        [Test] public void InvalidAimAndForeignCommandsAreRejected()
        {
            var p=Packet(CommandType.DirectControl);p.Direct.AimTarget=new Vector3(float.NaN,0,0);
            Assert.That(world.Session.Submit(actor.Id,p),Is.False);
            p=Packet(CommandType.Action);p.Action.ActionId=999;
            Assert.That(world.Session.Submit(actor.Id,p),Is.False);
        }
    }
}
