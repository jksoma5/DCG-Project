using DCG.Core;
using UnityEngine;
namespace DCG.Gameplay.Fighting
{
    public sealed class FighterMatch : MonoBehaviour
    {
        public SimulationWorld world;
        public FighterAgent first,second;
        public int Frame { get; private set; }
        public void Initialize()
        {
            world.AutomaticTicks=false;
            world.Register(first.GetComponent<ActorSimulation>());world.Register(second.GetComponent<ActorSimulation>());
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
