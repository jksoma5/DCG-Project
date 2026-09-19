using DCG.Core;
using UnityEngine;
namespace DCG.Classes.Sniper
{
    [CreateAssetMenu(menuName="DCG/Sniper Loadout Tuning")]
    public sealed class SniperTuning : ScriptableObject
    {
        public ReferenceStatus referenceStatus=ReferenceStatus.TuningPending;
        [Header("Prototype timings and speeds; not measured original values")]
        public float sniperSpeed=4.2f,pistolSpeed=5,knifeSpeed=6;
        public float walkMultiplier=.45f,crouchMultiplier=.5f,jumpSpeed=6;
        public float standingHeight=1.8f,crouchHeight=1.1f,inputTimeout=.3f;
        public float drawSeconds=.2f;
        public int sniperMagazine=5,sniperReserve=20,pistolMagazine=12,pistolReserve=48;
        public float sniperDamage=100,pistolDamage=30;
        public float sniperCycle=.9f,pistolCycle=.18f,sniperReload=2.5f,pistolReload=1.5f;
        public float gunRange=160,sniperHipSpread=3,sniperScopedSpread=.015f,pistolSpread=.35f;
        public float knifeRange=1.8f,knifeDamage=35,knifeHeavyDamage=65,knifeCycle=.4f,knifeHeavyCycle=.75f;
        public float normalFov=75,scopeFov=24,deepScopeFov=12,mouseSensitivity=.12f,scopeSensitivity=.035f;
    }
}
