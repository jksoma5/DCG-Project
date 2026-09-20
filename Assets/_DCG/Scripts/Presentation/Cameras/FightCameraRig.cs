using DCG.Gameplay.Fighting;
using UnityEngine;
namespace DCG.Presentation
{
    // Side view of a fight. The camera used to stand south of a stage built along the map's east-west
    // axis; now it stands on the perpendicular of the fight plane, which the two participants define
    // between them (see FightPlane). Screen right is therefore always the direction of the opponent,
    // whichever way round the two have moved, and the left-right controls never mirror.
    public sealed class FightCameraRig : MonoBehaviour
    {
        public Transform first,second;
        public Camera viewCamera;
        public float height=3,minDistance=7,maxDistance=18,fieldOfView=48;
        // The plane turns with the pair. Following it instantly would snap the view around while they
        // circle each other, so the axis is eased toward its target.
        public float turnSmoothing=8;
        Vector3 axis;
        void LateUpdate()
        {
            if(first==null||second==null||viewCamera==null)return;
            var plane=FightPlane.Between(first.position,second.position,axis,.05f);
            axis=axis.sqrMagnitude>1e-6f
                ? Vector3.Slerp(axis,plane.Axis,1-Mathf.Exp(-turnSmoothing*Time.deltaTime))
                : plane.Axis;
            var eased=new FightPlane(plane.Origin,axis);
            float distance=Mathf.Clamp(Vector3.Distance(first.position,second.position)*.8f+5,minDistance,maxDistance);
            Vector3 center=eased.Origin+Vector3.up;
            transform.position=center+eased.Normal*distance+Vector3.up*(height-1);
            transform.LookAt(center);
            viewCamera.fieldOfView=fieldOfView;
        }
    }
}
