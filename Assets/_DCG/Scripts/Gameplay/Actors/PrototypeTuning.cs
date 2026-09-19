using DCG.Core;
using UnityEngine;

namespace DCG.Gameplay
{
    [CreateAssetMenu(menuName = "DCG/Prototype Tuning")]
    public sealed class PrototypeTuning : ScriptableObject
    {
        [Header("Temporary lab values, NOT verified original-game values")]
        public ReferenceStatus referenceStatus = ReferenceStatus.TuningPending;
        [Min(1)] public float maxHealth = 100;
        [Min(.1f)] public float moveSpeed = 5;
        [Min(.01f)] public float arrivalDistance = .12f;
        [Min(.1f)] public float turnSpeed = 720;
        public float gravity = -25;
        [Min(.1f)] public float attackRange = 6;
        [Min(.01f)] public float windupSeconds = .15f;
        [Min(.01f)] public float recoverySeconds = .5f;
        [Min(.01f)] public float reloadSeconds = 1.2f;
        [Min(1)] public int magazineSize = 2;
        [Min(1)] public int pelletCount = 4;
        [Min(.1f)] public float damagePerPellet = 7;
        [Range(0, 45)] public float spreadDegrees = 9;
        [Min(.01f)] public float repathSeconds = .3f;
        [Min(.1f)] public float acquireRange = 8;
        [Min(.1f)] public float stuckSeconds = .6f;
        [Min(.01f)] public float cameraFollowSpeed = 12;
        public Vector3 cameraOffset = new Vector3(0, 18, -12);
        [Range(15, 90)] public float cameraFov = 50;
    }
}
