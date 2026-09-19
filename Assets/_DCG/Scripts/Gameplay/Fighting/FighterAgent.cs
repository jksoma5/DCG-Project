using DCG.Core;
using UnityEngine;
namespace DCG.Gameplay.Fighting
{
    public sealed class FighterAgent : MonoBehaviour,IActorActionPolicy
    {
        public FightMoveSet moveSet;
        public bool HoldPosition;
        public ActorSimulation Actor { get; private set; }
        public FighterAgent Opponent { get; set; }
        public FightSide Side { get; set; }
        public InputBuffer Buffer { get; }=new InputBuffer();
        public FighterStateMachine State { get; }=new FighterStateMachine();
        readonly CommandParser parser=new CommandParser();
        FightInputFrame input=new FightInputFrame{Direction=5};
        int lastReceived,downFrames,upFrames,priorDirection=5,lastForward=-100,lastBack=-100,movementFrames;
        Vector3 specialVelocity;
        bool sidestepping;
        public int Frame { get; private set; }
        public int RelativeDirection { get; private set; }=5;
        public bool Crouched { get; private set; }
        public string LastCommand { get; private set; }="-";
        public string LastResult { get; set; }="-";
        public int LastAdvantage { get; set; }
        public FighterTags Tags
        {
            get
            {
                FighterTags tags=Crouched?FighterTags.Crouching:FighterTags.Standing;
                if(State.IsAirborne||Actor.Motor.State==MovementState.Airborne)tags|=FighterTags.Airborne;
                if(State.IsDown)tags=(tags&~FighterTags.Airborne)|FighterTags.Down;
                if(State.Backturned)tags|=FighterTags.Backturned;
                if(sidestepping&&movementFrames>0&&!State.Busy)tags|=FighterTags.Sidestep;
                if(State.Stun>0)tags|=State.GuardStun?FighterTags.Blockstun:FighterTags.Hitstun;
                if(RelativeDirection==4)tags|=FighterTags.HighGuard;
                if(RelativeDirection==1)tags|=FighterTags.LowGuard;
                var move=State.Move;
                if(move!=null)
                {
                    tags|=State.Active?FighterTags.AttackActive:State.Age<move.startup?FighterTags.AttackStartup:FighterTags.AttackRecovery;
                    if(move.highCrush.Contains(State.Age))tags|=FighterTags.HighCrush;
                    if(move.lowCrush.Contains(State.Age))tags|=FighterTags.LowCrush;
                    if(move.powerCrush.Contains(State.Age))tags|=FighterTags.PowerCrush;
                }
                else if(Actor.Motor.State==MovementState.Airborne&&!State.IsAirborne)tags|=FighterTags.LowCrush;
                return tags;
            }
        }
        public bool CanChangeSide=>!State.Busy&&Actor.Motor.State==MovementState.Grounded;
        public ActionState ActionState=>State.Move!=null?(State.Active?ActionState.Active:State.Age<State.Move.startup?ActionState.Windup:ActionState.Recovery):State.Stun>0?ActionState.Recovery:ActionState.Ready;
        public bool UseFullVelocity=>false;
        public void Initialize(){Actor=GetComponent<ActorSimulation>();}
        public bool Accepts(PlayerCommand command)
        {
            if(command.Envelope.CommandType==CommandType.Stop)return true;
            if(command.Envelope.CommandType!=CommandType.FightInput)return false;
            var f=command.Fight;
            return f.Direction>=1&&f.Direction<=9&&((int)(f.Held|f.Pressed|f.Released)&~15)==0;
        }
        public void Receive(PlayerCommand command)
        {
            if(!Accepts(command))return;
            if(command.Envelope.CommandType==CommandType.Stop){Stop();return;}
            input=command.Fight;lastReceived=Frame;
        }
        public void Prepare(int frame)
        {
            Frame=frame;
            if(!Actor.Health.IsAlive)
            {
                if(State.IsAirborne)
                {
                    var falling=State.StepReaction(Actor.tuning.gravity,1f/60);Actor.Motor.StepFullVelocity(falling,1f/60);
                    if(falling.y<=0&&Actor.Motor.State==MovementState.Grounded)State.Land();
                    else if(falling.y>0&&Actor.Motor.Velocity.y<.001f)State.HitCeiling();
                }
                else Actor.Motor.Step(Vector3.zero,Actor.tuning.gravity,1f/60);
                return;
            }
            if(frame-lastReceived>15)input=new FightInputFrame{Direction=5};
            RelativeDirection=FightDirections.Relative(input.Direction,Side);
            Buffer.Push(frame,input,Side,moveSet.simultaneousWindow);
            if(Buffer.Commit(frame,moveSet.simultaneousWindow,out var chord))
            {
                var move=parser.Match(Buffer,chord,moveSet.moves,moveSet.commandWindow);
                LastCommand=move==null?"Unassigned "+chord.Buttons:move.command;
                if(move!=null)State.Start(move);
            }
            Vector3 facing=Opponent.transform.position-transform.position;facing.y=0;
            if(facing.sqrMagnitude>.0001f)transform.rotation=Quaternion.LookRotation(State.Backturned?-facing:facing);
            var direction=facing.sqrMagnitude>.0001f?facing.normalized:transform.forward;
            Vector3 velocity=Vector3.zero;
            if(!State.Busy)
            {
                int relative=RelativeDirection;
                if(relative!=priorDirection)
                {
                    if(relative==6){if(frame-lastForward<=moveSet.doubleTapWindow){sidestepping=false;movementFrames=moveSet.dashFrames;specialVelocity=direction*moveSet.dashSpeed;}lastForward=frame;}
                    if(relative==4){if(frame-lastBack<=moveSet.doubleTapWindow){sidestepping=false;movementFrames=moveSet.dashFrames;specialVelocity=-direction*moveSet.dashSpeed;}lastBack=frame;}
                    if(moveSet.allowSidestep&&relative==5&&((priorDirection==8&&upFrames>0&&upFrames<moveSet.tapFrames)||(priorDirection==2&&downFrames>0&&downFrames<moveSet.tapFrames)))
                    {
                        sidestepping=true;movementFrames=moveSet.dashFrames;
                        specialVelocity=Vector3.Cross(Vector3.up,direction)*moveSet.sideSpeed*(upFrames>0?-1:1);
                    }
                }
                downFrames=FightDirections.Down(relative)?downFrames+1:0;
                upFrames=FightDirections.Up(relative)?upFrames+1:0;
                Crouched=downFrames>=moveSet.tapFrames||relative==1;
                if(upFrames==moveSet.tapFrames)Actor.Motor.Jump(moveSet.jumpSpeed);
                if(movementFrames>0){movementFrames--;velocity=specialVelocity;}
                else if(!Crouched)
                {
                    int x=(relative-1)%3-1;
                    velocity=direction*x*(x>0?moveSet.forwardSpeed:moveSet.backSpeed);
                }
            }
            else {movementFrames=0;sidestepping=false;downFrames=upFrames=0;Crouched=State.ForceCrouch;}
            priorDirection=RelativeDirection;
            if(State.IsAirborne)
            {
                var motion=State.StepReaction(Actor.tuning.gravity,1f/60);
                Actor.Motor.StepFullVelocity(motion,1f/60);
                if(motion.y<=0&&Actor.Motor.State==MovementState.Grounded)State.Land();
                else if(motion.y>0&&Actor.Motor.Velocity.y<.001f)State.HitCeiling();
            }
            else Actor.Motor.Step((HoldPosition?Vector3.zero:velocity)+State.StepReaction(Actor.tuning.gravity,1f/60),Actor.tuning.gravity,1f/60);
            input.Pressed=input.Released=0;
        }
        // FighterMatch owns stepping and resolution; SimulationWorld.AutomaticTicks is disabled in PaulLab.
        public Vector3 DesiredVelocity(float dt)=>Vector3.zero;
        public void AfterMove(float dt,uint tick){}
        public void Stop(){input=new FightInputFrame{Direction=5};Buffer.Clear();State.Reset();movementFrames=downFrames=upFrames=0;}
    }
}
