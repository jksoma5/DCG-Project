using System;
using System.IO;
using System.Linq;
using DCG.Core;
using DCG.Gameplay;
using DCG.Classes.Rifle;
using DCG.Presentation;
using DCG.Bootstrap;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
namespace DCG.Editor
{
    public static class RifleLabSetup
    {
        const string Root = "Assets/_DCG/";
        public const string ScenePath = Root + "Scenes/RifleLab.unity";
        static Material floor, cover, body, target, metal, accent;
        [MenuItem("DCG/Generate Rifle Lab")]
        public static void Generate()
        {
            ProjectSetup.ValidateRestoration();
            var previous = SceneManager.GetActiveScene();
            var mode = Application.isBatchMode && string.IsNullOrEmpty(previous.path) ? NewSceneMode.Single : NewSceneMode.Additive;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            SceneManager.SetActiveScene(scene);
            try
            {
                floor = Material("RifleFloor",new Color(.075f,.09f,.11f));
                cover = Material("RifleCover",new Color(.22f,.27f,.31f));
                body = Material("RifleBody",new Color(.35f,.55f,.26f));
                target = Material("RifleTarget",new Color(.95f,.38f,.16f));
                metal = Material("RifleMetal",new Color(.07f,.09f,.1f));
                accent = Material("RifleAccent",new Color(.2f,.9f,.85f),true);
                var tune = Asset<RifleTuning>("Rifle_TuningPending");
                var common = Asset<PrototypeTuning>("Rifle_Common");
                var world = new GameObject("Rifle Simulation").AddComponent<SimulationWorld>();
                Cube("Floor",new Vector3(0,-.25f,13),new Vector3(40,.5f,66),floor,"NavigationSurface");
                Cube("Backstop",new Vector3(0,3,44),new Vector3(40,6,.6f),cover,"World");
                Cube("Left wall",new Vector3(-20,2,13),new Vector3(.5f,4,66),cover,"World");
                Cube("Right wall",new Vector3(20,2,13),new Vector3(.5f,4,66),cover,"World");
                Cube("South wall",new Vector3(0,2,-20),new Vector3(40,4,.5f),cover,"World");
                Cube("Tall cover",new Vector3(5,1.5f,2),new Vector3(3,3,2),cover,"World");
                Cube("Crouch cover",new Vector3(-5,.6f,3),new Vector3(4,1.2f,1),cover,"World");
                Cube("Low ceiling",new Vector3(-11,1.5f,0),new Vector3(4,.3f,5),cover,"World");
                for(int z=0;z<=40;z+=10)
                    Cube("Range stripe "+z,new Vector3(0,.012f,z),new Vector3(32,.02f,.05f),accent,"LocalViewModel");
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.68f,.72f);
                var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;
                sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(55,-25,0);
                var player=Actor("M416",201,0,new Vector3(0,0,-8),common,body);
                player.classId=ClassId.Rifle;
                var rifle=player.gameObject.AddComponent<RifleController>();rifle.tuning=tune;
                Transform gun=Gun("M416 placeholder",metal,accent);
                gun.SetParent(player.transform,false);gun.localPosition=new Vector3(.18f,1.55f,.5f);
                var prefab=PrefabUtility.SaveAsPrefabAsset(player.gameObject,Root+"Prefabs/Actors/Rifle.prefab");
                Actor("Near target",202,1,new Vector3(0,0,8),common,target);
                Actor("Far target",203,1,new Vector3(-5,0,28),common,target);
                Actor("Covered target",204,1,new Vector3(5,0,8),common,target);
                var camera=new GameObject("Shoulder and ADS camera").AddComponent<Camera>();
                camera.tag="MainCamera";camera.nearClipPlane=.03f;camera.farClipPlane=220;camera.fieldOfView=tune.hipFov;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.045f,.075f);
                camera.transform.position=player.transform.position+new Vector3(tune.shoulderOffset,1.64f,-tune.hipDistance);
                camera.gameObject.AddComponent<AudioListener>();
                var rig=camera.gameObject.AddComponent<ShoulderCameraRig>();rig.viewCamera=camera;
                var input=camera.gameObject.AddComponent<RifleInputReader>();input.actor=player;input.rifle=rifle;input.worldCamera=camera;
                input.controls=AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root+"Input/DCGControls.inputactions");
                var lab=new GameObject("Rifle Lab").AddComponent<RifleLabController>();
                lab.world=world;lab.player=player;lab.rifle=rifle;lab.input=input;lab.rig=rig;lab.gun=gun;
                lab.model=player.transform.Find("Body");
                lab.adsGun=Gun("Local ADS weapon",metal,accent);lab.adsGun.SetParent(camera.transform,false);
                lab.adsGun.localPosition=new Vector3(0,-.09425f,.32f);lab.adsGun.localScale=Vector3.one*.65f;
                lab.adsGun.gameObject.SetActive(false);
                lab.tracer=new GameObject("Bullet tracer").AddComponent<LineRenderer>();
                lab.tracer.sharedMaterial=accent;lab.tracer.positionCount=2;lab.tracer.startWidth=lab.tracer.endWidth=.025f;lab.tracer.enabled=false;
                lab.gameObject.AddComponent<RifleSmokeProbe>();
                var definition=Asset<ClassDefinition>("Rifle");definition.classId=ClassId.Rifle;definition.actorPrefab=prefab;
                definition.prototypeTuning=common;definition.playableInLab=true;definition.referenceStatus=ReferenceStatus.TuningPending;
                definition.referenceGame="PUBG M416 / PC shoulder aim + ADS";
                definition.pendingNotes="Separate RifleLab. Prototype gunplay, movement, stance and aim. Balance and animation not verified against PUBG.";
                EditorUtility.SetDirty(definition);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
                if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath))
                    EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
                Debug.Log("RIFLE_SETUP_OK");
            }
            finally
            {
                if(previous.IsValid())SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene,true);
            }
        }
        static T Asset<T>(string name) where T:ScriptableObject
        {
            string path=Root+"Data/Classes/"+name+".asset";var value=AssetDatabase.LoadAssetAtPath<T>(path);
            if(value==null){value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);}return value;
        }
        static Material Material(string name,Color color,bool unlit=false)
        {
            string path=Root+"Materials/"+name+".mat";var value=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(value==null){value=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(value,path);}
            value.SetColor("_BaseColor",color);EditorUtility.SetDirty(value);return value;
        }
        static GameObject Cube(string name,Vector3 position,Vector3 scale,Material material,string layer)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=scale;
            go.layer=LayerMask.NameToLayer(layer);go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static Transform Gun(string name,Material material,Material sight)
        {
            var root=new GameObject(name).transform;
            Part("Receiver",new Vector3(0,0,0),new Vector3(.12f,.14f,.5f),material,root);
            Part("Barrel",new Vector3(0,.015f,.42f),new Vector3(.045f,.045f,.45f),material,root);
            Part("Magazine",new Vector3(0,-.15f,-.03f),new Vector3(.09f,.23f,.13f),material,root);
            Part("Stock",new Vector3(0,0,-.38f),new Vector3(.09f,.16f,.28f),material,root);
            Part("Front sight",new Vector3(0,.11f,.5f),new Vector3(.02f,.07f,.02f),sight,root);
            return root;
        }
        static void Part(string name,Vector3 position,Vector3 scale,Material material,Transform parent)
        {
            var go=Cube(name,Vector3.zero,scale,material,"LocalViewModel");UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=position;
        }
        static ActorSimulation Actor(string name,uint id,int team,Vector3 position,PrototypeTuning tune,Material material)
        {
            var go=new GameObject(name);go.transform.position=position;go.layer=LayerMask.NameToLayer("CharacterBody");
            var cc=go.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.3f;cc.center=Vector3.up*.9f;cc.skinWidth=.025f;
            go.AddComponent<CharacterMotor>();var actor=go.AddComponent<ActorSimulation>();actor.actorNumber=id;actor.team=team;actor.tuning=tune;
            var hurt=new GameObject("Hurtbox");hurt.transform.SetParent(go.transform,false);hurt.layer=LayerMask.NameToLayer("Hurtbox");
            var collider=hurt.AddComponent<CapsuleCollider>();collider.height=1.8f;collider.radius=.35f;collider.center=Vector3.up*.9f;collider.isTrigger=true;
            var mesh=GameObject.CreatePrimitive(PrimitiveType.Capsule);mesh.name="Body";UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.transform.SetParent(go.transform,false);mesh.transform.localPosition=Vector3.up*.9f;mesh.transform.localScale=new Vector3(.7f,.9f,.7f);
            mesh.GetComponent<Renderer>().sharedMaterial=material;
            var view=go.AddComponent<ActorView>();view.actor=actor;view.body=mesh.GetComponent<Renderer>();
            return actor;
        }
        [MenuItem("DCG/Build Windows Rifle Lab")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/RifleLab");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},
                locationPathName="Builds/RifleLab/DCG-RifleLab.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Rifle build failed.");
            Debug.Log("RIFLE_BUILD_OK");
        }
    }
}
