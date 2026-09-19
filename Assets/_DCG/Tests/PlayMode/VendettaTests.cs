using System.Collections;
using DCG.Core;
using DCG.Gameplay;
using DCG.Classes.Vendetta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace DCG.Tests
{
    public sealed class VendettaTests
    {
        SimulationWorld world;ActorSimulation actor;VendettaController controller;uint sequence;
        [UnitySetUp] public IEnumerator Setup()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_DCG/Scenes/VendettaLab.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("VendettaLab");
#endif
            yield return null;
            world=Object.FindFirstObjectByType<SimulationWorld>();world.AutomaticTicks=false;
            actor=world.Find(new ActorId(101));controller=actor.GetComponent<VendettaController>();
            Object.FindFirstObjectByType<VendettaInputReader>().enabled=false;sequence=100;
            Step(2);
        }
        PlayerCommand Packet(CommandType type)=>new PlayerCommand{Envelope=new CommandEnvelope{ActorId=actor.Id,Sequence=++sequence,CommandType=type}};
        void Aim(float yaw=0,float pitch=0,Vector2 movement=default)
        {
            var p=Packet(CommandType.DirectControl);p.Direct=new DirectControlFrame{AimYawPitch=new Vector2(yaw,pitch),MoveAxes=movement};
            Assert.That(world.Session.Submit(actor.Id,p),Is.True);Step(1);
        }
        bool Skill(int id,Vector3 direction)
        {
            var p=Packet(CommandType.Action);p.Action=new ActionCommand{ActionId=id,TargetPoint=actor.AimPoint+direction.normalized*10};
            return world.Session.Submit(actor.Id,p);
        }
        void Step(int count){for(int i=0;i<count;i++){world.Step(.02f);Physics.SyncTransforms();}}
        void Place(Vector3 position)
        {
            var cc=actor.GetComponent<CharacterController>();cc.enabled=false;actor.transform.position=position;cc.enabled=true;Physics.SyncTransforms();Step(2);
        }
        [Test] public void ShiftDashesThenSpinsAndHitsEachTargetOnce()
        {
            Aim();float z=actor.transform.position.z;var target=world.Find(new ActorId(102));
            Skill(1,Vector3.forward);Step(5);
            Assert.That(controller.Phase,Is.EqualTo(VendettaPhase.Dash));
            Assert.That(target.Health.Current,Is.EqualTo(target.Health.Maximum));
            Step(40);
            Assert.That(actor.transform.position.z-z,Is.InRange(5.8f,6.5f));
            Assert.That(target.Health.Current,Is.EqualTo(target.Health.Maximum-controller.tuning.spinDamage));
            Assert.That(controller.Phase,Is.EqualTo(VendettaPhase.Ready));
            Skill(1,Vector3.forward);Step(1);Assert.That(controller.Phase,Is.EqualTo(VendettaPhase.Ready));
        }
        [Test] public void EThrowsFirstFliesToStationarySwordThenStopsWithoutDamage()
        {
            Aim();var start=actor.transform.position;var target=world.Find(new ActorId(102));
            Skill(2,new Vector3(0,.3f,1));Step(5);
            Assert.That(controller.Phase,Is.EqualTo(VendettaPhase.SwordThrow));
            Assert.That(Vector3.Distance(actor.transform.position,start),Is.LessThan(.1f));
            Step(12);Assert.That(controller.Phase,Is.EqualTo(VendettaPhase.Flight));
            var sword=controller.SwordDestination;Step(50);
            Assert.That(controller.Phase,Is.EqualTo(VendettaPhase.Ready));
            Assert.That(Vector2.Distance(new Vector2(actor.transform.position.x,actor.transform.position.z),
                new Vector2(sword.x,sword.z)),Is.LessThan(.2f));
            var stopped=actor.transform.position;Step(20);
            Assert.That(Mathf.Abs(actor.transform.position.z-stopped.z),Is.LessThan(.05f));
            Assert.That(target.Health.Current,Is.EqualTo(target.Health.Maximum));
        }
        [Test] public void SkillsDoNotMoveThroughWall()
        {
            Place(new Vector3(5,0,-5));Aim();Skill(1,Vector3.forward);Step(40);
            Assert.That(actor.transform.position.z,Is.LessThan(-2.2f));
            Skill(2,Vector3.forward);Step(80);
            Assert.That(actor.transform.position.z,Is.LessThan(-2.2f));
            Assert.That(controller.Phase,Is.EqualTo(VendettaPhase.Ready));
        }
        [Test] public void InvalidDirectInputAndUnknownSkillAreRejected()
        {
            var p=Packet(CommandType.DirectControl);p.Direct.AimYawPitch=new Vector2(float.NaN,0);
            Assert.That(world.Session.Submit(actor.Id,p),Is.False);
            Assert.That(Skill(999,Vector3.forward),Is.False);
        }
        [Test] public void DirectMovementTimesOutAndDeathCancelsSkill()
        {
            Aim(0,0,Vector2.right);Step(30);var stop=actor.transform.position;Step(20);
            Assert.That(Mathf.Abs(actor.transform.position.x-stop.x),Is.LessThan(.01f));
            Skill(2,Vector3.up);Step(20);
            world.Damage.Apply(new DamageRequest(new ActorId(102),actor.Id,1,10000),actor);
            var dead=actor.transform.position;Step(40);
            Assert.That(actor.transform.position,Is.EqualTo(dead));
            Assert.That(controller.Phase,Is.EqualTo(VendettaPhase.Ready));
        }
    }
}
