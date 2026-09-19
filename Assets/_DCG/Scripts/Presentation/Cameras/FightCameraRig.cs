using UnityEngine;
namespace DCG.Presentation
{
    public sealed class FightCameraRig : MonoBehaviour
    {
        public Transform first,second;
        public Camera viewCamera;
        void LateUpdate()
        {
            Vector3 center=(first.position+second.position)*.5f+Vector3.up;
            float distance=Mathf.Clamp(Vector3.Distance(first.position,second.position)*.8f+5,7,18);
            transform.position=center+new Vector3(0,3,-distance);
            transform.LookAt(center);viewCamera.fieldOfView=48;
        }
    }
}
