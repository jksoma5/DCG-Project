using DCG.Core;
using UnityEngine;
namespace DCG.Classes.Rifle
{
    [CreateAssetMenu(menuName = "DCG/Rifle Tuning")]
    public sealed class RifleTuning : ScriptableObject
    {
        public ReferenceStatus referenceStatus = ReferenceStatus.TuningPending;
        [Header("Prototype values, not measured PUBG balance")]
        [Min(0)] public float moveSpeed = 4.5f, sprintSpeed = 6.5f, walkSpeed = 2, crouchSpeed = 2.2f, proneSpeed = 1.1f;
        [Min(0)] public float aimSpeed = 2.3f, jumpSpeed = 6;
        [Min(.05f)] public float inputTimeout = .3f;
        [Min(1)] public int magazineSize = 30;
        [Min(0)] public int reserveAmmo = 120;
        [Min(.02f)] public float shotInterval = .086f;
        [Min(.1f)] public float reloadSeconds = 2.3f;
        [Min(0)] public float damage = 35;
        [Min(1)] public float range = 180, bulletSpeed = 880;
        [Min(0)] public float bulletGravity = 9.81f;
        [Min(0)] public float hipSpread = 1.5f, aimSpread = .45f, adsSpread = .06f;
        [Min(0)] public float recoilPerShot = .8f, recoilRecovery = 2.5f, maxRecoil = 9;
        [Min(.01f)] public float mouseSensitivity = .12f, adsSensitivity = .07f;
        [Min(.05f)] public float aimHoldThreshold = .18f;
        public float standingHeight = 1.8f, crouchHeight = 1.15f, proneHeight = .7f;
        public float hipFov = 70, shoulderFov = 56, adsFov = 42;
        public float shoulderOffset = .65f, hipDistance = 3.1f, aimDistance = 1.5f;
    }
}
