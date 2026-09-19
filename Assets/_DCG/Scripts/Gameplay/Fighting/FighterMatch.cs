using DCG.Core;
using UnityEngine;
namespace DCG.Gameplay.Fighting
{
    public sealed class FighterMatch : MonoBehaviour
    {
        public SimulationWorld world;
        public FighterAgent first,second;
        public int Frame { get; private set; }
        // Owns everything: registers both fighters and takes the world off automatic ticks.
        // This is what a fighting-only scene wants.
        public void Initialize()
        {
            world.AutomaticTicks=false;
            world.Register(first.GetComponent<ActorSimulation>());world.Register(second.GetComponent<ActorSimulation>());
            InitializeAgents();
        }
        // Agents only. The control hub registers actors with hub-issued ids and owns the tick policy
        // itself, so it must not have either of those done behind its back (doc 14, conflict 3 and 4).
        public void InitializeAgents()
        {
            first.Initialize();second.Initialize();first.Opponent=second;second.Opponent=first;
            first.Side=FightDirections.Side(first.transform.position.x,second.transform.position.x,FightSide.Normal,first.moveSet.sideEpsilon);
            second.Side=first.Side==FightSide.Normal?FightSide.Reversed:FightSide.Normal;
        }
        public void StepFrame()
        {
            Frame++;
            world.Session.Drain();
            if(first.Actor.Health.IsAlive)first.State.Advance();if(second.Actor.Health.IsAlive)second.State.Advance();
            if(first.CanChangeSide&&second.CanChangeSide)
            {
                first.Side=FightDirections.Side(first.transform.position.x,second.transform.position.x,first.Side,first.moveSet.sideEpsilon);
                second.Side=first.Side==FightSide.Normal?FightSide.Reversed:FightSide.Normal;
            }
            first.Prepare(Frame);second.Prepare(Frame);
            Physics.SyncTransforms();
            // Capture both contacts before any damage or hit stun can cancel the other attack.
            var a=FightResolver.Evaluate(first,second);var b=FightResolver.Evaluate(second,first);
            FightResolver.Apply(a);FightResolver.Apply(b);
        }
    }
}
