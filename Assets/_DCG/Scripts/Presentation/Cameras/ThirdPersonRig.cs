using UnityEngine;
namespace DCG.Presentation
{
    public sealed class ThirdPersonRig : MonoBehaviour
    {
        public Transform target;
        public Vector2 aim = new Vector2(0,12);
        public Vector3 pivotOffset = new Vector3(0,1.6f,0);
        public float distance = 5;
        public float radius = .2f;
        void LateUpdate()
        {
            if (target == null) return;
            Quaternion rotation = Quaternion.Euler(aim.y,aim.x,0);
            Vector3 pivot = target.position + Quaternion.Euler(0,aim.x,0) * pivotOffset;
            Vector3 backward = rotation * Vector3.back;
            float actual = distance;
            if (Physics.SphereCast(pivot,radius,backward,out var hit,distance,
                LayerMask.GetMask("World","NavigationSurface"),QueryTriggerInteraction.Ignore))
                actual = Mathf.Max(.1f,hit.distance-.04f);
            transform.SetPositionAndRotation(pivot + backward * actual,rotation);
        }
    }
}
