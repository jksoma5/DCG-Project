using UnityEngine;
namespace DCG.Presentation
{
    public sealed class FirstPersonScopeRig : MonoBehaviour
    {
        public Camera viewCamera;
        public Material scopeMaterial;
        Transform scopeRoot;
        Mesh maskMesh;
        void Awake()
        {
            scopeRoot=new GameObject("Scope mask and reticle").transform;scopeRoot.SetParent(transform,false);
            scopeRoot.localPosition=new Vector3(0,0,.1f);
            const int count=96;
            var vertices=new Vector3[count*2];var triangles=new int[count*6];
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;var v=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                vertices[i*2]=v*.84f;vertices[i*2+1]=v*20;
                int n=(i+1)%count;
                int t=i*6;triangles[t]=i*2;triangles[t+1]=n*2;triangles[t+2]=i*2+1;
                triangles[t+3]=n*2;triangles[t+4]=n*2+1;triangles[t+5]=i*2+1;
            }
            maskMesh=new Mesh{name="Scope ring"};maskMesh.vertices=vertices;maskMesh.triangles=triangles;maskMesh.RecalculateBounds();
            scopeRoot.gameObject.AddComponent<MeshFilter>().sharedMesh=maskMesh;
            scopeRoot.gameObject.AddComponent<MeshRenderer>().sharedMaterial=scopeMaterial;
            Reticle("Horizontal",new Vector3(1.7f,.004f,1));Reticle("Vertical",new Vector3(.004f,1.7f,1));
            scopeRoot.gameObject.SetActive(false);
        }
        void Reticle(string name,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name=name;Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(scopeRoot,false);go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=scopeMaterial;
        }
        public void Place(Vector3 eye,Quaternion rotation,float fov,bool scoped)
        {
            transform.SetPositionAndRotation(eye,rotation);viewCamera.fieldOfView=fov;
            if(scopeRoot==null)return;
            scopeRoot.gameObject.SetActive(scoped);
            scopeRoot.localScale=Vector3.one*(.1f*Mathf.Tan(fov*.5f*Mathf.Deg2Rad));
        }
        void OnDestroy(){if(maskMesh!=null)Destroy(maskMesh);}
    }
}
