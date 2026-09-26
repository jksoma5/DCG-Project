using DCG.Core;
using DCG.Gameplay;
using DCG.Gameplay.Fighting;
using DCG.Classes.Rifle;
using DCG.Classes.Sniper;
using DCG.Networking;
using UnityEngine;

namespace DCG.Bootstrap.Hub
{
    public sealed class DuelReplicaView : MonoBehaviour
    {
        ActorSimulation actor;
        RifleController rifle;
        SniperController sniper;
        IReplicaStateSource source;
        uint effect, shown;
        Vector3 origin, impact;
        public void Initialize(ActorSimulation value)
        {
            actor = value; source = actor.Movement as IReplicaStateSource;
            rifle = actor.GetComponent<RifleController>(); sniper = actor.GetComponent<SniperController>();
            actor.Combat.Fired += OnGravesShot;
            if (rifle != null) rifle.Tracer += OnShot;
            if (sniper != null) sniper.Fired += OnShot;
        }
        void OnGravesShot(CombatEvent value) { OnShot(value.Origin, value.ImpactPoint); }
        void OnShot(Vector3 from, Vector3 to) { effect++; origin = from; impact = to; }
        public DuelActorPacket Capture(uint tick) => new DuelActorPacket {
            Actor = actor.Snapshot(tick), View = source?.CaptureReplica() ?? default,
            Reaction = actor.Reaction != null ? actor.Reaction.State.CaptureReplica(null) : default,
            Effect = effect, ShotOrigin = origin, ShotImpact = impact
        };
        public void Apply(DuelActorPacket value, bool owner)
        {
            actor.Health.ApplySnapshot(value.Actor.Health);
            if (!owner) source?.ApplyReplica(value.View);
            if (rifle != null) rifle.ApplyAmmoSnapshot(value.Actor.Ammo);
            else if (sniper != null) sniper.ApplyAmmoSnapshot(value.Actor.Ammo);
            else actor.Combat.ApplyAmmoSnapshot(value.Actor.Ammo);
            if (actor.Reaction != null) actor.Reaction.State.ApplyReplica(value.Reaction, null);
            if (owner && actor.Movement is FighterAgent fighter &&
                (value.View.Reaction != 0 || fighter.State.ReactionState != FighterReactionState.None))
                fighter.State.ApplyReplica(value.View, fighter.moveSet.moves);
            if (!actor.Health.IsAlive && actor.Movement is FighterAgent defeated) defeated.State.Defeat();
            if (!owner && value.Effect != shown)
            {
                shown = value.Effect;
                if (rifle != null) rifle.ShowNetworkShot(value.ShotOrigin, value.ShotImpact);
                else if (sniper != null) sniper.ShowNetworkShot(value.ShotOrigin, value.ShotImpact);
                else actor.Combat.ShowNetworkShot(shown, value.ShotOrigin, value.ShotImpact);
            }
        }
        void OnDestroy()
        {
            if (actor != null && actor.Combat != null) actor.Combat.Fired -= OnGravesShot;
            if (rifle != null) rifle.Tracer -= OnShot;
            if (sniper != null) sniper.Fired -= OnShot;
        }
    }
}
