using System;
using UnityEngine;
namespace DCG.Gameplay.Fighting
{
    public enum HitReaction { Stagger, CrouchStagger, Launch, CounterLaunch, Knockdown, BlowAway, Crumple, Spin, Screw }
    public enum KnockdownPose { FaceUp, FaceDown }
    [Flags] public enum FighterTags
    {
        None=0, Standing=1, Crouching=2, HighGuard=4, LowGuard=8,
        AttackStartup=16, AttackActive=32, AttackRecovery=64, Hitstun=128, Blockstun=256,
        Airborne=512, Down=1024, Backturned=2048, Sidestep=4096, HighCrush=8192, LowCrush=16384, PowerCrush=32768
    }
    [Serializable] public sealed class HitOutcome
    {
        public HitReaction reaction;
        public int advantageFrames=3, stunFrames;
        [Min(0)] public float launchHeight=1.8f, horizontalSpeed=.7f, pushback;
        public KnockdownPose knockdown;
        public bool headTowardAttacker, turnDefender;
        [Min(0)] public int juggleCost=1;
    }
    [Serializable] public sealed class BlockOutcome
    {
        public int advantageFrames=-5;
        public float pushback;
        public bool guardBreak, forceCrouch;
    }
    [Serializable] public struct PropertyWindow
    {
        public bool enabled;
        public int firstFrame,lastFrame;
        public bool Contains(int frame)=>enabled&&frame>=firstFrame&&frame<=lastFrame;
    }
    public enum HitSituation { Miss, Evaded, Block, Armor, Airborne, Ground, Counter, Normal }
    public readonly struct ReactionChoice
    {
        public readonly HitSituation Situation;
        public readonly HitOutcome Hit;
        public ReactionChoice(HitSituation situation,HitOutcome hit=null){Situation=situation;Hit=hit;}
    }
    public static class HitOutcomeSelector
    {
        public static ReactionChoice Select(MoveData move,FighterTags tags,FighterTags counterStates)
        {
            // Throws require a separate break window; no throw moves are enabled in PaulLab.
            if(move.level==HitLevel.Throw)return new ReactionChoice(HitSituation.Miss);
            bool Has(FighterTags value)=>(tags&value)!=0;
            bool air=Has(FighterTags.Airborne),down=Has(FighterTags.Down);
            if(!down&&((move.level==HitLevel.High&&(Has(FighterTags.HighCrush)||(!air&&Has(FighterTags.Crouching))))||
                (move.level==HitLevel.Low&&Has(FighterTags.LowCrush))||(!air&&!move.homing&&Has(FighterTags.Sidestep))))
                return new ReactionChoice(HitSituation.Evaded);
            bool guardAllowed=!air&&!down&&!Has(FighterTags.Backturned|FighterTags.AttackStartup|FighterTags.AttackActive|FighterTags.AttackRecovery|FighterTags.Hitstun|FighterTags.Blockstun);
            bool guarded=move.level==HitLevel.Low?Has(FighterTags.LowGuard):
                move.level==HitLevel.SpecialMid?Has(FighterTags.HighGuard|FighterTags.LowGuard):Has(FighterTags.HighGuard);
            if(guardAllowed&&guarded)return new ReactionChoice(HitSituation.Block);
            if(!air&&!down&&Has(FighterTags.PowerCrush)&&(move.level==HitLevel.High||move.level==HitLevel.Mid||move.level==HitLevel.SpecialMid))
                return new ReactionChoice(HitSituation.Armor);
            if(air)return new ReactionChoice(move.hasAirborneOutcome?HitSituation.Airborne:HitSituation.Miss,move.airborneOutcome);
            if(down)return new ReactionChoice(move.hasGroundOutcome?HitSituation.Ground:HitSituation.Miss,move.groundOutcome);
            if(Has(counterStates))return new ReactionChoice(HitSituation.Counter,move.hasCounterOutcome?move.counterOutcome:move.hitOutcome);
            return new ReactionChoice(HitSituation.Normal,move.hasCrouchingOutcome&&Has(FighterTags.Crouching)?move.crouchingOutcome:move.hitOutcome);
        }
    }
}
