using DCG.Core;
using UnityEngine;
namespace DCG.Gameplay.Fighting
{
    // One fight. The near side is always a fighter; the far side is either a second fighter or any other
    // actor - a class from a different game with no guard, no side and no frame data. The loop is the
    // same either way: advance the frame, rebuild the plane, let each side act, then resolve contact.
    public sealed class FighterMatch : MonoBehaviour
    {
        public SimulationWorld world;
        public FighterAgent first,second;
        // Set instead of `second` when the opponent is not a fighter.
        public ActorSimulation opponentActor;
        public int Frame { get; private set; }
        // The 2D plane this match is played in, rebuilt every frame from the two participants.
        public FightPlane Plane { get; private set; }
        public ActorSimulation Defender=>second!=null?second.Actor:opponentActor;
        public Transform DefenderTransform=>second!=null?second.transform:opponentActor!=null?opponentActor.transform:null;
        // Owns everything: registers both fighters and takes the world off automatic ticks.
        // This is what a fighting-only scene wants.
        public void Initialize()
        {
            world.AutomaticTicks=false;
            world.Register(first.GetComponent<ActorSimulation>());
            if(second!=null)world.Register(second.GetComponent<ActorSimulation>());
            InitializeAgents();
        }
        // Agents only. The control hub registers actors with hub-issued ids and owns the tick policy
        // itself, so it must not have either of those done behind its back (doc 14, conflict 3 and 4).
        public void InitializeAgents()
        {
            first.Initialize();first.Opponent=second;
            if(second!=null){second.Initialize();second.Opponent=first;second.Target=first.transform;}
            first.Target=DefenderTransform;
            UpdatePlane();
        }
        // Points the match at a different far side. The hub links two live classes this way once both
        // exist, so either of them can be the one the player is driving.
        public void SetOpponent(FighterAgent fighter,ActorSimulation actor)
        {
            second=fighter;opponentActor=fighter!=null?fighter.Actor:actor;
            first.Opponent=fighter;first.Target=DefenderTransform;
            if(fighter!=null){fighter.Opponent=first;fighter.Target=first.transform;}
            UpdatePlane();
        }
        // The plane runs from first to the far side, so first is on the left of the screen and the
        // opponent on the right, and it stays that way however the pair turns. The camera follows the
        // plane instead of the map, which is why crossing over no longer mirrors the controls: there is
        // no crossing over when the axis is defined by the pair itself.
        public void UpdatePlane()
        {
            var far=DefenderTransform;
            if(far==null)return;
            Plane=FightPlane.Between(first.transform.position,far.position,Plane.Axis,first.moveSet.sideEpsilon);
            first.Side=FightDirections.Side(Plane.Along(first.transform.position),
                Plane.Along(far.position),first.Side,first.moveSet.sideEpsilon);
            if(second!=null)second.Side=first.Side==FightSide.Normal?FightSide.Reversed:FightSide.Normal;
        }
        public void StepFrame()
        {
            Frame++;
            world.Session.Drain();
            if(first.Actor.Health.IsAlive)first.State.Advance();
            if(second!=null&&second.Actor.Health.IsAlive)second.State.Advance();
            if(first.CanChangeSide&&(second==null||second.CanChangeSide))UpdatePlane();
            first.Prepare(Frame);
            if(second!=null)second.Prepare(Frame);
            Physics.SyncTransforms();
            if(second!=null)
            {
                // Capture both contacts before any damage or hit stun can cancel the other attack.
                var a=FightResolver.Evaluate(first,second);var b=FightResolver.Evaluate(second,first);
                FightResolver.Apply(a);FightResolver.Apply(b);
            }
            else if(opponentActor!=null)
            {
                // Nothing to capture in pairs: the other side has no attack frames of its own. Its guns
                // reach the fighter through the ordinary damage path instead.
                FightResolver.ApplyActor(FightResolver.EvaluateActor(first,opponentActor));
            }
        }
    }
}
