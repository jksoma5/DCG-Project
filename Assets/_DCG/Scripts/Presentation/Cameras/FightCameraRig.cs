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
        // The view is fixed to the fight plane, so it cannot step around a wall between it and the
        // fighters. Surfaces drawn with DCG/Occlusion Dither Lit that are nearer to the camera than
        // the nearer fighter, less the margin, are dithered out over the range, down to the given share
        // of their pixels. The fighters themselves stay solid.
        public float occlusionMargin=.6f,occlusionRange=1.5f;
        [Range(0,1)]public float occlusionMinVisible=.3f;
        static readonly int OcclusionStrength=Shader.PropertyToID("_DCGOcclusionStrength");
        static readonly int OcclusionDepth=Shader.PropertyToID("_DCGOcclusionDepth");
        static readonly int OcclusionRange=Shader.PropertyToID("_DCGOcclusionRange");
        static readonly int OcclusionMinVisible=Shader.PropertyToID("_DCGOcclusionMinVisible");
        Vector3 axis;
        void LateUpdate()
        {
            if(first==null||second==null||viewCamera==null){Shader.SetGlobalFloat(OcclusionStrength,0);return;}
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
            UpdateOcclusion();
        }
        // Eye depth is measured to the chest, the same height the camera aims at.
        void UpdateOcclusion()
        {
            var view=transform;
            float depth=Mathf.Min(Vector3.Dot(first.position+Vector3.up-view.position,view.forward),
                Vector3.Dot(second.position+Vector3.up-view.position,view.forward));
            Shader.SetGlobalFloat(OcclusionStrength,1);
            Shader.SetGlobalFloat(OcclusionDepth,depth-occlusionMargin);
            Shader.SetGlobalFloat(OcclusionRange,occlusionRange);
            Shader.SetGlobalFloat(OcclusionMinVisible,occlusionMinVisible);
        }
        // The camera is shared with the other classes in the hub, so the fade must not outlive the rig.
        void OnDisable(){Shader.SetGlobalFloat(OcclusionStrength,0);}
    }
}
