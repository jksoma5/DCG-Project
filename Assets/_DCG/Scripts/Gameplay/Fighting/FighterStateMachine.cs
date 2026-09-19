using System;
using UnityEngine;
namespace DCG.Gameplay.Fighting
{
    public enum FighterReactionState { None, Hitstun, Blockstun, Airborne, Down, GettingUp }
    public sealed class FighterStateMachine
    {
        public MoveData Move { get; private set; }
        public int Age { get; private set; }
        public int Stun { get; private set; }
        public bool GuardStun { get; private set; }
        public bool Contacted { get; set; }
        public ulong AttackId { get; private set; }
        public FighterReactionState ReactionState { get; private set; }
        public HitReaction Reaction { get; private set; }
        public KnockdownPose DownPose { get; private set; }
        public bool HeadTowardAttacker { get; private set; }
        public bool Backturned { get; private set; }
        public bool ForceCrouch { get; private set; }
        public int JuggleCost { get; private set; }
        public bool ScrewUsed { get; private set; }
        public Vector3 ReactionVelocity { get; private set; }
        int downFrames=36,getupFrames=18,pushFrames;
        Vector3 pushVelocity;
        public bool IsAirborne=>ReactionState==FighterReactionState.Airborne;
        public bool IsDown=>ReactionState==FighterReactionState.Down||ReactionState==FighterReactionState.GettingUp;
        public bool Busy=>Move!=null||Stun>0||ReactionState!=FighterReactionState.None;
        public bool Active=>Move!=null&&Age>=Move.startup&&Age<Move.startup+Move.active;
        public int Remaining=>Move==null?0:Move.Total-Age;
        public string Phase=>ReactionState!=FighterReactionState.None?ReactionState+" / "+Reaction:Move==null?"Ready":Age<Move.startup?"Startup":Active?"Active":"Recovery";
        public void Advance()
        {
            if(Stun>0&&--Stun==0&&!IsAirborne)
            {
                if(ReactionState==FighterReactionState.Down){ReactionState=FighterReactionState.GettingUp;Stun=getupFrames;}
                else {ReactionState=FighterReactionState.None;ForceCrouch=Backturned=false;JuggleCost=0;ScrewUsed=false;}
            }
            if(Move!=null&&++Age>=Move.Total)Move=null;
        }
        public bool Start(MoveData next)
        {
            if(Stun>0||ReactionState!=FighterReactionState.None||next==null)return false;
            if(Move==null&&!string.IsNullOrEmpty(next.follows))return false;
            if(Move!=null&&(next.follows!=Move.moveId||Age<Move.chainStart||Age>Move.chainEnd))return false;
            Move=next;Age=0;Contacted=false;AttackId++;return true;
        }
        public void Impact(int frames,bool guarded)
        {
            Move=null;Stun=Math.Max(1,frames);GuardStun=guarded;
            ReactionState=guarded?FighterReactionState.Blockstun:FighterReactionState.Hitstun;
            Reaction=HitReaction.Stagger;ForceCrouch=Backturned=false;
        }
        public void Block(BlockOutcome outcome,int remaining,Vector3 away)
        {
            Impact(remaining+outcome.advantageFrames,!outcome.guardBreak);
            ForceCrouch=outcome.forceCrouch;BeginPush(away,outcome.pushback,Stun);
        }
        public void React(HitOutcome outcome,int remaining,Vector3 away,float gravity,FightMoveSet settings,bool airborneHit)
        {
            bool wasAir=IsAirborne;
            Impact(outcome.stunFrames>0?outcome.stunFrames:remaining+outcome.advantageFrames,false);
            Reaction=outcome.reaction;ForceCrouch=Reaction==HitReaction.CrouchStagger||Reaction==HitReaction.Crumple;
            Backturned=outcome.turnDefender;DownPose=outcome.knockdown;HeadTowardAttacker=outcome.headTowardAttacker;
            downFrames=Math.Max(1,settings.downFrames);getupFrames=Math.Max(1,settings.getupFrames);
            bool launch=Reaction==HitReaction.Launch||Reaction==HitReaction.CounterLaunch||Reaction==HitReaction.BlowAway||Reaction==HitReaction.Screw;
            if(launch||airborneHit||wasAir)
            {
                if(!airborneHit){JuggleCost=0;ScrewUsed=false;}
                else JuggleCost+=Math.Max(1,outcome.juggleCost);
                bool exhausted=JuggleCost>=settings.maxJuggleCost||(Reaction==HitReaction.Screw&&ScrewUsed);
                if(Reaction==HitReaction.Screw)ScrewUsed=true;
                ReactionState=FighterReactionState.Airborne;Stun=0;
                float up=exhausted?Mathf.Min(-1,ReactionVelocity.y):Mathf.Sqrt(2*Mathf.Abs(gravity)*Mathf.Max(0,outcome.launchHeight))/(1+.12f*JuggleCost);
                ReactionVelocity=away*outcome.horizontalSpeed+Vector3.up*up;
                pushFrames=0;
            }
            else if(Reaction==HitReaction.Knockdown){ReactionState=FighterReactionState.Down;Stun=downFrames;BeginPush(away,outcome.pushback,Stun);}
            else BeginPush(away,outcome.pushback,Stun);
        }
        void BeginPush(Vector3 away,float distance,int frames){pushFrames=Math.Max(1,frames);pushVelocity=away*Mathf.Max(0,distance)*60/pushFrames;}
        public Vector3 StepReaction(float gravity,float dt)
        {
            if(IsAirborne){ReactionVelocity+=Vector3.up*gravity*dt;return ReactionVelocity;}
            if(pushFrames>0){pushFrames--;return pushVelocity;}
            return Vector3.zero;
        }
        public void HitCeiling(){ReactionVelocity=new Vector3(ReactionVelocity.x,Mathf.Min(0,ReactionVelocity.y),ReactionVelocity.z);}
        public void Land(){if(!IsAirborne)return;ReactionVelocity=Vector3.zero;ReactionState=FighterReactionState.Down;Stun=downFrames;}
        public void Defeat(){Move=null;if(!IsAirborne){ReactionState=FighterReactionState.Down;Stun=0;}}
        public void Reset(){Move=null;Stun=0;Age=0;Contacted=false;ReactionState=FighterReactionState.None;GuardStun=ForceCrouch=Backturned=false;JuggleCost=0;ScrewUsed=false;ReactionVelocity=Vector3.zero;pushFrames=0;}
    }
}
