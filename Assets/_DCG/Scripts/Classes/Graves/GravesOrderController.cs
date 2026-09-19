using DCG.Core;
using DCG.Gameplay;
using DCG.Gameplay.Navigation;
using UnityEngine;

namespace DCG.Classes.Graves
{
    public sealed class GravesOrderController : MonoBehaviour, IMovementPolicy
    {
        public GridGraph grid;
        ActorSimulation actor;
        IPathService paths;
        ActorSimulation target;
        Vector3 destination, lastTargetPosition, lastPosition;
        float repath, stuckTime;
        public OrderType Order { get; private set; }
        public ActorId TargetId => target != null ? target.Id : default;
        public PathFollower Follower { get; } = new PathFollower();
        void EnsureReady()
        {
            if (actor == null) actor = GetComponent<ActorSimulation>();
            if (paths == null) paths = new AStarPathService(grid);
        }
        public void Receive(PlayerCommand command)
        {
            EnsureReady(); actor.Combat.CancelWindup(); target = null;
            switch (command.Envelope.CommandType)
            {
                case CommandType.MoveTo:
                case CommandType.AttackMove:
                    Order = command.Envelope.CommandType == CommandType.MoveTo ? OrderType.MoveTo : OrderType.AttackMove;
                    destination = command.Move.Destination; RequestPath(destination); break;
                case CommandType.Target:
                    Order = OrderType.AttackTarget; target = actor.World.Find(command.Target.TargetActorId);
                    if (target != null) { lastTargetPosition = target.transform.position; RequestPath(lastTargetPosition); }
                    repath = actor.tuning.repathSeconds; break;
                case CommandType.Stop: Stop(); break;
            }
        }
        void RequestPath(Vector3 point)
        {
            uint request = Follower.BeginRequest();
            Follower.Accept(paths.Find(transform.position, point, request));
            lastPosition = transform.position; stuckTime = 0;
        }
        public void Stop()
        {
            Order = OrderType.Idle; target = null; Follower.Stop();
            if (actor != null && actor.Combat != null) actor.Combat.CancelWindup();
        }
        public Vector3 DesiredVelocity(float dt)
        {
            EnsureReady(); repath -= dt;
            if (Order == OrderType.Idle) return Vector3.zero;
            if (target != null && !target.Health.IsAlive)
            {
                target = null;
                if (Order == OrderType.AttackTarget) { Stop(); return Vector3.zero; }
                RequestPath(destination);
            }
            if (Order == OrderType.AttackMove && target == null)
            {
                target = TargetTracker.Acquire(actor);
                if (target != null) { lastTargetPosition = target.transform.position; repath = 0; }
            }
            if (target != null)
            {
                // A selected target can be approached around cover, using its last observed point.
                if (Physics.Linecast(actor.AimPoint, target.AimPoint, actor.World.WorldMask,
                    QueryTriggerInteraction.Ignore))
                {
                    if (!Follower.HasPath)
                    {
                        target = null;
                        if (Order == OrderType.AttackTarget) { Stop(); return Vector3.zero; }
                        RequestPath(destination);
                    }
                }
                else if (actor.Combat.CanReach(target))
                {
                    Follower.Stop();
                    actor.Combat.TryAttack(target);
                    actor.Motor.Face(target.transform.position - transform.position, actor.tuning.turnSpeed, dt);
                    return Vector3.zero;
                }
                else if (repath <= 0 || !Follower.HasPath)
                {
                    lastTargetPosition = target.transform.position;
                    RequestPath(lastTargetPosition); repath = actor.tuning.repathSeconds;
                }
            }
            if (actor.Combat.State == ActionState.Windup) return Vector3.zero;
            Vector3 velocity = Follower.Velocity(transform.position, actor.tuning.moveSpeed, actor.tuning.arrivalDistance, dt);
            if (velocity.sqrMagnitude > 0)
            {
                if ((transform.position - lastPosition).sqrMagnitude < .0001f) stuckTime += dt; else stuckTime = 0;
                lastPosition = transform.position;
                if (stuckTime > actor.tuning.stuckSeconds)
                    RequestPath(target != null ? lastTargetPosition : destination);
            }
            else if (target == null) Stop();
            return velocity;
        }
    }
}
