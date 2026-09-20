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
    // A move landing on a class that is not a fighter. The other three classes have no guard, no side
    // and no fighter state machine, so everything about defending is absent and the hit simply lands.
    // The contact rules are the fighters' own: attack level against the target's height, the move's
    // range measured flat in the fight plane, and line of sight.
    public readonly struct FightActorOutcome
    {
        public readonly FighterAgent Attacker;
        public readonly ActorSimulation Defender;
        public readonly MoveData Move;
        public readonly ulong Attack;
        public readonly float Damage;
        public FightActorOutcome(FighterAgent a,ActorSimulation d)
        {
            Attacker=a;Defender=d;Move=a.State.Move;Attack=a.State.AttackId;Damage=a.State.Move.damage;
        }
        public bool Valid=>Attacker!=null&&Defender!=null&&Move!=null;
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
        public static FightActorOutcome EvaluateActor(FighterAgent a,ActorSimulation d)
        {
            if(a==null||d==null||!d.Initialized)return default;
            if(!a.Actor.Health.IsAlive||!d.Health.IsAlive||!a.State.Active||a.State.Contacted)return default;
            float height=a.transform.position.y+(a.State.Move.level==HitLevel.High?1.4f:a.State.Move.level==HitLevel.Low?.3f:1f);
            // A target off the ground is only reachable where its body actually is. A three dimensional
            // class jumps and falls on its own, so this is checked for everyone, not only for a juggle.
            if(height<d.transform.position.y||height>d.transform.position.y+1.8f)return default;
            if(FightPlane.FlatDistance(d.transform.position,a.transform.position)>a.State.Move.range)return default;
            if(Physics.Linecast(a.Actor.AimPoint,d.AimPoint,a.Actor.World.WorldMask,QueryTriggerInteraction.Ignore))return default;
            return new FightActorOutcome(a,d);
        }
        public static void ApplyActor(FightActorOutcome o)
        {
            if(!o.Valid)return;
            o.Attacker.State.Contacted=true;
            o.Attacker.Actor.World.Damage.Apply(
                new DamageRequest(o.Attacker.Actor.Id,o.Defender.Id,o.Attack,o.Damage),o.Defender);
            o.Attacker.LastResult="Hit";
            o.Attacker.LastAdvantage=o.Move.hitOutcome.advantageFrames;
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
