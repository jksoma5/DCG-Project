using DCG.Core;
using UnityEngine;
namespace DCG.Gameplay.Fighting
{
    public readonly struct FightOutcome
    {
        public readonly FighterAgent Attacker,Defender;
        public readonly MoveData Move;
        public readonly ulong Attack;
        public readonly ReactionChoice Choice;
        public readonly int Remaining;
        public readonly Vector3 Away;
        public readonly float Damage;
        public FightOutcome(FighterAgent a,FighterAgent d,ReactionChoice choice)
        {
            Attacker=a;Defender=d;Move=a.State.Move;Attack=a.State.AttackId;Choice=choice;Remaining=a.State.Remaining;
            var away=d.transform.position-a.transform.position;away.y=0;Away=away.sqrMagnitude>.0001f?away.normalized:a.transform.forward;
            Damage=Move.damage+(choice.Situation==HitSituation.Counter?Move.counterDamageBonus:0);
            if(choice.Situation==HitSituation.Airborne)Damage*=Mathf.Max(a.moveSet.minimumAirDamageScale,a.moveSet.airDamageScale-d.State.JuggleCost*a.moveSet.airDamageDecay);
        }
        public bool Valid=>Move!=null&&Choice.Situation!=HitSituation.Miss;
    }
    public static class FightResolver
    {
        public static FightOutcome Evaluate(FighterAgent a,FighterAgent d)
        {
            if(!a.Actor.Health.IsAlive||!d.Actor.Health.IsAlive||!a.State.Active||a.State.Contacted)return default;
            Vector3 delta=d.transform.position-a.transform.position;
            float height=a.transform.position.y+(a.State.Move.level==HitLevel.High?1.4f:a.State.Move.level==HitLevel.Low?.3f:1f);
            if(d.State.IsAirborne&&(height<d.transform.position.y||height>d.transform.position.y+1.8f))return default;
            delta.y=0;
            if(delta.magnitude>a.State.Move.range||Physics.Linecast(a.Actor.AimPoint,d.Actor.AimPoint,a.Actor.World.WorldMask,QueryTriggerInteraction.Ignore))return default;
            var choice=HitOutcomeSelector.Select(a.State.Move,d.Tags,a.moveSet.counterStates);
            return new FightOutcome(a,d,choice);
        }
        public static void Apply(FightOutcome o)
        {
            if(!o.Valid)return;
            o.Attacker.State.Contacted=true;
            var situation=o.Choice.Situation;
            if(situation==HitSituation.Evaded){o.Attacker.LastResult="Evaded";o.Attacker.LastAdvantage=0;return;}
            if(situation==HitSituation.Block)
            {
                o.Attacker.LastResult=o.Move.blockOutcome.guardBreak?"GuardBreak":"Blocked";
                o.Attacker.LastAdvantage=o.Move.blockOutcome.advantageFrames;
                o.Defender.State.Block(o.Move.blockOutcome,o.Remaining,o.Away);return;
            }
            o.Attacker.Actor.World.Damage.Apply(new DamageRequest(o.Attacker.Actor.Id,o.Defender.Actor.Id,o.Attack,o.Damage),o.Defender.Actor);
            if(situation==HitSituation.Armor){o.Attacker.LastResult="PowerCrush";o.Attacker.LastAdvantage=0;if(!o.Defender.Actor.Health.IsAlive)o.Defender.State.Defeat();return;}
            o.Attacker.LastResult=situation==HitSituation.Counter?"Counter":situation==HitSituation.Airborne?"Air hit":situation==HitSituation.Ground?"Ground hit":"Hit";
            o.Attacker.LastAdvantage=o.Choice.Hit.advantageFrames;
            o.Defender.State.React(o.Choice.Hit,o.Remaining,o.Away,o.Defender.Actor.tuning.gravity,o.Defender.moveSet,situation==HitSituation.Airborne);
            if(!o.Defender.Actor.Health.IsAlive)o.Defender.State.Defeat();
        }
    }
}
