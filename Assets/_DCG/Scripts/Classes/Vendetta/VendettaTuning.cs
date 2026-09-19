using DCG.Core;
using UnityEngine;
namespace DCG.Classes.Vendetta
{
    [CreateAssetMenu(menuName = "DCG/Vendetta Tuning")]
    public sealed class VendettaTuning : ScriptableObject
    {
        public ReferenceStatus referenceStatus = ReferenceStatus.TuningPending;
        [Header("Prototype values; user-defined ability behavior, unverified original timing")]
        public float moveSpeed = 5.5f;
        public float jumpSpeed = 7;
        public float inputTimeout = .3f;
        public float dashDistance = 6;
        public float dashSeconds = .25f;
        public float spinSeconds = .35f;
        public float spinRadius = 2.8f;
        public float spinDamage = 45;
        public float shiftCooldown = 5;
        public float swordDistance = 10;
        public float swordThrowSeconds = .3f;
        public float flightSpeed = 20;
        public float arrivalDistance = .12f;
        public float eCooldown = 6;
        public float mouseSensitivity = .12f;
        public float cameraDistance = 5;
        public Vector3 cameraPivot = new Vector3(.45f, 1.6f, 0);
        public float cameraRadius = .2f;
        void OnValidate()
        {
            moveSpeed = Mathf.Max(0,moveSpeed); jumpSpeed = Mathf.Max(0,jumpSpeed);
            inputTimeout = Mathf.Max(.05f,inputTimeout);
            dashDistance = Mathf.Max(.1f,dashDistance); dashSeconds = Mathf.Max(.02f,dashSeconds);
            spinSeconds = Mathf.Max(.02f,spinSeconds); spinRadius = Mathf.Max(.1f,spinRadius); spinDamage = Mathf.Max(0,spinDamage);
            shiftCooldown = Mathf.Max(0,shiftCooldown); eCooldown = Mathf.Max(0,eCooldown);
            swordDistance = Mathf.Max(.1f,swordDistance); swordThrowSeconds = Mathf.Max(.02f,swordThrowSeconds);
            flightSpeed = Mathf.Max(.1f,flightSpeed); arrivalDistance = Mathf.Max(.02f,arrivalDistance);
        }
    }
}
