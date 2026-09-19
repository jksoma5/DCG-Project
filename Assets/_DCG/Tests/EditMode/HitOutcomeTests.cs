using DCG.Gameplay.Fighting;
using NUnit.Framework;
using UnityEngine;
namespace DCG.Tests
{
    public sealed class HitOutcomeTests
    {
        MoveData move;FightMoveSet settings;
        [SetUp] public void Setup(){move=ScriptableObject.CreateInstance<MoveData>();settings=ScriptableObject.CreateInstance<FightMoveSet>();move.level=HitLevel.Mid;}
        [TearDown] public void Cleanup(){Object.DestroyImmediate(move);Object.DestroyImmediate(settings);}
        ReactionChoice Select(FighterTags tags)=>HitOutcomeSelector.Select(move,tags,settings.counterStates);
        [Test] public void EvadeBeforeGuardAndGuardBeforeArmor()
        {
            move.level=HitLevel.High;Assert.That(Select(FighterTags.HighCrush|FighterTags.HighGuard|FighterTags.PowerCrush).Situation,Is.EqualTo(HitSituation.Evaded));
            Assert.That(Select(FighterTags.HighGuard|FighterTags.PowerCrush).Situation,Is.EqualTo(HitSituation.Block));
        }
        [Test] public void SidestepNeedsHomingAndAirborneHitsIgnoreStandingGuards()
        {
            Assert.That(Select(FighterTags.Sidestep).Situation,Is.EqualTo(HitSituation.Evaded));move.homing=true;
            Assert.That(Select(FighterTags.Sidestep).Situation,Is.EqualTo(HitSituation.Normal));
            move.hasAirborneOutcome=true;Assert.That(Select(FighterTags.Airborne|FighterTags.HighGuard|FighterTags.AttackActive).Situation,Is.EqualTo(HitSituation.Airborne));
        }
        [Test] public void CounterDefaultsToStartupAndActiveButNotRecoveryAndFallsBack()
        {
            Assert.That(Select(FighterTags.AttackStartup).Situation,Is.EqualTo(HitSituation.Counter));
            Assert.That(Select(FighterTags.AttackActive).Situation,Is.EqualTo(HitSituation.Counter));
            Assert.That(Select(FighterTags.AttackRecovery).Situation,Is.EqualTo(HitSituation.Normal));
            Assert.That(Select(FighterTags.AttackActive).Hit,Is.SameAs(move.hitOutcome));
            move.hasCounterOutcome=true;Assert.That(Select(FighterTags.AttackActive).Hit,Is.SameAs(move.counterOutcome));
        }
        [Test] public void MissingAirAndGroundResultsWhiffAndLowBeatsArmor()
        {
            Assert.That(Select(FighterTags.Down).Situation,Is.EqualTo(HitSituation.Miss));
            Assert.That(Select(FighterTags.Airborne).Situation,Is.EqualTo(HitSituation.Miss));
            move.hasGroundOutcome=true;Assert.That(Select(FighterTags.Down|FighterTags.HighGuard).Hit,Is.SameAs(move.groundOutcome));
            Assert.That(Select(FighterTags.PowerCrush).Situation,Is.EqualTo(HitSituation.Armor));
            move.level=HitLevel.Low;Assert.That(Select(FighterTags.PowerCrush).Situation,Is.EqualTo(HitSituation.Normal));
        }
        [Test] public void BackturnAndStunPreventGuardButSpecialMidAllowsBothGuards()
        {
            Assert.That(Select(FighterTags.Backturned|FighterTags.HighGuard).Situation,Is.EqualTo(HitSituation.Normal));
            Assert.That(Select(FighterTags.Blockstun|FighterTags.HighGuard).Situation,Is.EqualTo(HitSituation.Normal));
            move.level=HitLevel.SpecialMid;Assert.That(Select(FighterTags.LowGuard).Situation,Is.EqualTo(HitSituation.Block));
        }
        [Test] public void LaunchLocksUntilLandingThenDownAndGetupFinish()
        {
            var state=new FighterStateMachine();state.React(new HitOutcome{reaction=HitReaction.Launch},10,Vector3.right,-25,settings,false);
            for(int i=0;i<200;i++)state.Advance();Assert.That(state.IsAirborne,Is.True);Assert.That(state.Start(move),Is.False);
            state.Land();Assert.That(state.IsDown,Is.True);for(int i=0;i<settings.downFrames;i++)state.Advance();
            Assert.That(state.ReactionState,Is.EqualTo(FighterReactionState.GettingUp));
            for(int i=0;i<settings.getupFrames;i++)state.Advance();Assert.That(state.Busy,Is.False);
        }
        [Test] public void JuggleBudgetAndSecondScrewCannotRelaunchForever()
        {
            var state=new FighterStateMachine();var hit=new HitOutcome{reaction=HitReaction.Launch};state.React(hit,0,Vector3.right,-25,settings,false);
            for(int i=0;i<settings.maxJuggleCost;i++)state.React(hit,0,Vector3.right,-25,settings,true);
            Assert.That(state.ReactionVelocity.y,Is.LessThan(0));state.Reset();hit.reaction=HitReaction.Screw;
            state.React(hit,0,Vector3.right,-25,settings,true);state.React(hit,0,Vector3.right,-25,settings,true);
            Assert.That(state.ReactionVelocity.y,Is.LessThan(0));
        }
        [Test] public void CrouchingOverrideOnlyAppliesToNormalHit()
        {
            move.hasCrouchingOutcome=true;Assert.That(Select(FighterTags.Crouching).Hit,Is.SameAs(move.crouchingOutcome));
            Assert.That(Select(FighterTags.Crouching|FighterTags.AttackStartup).Hit,Is.SameAs(move.hitOutcome));
        }
    }
}
