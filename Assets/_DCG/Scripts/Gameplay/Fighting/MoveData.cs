using DCG.Core;
using UnityEngine;
namespace DCG.Gameplay.Fighting
{
    public enum HitLevel { High, Mid, Low, Throw, SpecialMid }
    [CreateAssetMenu(menuName="DCG/Fighting/Move")]
    public sealed class MoveData : ScriptableObject
    {
        public string moveId,command;
        public ReferenceStatus referenceStatus=ReferenceStatus.TuningPending;
        public int priority,startup=10,active=2,recovery=15;
        public HitLevel level;
        public float damage=10,range=2;
        [HideInInspector] public int onHit=3,onBlock=-5,onCounter=6;
        // Legacy advantage fields remain serialized for existing prototype assets/tools.
        public HitOutcome hitOutcome=new HitOutcome(),counterOutcome=new HitOutcome(),airborneOutcome=new HitOutcome(),groundOutcome=new HitOutcome(),crouchingOutcome=new HitOutcome();
        public BlockOutcome blockOutcome=new BlockOutcome();
        public bool hasCounterOutcome,hasAirborneOutcome,hasGroundOutcome,hasCrouchingOutcome,homing;
        public float counterDamageBonus;
        public PropertyWindow highCrush,lowCrush,powerCrush;
        public int chainStart=6,chainEnd=18;
        public string follows;
        public int Total=>startup+active+recovery;
    }
}
