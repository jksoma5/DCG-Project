using UnityEngine;
namespace DCG.Presentation
{
    public sealed class ShoulderCameraRig : MonoBehaviour
    {
        public Camera viewCamera;
        public void Place(Vector3 eye, Quaternion rotation, float shoulder, float distance, float fov, float roll, float dt)
        {
            Vector3 desired = eye + rotation * new Vector3(shoulder, 0, -distance);
            Vector3 delta = desired - eye;
            if (delta.sqrMagnitude > .0001f && Physics.SphereCast(eye, .13f, delta.normalized, out var hit,
                delta.magnitude, LayerMask.GetMask("World", "NavigationSurface"), QueryTriggerInteraction.Ignore))
                desired = eye + delta.normalized * Mathf.Max(0, hit.distance - .04f);
            transform.SetPositionAndRotation(desired, rotation * Quaternion.Euler(0, 0, roll));
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, fov, 1 - Mathf.Exp(-16 * dt));
        }
    }
}
