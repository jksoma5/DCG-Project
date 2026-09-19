using DCG.Gameplay;
using UnityEngine;

namespace DCG.Presentation
{
    public sealed class TopDownRig : MonoBehaviour
    {
        public Transform target;
        public PrototypeTuning tuning;
        void Start()
        {
            if (target == null || tuning == null) return;
            transform.position = target.position + tuning.cameraOffset;
            transform.rotation = Quaternion.LookRotation(-tuning.cameraOffset);
            GetComponent<Camera>().fieldOfView = tuning.cameraFov;
        }
        void LateUpdate()
        {
            if (target == null || tuning == null) return;
            Vector3 desired = target.position + tuning.cameraOffset;
            transform.position = Vector3.Lerp(transform.position, desired,
                1 - Mathf.Exp(-tuning.cameraFollowSpeed * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(-tuning.cameraOffset);
        }
    }
}
