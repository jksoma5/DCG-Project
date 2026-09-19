using System.Collections;
using DCG.Core;
using DCG.Gameplay;
using DCG.Gameplay.Fighting;
using DCG.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace DCG.Tests
{
    public sealed class PaulTests
    {
        PaulLabController lab;FighterMatch match;FighterAgent a,b;uint sequence;
        InputSettings.UpdateMode originalMode;float originalFixedDelta;
        [UnitySetUp] public IEnumerator Setup()
        {
            originalMode=InputSystem.settings.updateMode;originalFixedDelta=Time.fixedDeltaTime;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_DCG/Scenes/PaulLab.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            lab=Object.FindFirstObjectByType<PaulLabController>();lab.automatic=false;lab.input.enabled=false;
            match=lab.match;a=match.first;b=match.second;sequence=10000;
            a.HoldPosition=b.HoldPosition=true;lab.Tick(new FightInputFrame{Direction=5});lab.Tick(new FightInputFrame{Direction=5});
        }
        void Send(FighterAgent who,int direction,FightButtons buttons)
        {
            Assert.That(match.world.Session.Submit(who.Actor.Id,new PlayerCommand{
                Envelope=new CommandEnvelope{ActorId=who.Actor.Id,Sequence=++sequence,CommandType=CommandType.FightInput},
                Fight=new FightInputFrame{Direction=FightDirections.Relative(direction,who.Side),Pressed=buttons,Held=buttons}}),Is.True);
        }
        void Tick(int direction=5,FightButtons buttons=0,int guard=5,FightButtons other=0)
        {
            if(a.Actor.Health.IsAlive)Send(a,direction,buttons);if(b.Actor.Health.IsAlive)Send(b,guard,other);
            match.StepFrame();
        }
        void Run(int count,int guard=5){for(int i=0;i<count;i++)Tick(5,0,guard);}
        [Test] public void UppercutActuallyRisesAndBlocksInputUntilLandingAndGetup()
        {
            Tick(3,FightButtons.RP);Run(18);Assert.That(a.LastCommand,Is.EqualTo("df+2"));Assert.That(b.State.IsAirborne,Is.True);
            Run(6);Assert.That(b.transform.position.y,Is.GreaterThan(.3f));
            Tick(5,0,5,FightButtons.LP);Run(3);Assert.That(b.State.Move,Is.Null);
            for(int i=0;i<90&&!b.State.IsDown;i++)Tick();Assert.That(b.State.IsDown,Is.True);
            Run(60);Assert.That(b.State.Busy,Is.False);Assert.That(b.transform.position.y,Is.LessThan(.1f));
        }
        [Test] public void LethalUppercutStillFallsAndDoesNotGetUp()
        {
            b.Actor.Health.Apply(195);Tick(3,FightButtons.RP);Run(24);
            Assert.That(b.Actor.Health.IsAlive,Is.False);Assert.That(b.State.IsAirborne,Is.True);Assert.That(b.transform.position.y,Is.GreaterThan(.2f));
            Run(160);Assert.That(b.State.IsDown,Is.True);Assert.That(b.transform.position.y,Is.LessThan(.1f));
        }
        [Test] public void UppercutGuardAndCrouchingNormalHitDoNotLaunch()
        {
            Tick(3,FightButtons.RP,4);Run(30,4);Assert.That(a.LastResult,Is.EqualTo("Blocked"));Assert.That(b.State.IsAirborne,Is.False);
            Run(30);Tick(3,FightButtons.RP,1);Run(20,1);Assert.That(b.State.Reaction,Is.EqualTo(HitReaction.CrouchStagger));Assert.That(b.State.IsAirborne,Is.False);
        }
        [Test] public void RightKickOnlyLaunchesOnCounter()
        {
            Tick(5,FightButtons.RK);Run(20);Assert.That(b.State.IsAirborne,Is.False);Run(40);
            Tick(5,FightButtons.RK);Run(6);Tick(5,0,5,FightButtons.RK);Run(8);
            Assert.That(a.LastResult,Is.EqualTo("Counter"));Assert.That(b.State.Reaction,Is.EqualTo(HitReaction.CounterLaunch));Assert.That(b.State.IsAirborne,Is.True);
        }
        [Test] public void BothUppercutsLaunchInSameFrame()
        {
            Tick(3,FightButtons.RP,3,FightButtons.RP);Run(18);
            Assert.That(a.State.IsAirborne,Is.True);Assert.That(b.State.IsAirborne,Is.True);
            Assert.That(a.Actor.Health.Current,Is.EqualTo(187));Assert.That(b.Actor.Health.Current,Is.EqualTo(187));
        }
        [Test] public void AirborneFollowupUsesAirResultAndDamageScaling()
        {
            Tick(3,FightButtons.RP);Run(44);a.HoldPosition=false;
            for(int i=0;i<3;i++)Tick(6);float hp=b.Actor.Health.Current;
            Tick(5,FightButtons.LP);Run(12);
            Assert.That(a.LastResult,Is.EqualTo("Air hit"));Assert.That(b.State.JuggleCost,Is.EqualTo(1));
            Assert.That(hp-b.Actor.Health.Current,Is.EqualTo(8*.7f).Within(.001));
        }
        [Test] public void PowerCrushTakesDamageWithoutCancelingAttack()
        {
            var armor=Object.Instantiate(b.moveSet.moves[1]);
            try
            {
                armor.startup=50;armor.powerCrush=new PropertyWindow{enabled=true,firstFrame=0,lastFrame=50};
                b.State.Start(armor);Tick(5,FightButtons.LP);Run(12);
                Assert.That(b.Actor.Health.Current,Is.EqualTo(192));Assert.That(b.State.Move,Is.SameAs(armor));Assert.That(a.LastResult,Is.EqualTo("PowerCrush"));
            }
            finally{Object.DestroyImmediate(armor);}
        }
        [Test] public void GroundHitRequiresExplicitOutcome()
        {
            b.State.React(new HitOutcome{reaction=HitReaction.Knockdown},0,Vector3.right,-25,b.moveSet,false);
            Tick(5,FightButtons.LP);Run(12);Assert.That(b.Actor.Health.Current,Is.EqualTo(200));a.State.Reset();
            var hit=Object.Instantiate(a.moveSet.moves[0]);
            try
            {
                hit.startup=0;hit.active=3;hit.follows="";hit.hasGroundOutcome=true;hit.groundOutcome=new HitOutcome{reaction=HitReaction.Knockdown};
                a.State.Start(hit);Tick();Assert.That(a.LastResult,Is.EqualTo("Ground hit"));Assert.That(b.Actor.Health.Current,Is.EqualTo(192));
            }
            finally{Object.DestroyImmediate(hit);}
        }
        [Test] public void PhoenixBlowsAwayDespiteDummyHoldPosition()
        {
            float start=b.transform.position.x;Tick(2);Tick(3);Tick(6,FightButtons.RP);Run(20);
            Assert.That(b.State.Reaction,Is.EqualTo(HitReaction.BlowAway));Assert.That(b.transform.position.x,Is.GreaterThan(start+.1f));
            Run(22);Assert.That(b.State.IsDown,Is.True);
        }
        [Test] public void RecordedPhoenixReplaysOnOppositeSide()
        {
            lab.SetRecording(true);
            lab.Tick(new FightInputFrame{Direction=2});lab.Tick(new FightInputFrame{Direction=3});
            lab.Tick(new FightInputFrame{Direction=6,Pressed=FightButtons.RP,Held=FightButtons.RP});
            for(int i=0;i<120;i++)lab.Tick(new FightInputFrame{Direction=5});
            lab.SetRecording(false);Assert.That(lab.RecordedFrames,Is.EqualTo(123));
            lab.StartReplay();for(int i=0;i<5;i++)lab.Tick(new FightInputFrame{Direction=5});
            Assert.That(b.Side,Is.EqualTo(FightSide.Reversed));
            Assert.That(b.State.Move.moveId,Is.EqualTo("Phoenix"));
        }
        [Test] public void DoubleTapBackMovesFasterThanWalking()
        {
            a.HoldPosition=false;float start=a.transform.position.x;
            Tick(4);float walk=start-a.transform.position.x;Tick();start=a.transform.position.x;Tick(4);
            Assert.That(start-a.transform.position.x,Is.GreaterThan(walk*1.5f));
        }
        [Test] public void TapUpSidestepsAndHoldUpJumps()
        {
            a.HoldPosition=false;float start=a.transform.position.z;Tick(8);Tick();
            Assert.That(Mathf.Abs(a.transform.position.z-start),Is.GreaterThan(.01f));
            Run(15);for(int i=0;i<8;i++)Tick(8);
            Assert.That(a.Actor.Motor.State,Is.EqualTo(MovementState.Airborne));
            Assert.That(a.transform.position.y,Is.GreaterThan(.02f));
        }
        [Test] public void PhoenixWinsPriorityAndDealsDamageOnce()
        {
            float hp=b.Actor.Health.Current;Tick(2);Tick(3);Tick(6,FightButtons.RP);Run(2);
            Assert.That(a.State.Move.moveId,Is.EqualTo("Phoenix"));Run(60);
            Assert.That(b.Actor.Health.Current,Is.EqualTo(hp-30));Assert.That(a.LastCommand,Is.EqualTo("d,df,f+2"));
        }
        [Test] public void StandGuardBlocksAndFrameAdvantageMatchesData()
        {
            float hp=b.Actor.Health.Current;Tick(5,FightButtons.LP,4);
            for(int i=0;i<20&&a.LastResult!="Blocked";i++)Tick(5,0,4);
            Assert.That(a.LastResult,Is.EqualTo("Blocked"));Assert.That(b.Actor.Health.Current,Is.EqualTo(hp));
            Assert.That(b.State.Stun-a.State.Remaining,Is.EqualTo(a.State.Move.onBlock));
        }
        [Test] public void CrouchEvadesHighButMidBeatsCrouchGuard()
        {
            float hp=b.Actor.Health.Current;Tick(5,FightButtons.LP,1);Run(40,1);
            Assert.That(b.Actor.Health.Current,Is.EqualTo(hp));Assert.That(a.LastResult,Is.EqualTo("Evaded"));
            Tick(3,FightButtons.LP,1);Run(40,1);Assert.That(b.Actor.Health.Current,Is.EqualTo(hp-12));
        }
        [Test] public void LowRequiresCrouchGuard()
        {
            float hp=b.Actor.Health.Current;Tick(2,FightButtons.RK,4);Run(50,4);
            Assert.That(b.Actor.Health.Current,Is.EqualTo(hp-12));
            hp=b.Actor.Health.Current;Tick(2,FightButtons.RK,1);Run(50,1);Assert.That(b.Actor.Health.Current,Is.EqualTo(hp));
            Assert.That(a.LastResult,Is.EqualTo("Blocked"));
        }
        [Test] public void SimultaneousLethalHitsTradeWithoutActorOrderBias()
        {
            a.Actor.Health.Apply(a.Actor.Health.Current-8);b.Actor.Health.Apply(b.Actor.Health.Current-8);
            Tick(5,FightButtons.LP,5,FightButtons.LP);Run(12);
            Assert.That(a.Actor.Health.Current,Is.Zero);Assert.That(b.Actor.Health.Current,Is.Zero);
        }
        [Test] public void SideWaitsDuringAttackButFacingTracksAndThenFlips()
        {
            Tick(5,FightButtons.LP);Run(2);
            var ca=a.GetComponent<CharacterController>();var cb=b.GetComponent<CharacterController>();
            ca.enabled=cb.enabled=false;a.transform.position=new Vector3(1,0,0);b.transform.position=new Vector3(-1,0,0);
            ca.enabled=cb.enabled=true;Physics.SyncTransforms();Tick();
            Assert.That(a.Side,Is.EqualTo(FightSide.Normal));Assert.That(Vector3.Dot(a.transform.forward,Vector3.left),Is.GreaterThan(.99f));
            Run(35);Assert.That(a.Side,Is.EqualTo(FightSide.Reversed));Assert.That(b.Side,Is.EqualTo(FightSide.Normal));
        }
        [Test] public void MacrosDoNotBecomeSinglePunchAndInputSettingsRestore()
        {
            Tick(5,FightButtons.LP|FightButtons.RP);Run(2);Assert.That(a.State.Move.moveId,Is.EqualTo("BothHands"));
            Assert.That(InputSystem.settings.updateMode,Is.EqualTo(originalMode));Assert.That(Time.fixedDeltaTime,Is.EqualTo(originalFixedDelta).Within(.00001f));
        }
        [Test] public void FollowupOnlyStartsInsideItsInputWindow()
        {
            Tick(5,FightButtons.LP);Run(9);Tick(5,FightButtons.RP);Run(2);
            Assert.That(a.State.Move.moveId,Is.EqualTo("LP_RP"));
        }
    }
}
