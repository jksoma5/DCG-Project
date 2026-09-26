using System;
using DCG.Core;

namespace DCG.Gameplay.Fighting
{
    public sealed partial class FighterStateMachine
    {
        public ReplicaState CaptureReplica(MoveData[] moves) => new ReplicaState {
            MoveIndex=Move==null||moves==null?-1:Array.IndexOf(moves,Move), Age=Age, Stun=Stun,
            Reaction=(int)ReactionState, ReactionKind=(int)Reaction, ReactionVelocity=ReactionVelocity,
            GuardStun=GuardStun, ForceCrouch=ForceCrouch, Backturned=Backturned
        };
        public void ApplyReplica(ReplicaState value, MoveData[] moves)
        {
            Move=moves!=null&&value.MoveIndex>=0&&value.MoveIndex<moves.Length?moves[value.MoveIndex]:null;
            Age=value.Age; Stun=value.Stun; ReactionState=(FighterReactionState)value.Reaction;
            Reaction=(HitReaction)value.ReactionKind; ReactionVelocity=value.ReactionVelocity;
            GuardStun=value.GuardStun; ForceCrouch=value.ForceCrouch; Backturned=value.Backturned;
        }
    }
    public sealed partial class FighterAgent : IReplicaStateSource
    {
        public ReplicaState CaptureReplica()
        { var value=State.CaptureReplica(moveSet.moves); value.Side=(int)Side; value.Crouched=Crouched; return value; }
        public void ApplyReplica(ReplicaState value)
        { State.ApplyReplica(value,moveSet.moves); Side=(FightSide)value.Side; Crouched=value.Crouched; }
    }
}
