using System;
using System.IO;
using System.Linq;
using DCG.Core;
using DCG.Gameplay;
using DCG.Classes.Sniper;
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
    public static class SniperLabSetup
    {
        const string Root="Assets/_DCG/";
        public const string ScenePath=Root+"Scenes/SniperLab.unity";
        [MenuItem("DCG/Generate Sniper Lab")]
        public static void Generate()
        {
            ProjectSetup.ValidateRestoration();
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode&&string.IsNullOrEmpty(previous.path)?NewSceneMode.Single:NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var floor=Material("SniperFloor",new Color(.12f,.13f,.15f));
                var wall=Material("SniperWall",new Color(.33f,.37f,.4f));
                var target=Material("SniperTarget",new Color(.9f,.33f,.2f));
                var metal=Material("SniperMetal",new Color(.15f,.19f,.23f));
                var wood=Material("SniperStock",new Color(.23f,.3f,.18f));
                var accent=Material("SniperAccent",new Color(.5f,.85f,.9f),true);
                var scope=Material("SniperScope",Color.black,true);scope.SetFloat("_Cull",0);EditorUtility.SetDirty(scope);
                var tune=Asset<SniperTuning>("Sniper_TuningPending");
                var common=Asset<PrototypeTuning>("Sniper_Common");
                var world=new GameObject("Sniper Simulation").AddComponent<SimulationWorld>();
                Cube("Floor",new Vector3(0,-.25f,20),new Vector3(40,.5f,80),floor,"NavigationSurface");
                Cube("Backstop",new Vector3(0,3,59),new Vector3(40,6,1),wall,"World");
                Cube("Left wall",new Vector3(-20,2,20),new Vector3(1,4,80),wall,"World");
                Cube("Right wall",new Vector3(20,2,20),new Vector3(1,4,80),wall,"World");
                Cube("South wall",new Vector3(0,2,-20),new Vector3(40,4,1),wall,"World");
                Cube("Cover",new Vector3(6,1.6f,5),new Vector3(3,3.2f,2),wall,"World");
                Cube("Jump box",new Vector3(-6,.4f,0),new Vector3(3,.8f,3),wall,"World");
                Cube("Crouch ceiling",new Vector3(-11,1.5f,0),new Vector3(4,.3f,5),wall,"World");
                for(int z=0;z<=50;z+=10)Cube("Range "+z,new Vector3(0,.015f,z),new Vector3(30,.02f,.04f),accent,"LocalViewModel");
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.68f,.72f);
                var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.4f;
                sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(50,-30,0);
                var player=Actor("Sniper loadout",301,0,new Vector3(0,0,-8),common,metal);
                player.classId=ClassId.Sniper;
                var sniper=player.gameObject.AddComponent<SniperController>();sniper.tuning=tune;
                player.GetComponent<ActorView>().body.enabled=false;
                var prefab=PrefabUtility.SaveAsPrefabAsset(player.gameObject,Root+"Prefabs/Actors/Sniper.prefab");
                Actor("Near target",302,1,new Vector3(0,0,12),common,target);
                Actor("Far target",303,1,new Vector3(-4,0,40),common,target);
                Actor("Covered target",304,1,new Vector3(6,0,12),common,target);
                Actor("Knife target",305,1,new Vector3(-4,0,-5),common,target);
                var camera=new GameObject("First person camera").AddComponent<Camera>();camera.tag="MainCamera";
                camera.nearClipPlane=.025f;camera.farClipPlane=190;camera.fieldOfView=tune.normalFov;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.055f,.08f);
                camera.gameObject.AddComponent<AudioListener>();
                var rig=camera.gameObject.AddComponent<FirstPersonScopeRig>();rig.viewCamera=camera;rig.scopeMaterial=scope;
                var input=camera.gameObject.AddComponent<SniperInputReader>();input.actor=player;input.sniper=sniper;
                input.controls=AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root+"Input/DCGControls.inputactions");
                var lab=new GameObject("Sniper Lab").AddComponent<SniperLabController>();
                lab.world=world;lab.player=player;lab.sniper=sniper;lab.input=input;lab.rig=rig;
                lab.weapons=new[]{Weapon("TRG prototype",1,camera.transform,metal,wood,accent),
                    Weapon("Pistol prototype",2,camera.transform,metal,wood,accent),Weapon("Knife prototype",3,camera.transform,metal,wood,accent)};
                lab.tracer=new GameObject("Shot tracer").AddComponent<LineRenderer>();
                lab.tracer.sharedMaterial=accent;lab.tracer.positionCount=2;lab.tracer.startWidth=lab.tracer.endWidth=.015f;lab.tracer.enabled=false;
                lab.gameObject.AddComponent<SniperSmokeProbe>();
                var definition=Asset<ClassDefinition>("Sniper");definition.classId=ClassId.Sniper;definition.actorPrefab=prefab;
                definition.prototypeTuning=common;definition.playableInLab=true;definition.referenceStatus=ReferenceStatus.TuningPending;
                definition.referenceGame="Sudden Attack TRG / first-person movement / 1 sniper, 2 pistol, 3 knife";
                definition.pendingNotes="Independent SniperLab. Pistol and knife are generic prototypes. Movement, timing and damage need comparison.";
                EditorUtility.SetDirty(definition);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
                if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath))
                    EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
                Debug.Log("SNIPER_SETUP_OK");
            }
            finally
            {
                if(previous.IsValid())SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene,true);
            }
        }
        static T Asset<T>(string name) where T:ScriptableObject
        {
            string path=Root+"Data/Classes/"+name+".asset";var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(asset==null){asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}return asset;
        }
        static Material Material(string name,Color color,bool unlit=false)
        {
            string path=Root+"Materials/"+name+".mat";var asset=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(asset==null){asset=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(asset,path);}
            asset.SetColor("_BaseColor",color);EditorUtility.SetDirty(asset);return asset;
        }
        static GameObject Cube(string name,Vector3 position,Vector3 scale,Material material,string layer)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=scale;
            go.layer=LayerMask.NameToLayer(layer);go.GetComponent<Renderer>().sharedMaterial=material;return go;
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
            mesh.GetComponent<Renderer>().sharedMaterial=material;var view=go.AddComponent<ActorView>();view.actor=actor;view.body=mesh.GetComponent<Renderer>();return actor;
        }
        static Transform Weapon(string name,int slot,Transform camera,Material metal,Material wood,Material accent)
        {
            var root=new GameObject(name).transform;root.SetParent(camera,false);root.localPosition=new Vector3(.22f,-.22f,.5f);
            if(slot==1)
            {
                Part(root,"Receiver",Vector3.zero,new Vector3(.1f,.12f,.45f),wood);
                Part(root,"Barrel",new Vector3(0,.015f,.4f),new Vector3(.035f,.035f,.4f),metal);
                Part(root,"Stock",new Vector3(0,-.02f,-.3f),new Vector3(.1f,.15f,.25f),wood);
                Part(root,"Scope",new Vector3(0,.12f,0),new Vector3(.1f,.1f,.28f),metal);
                Part(root,"Lens",new Vector3(0,.12f,-.145f),new Vector3(.065f,.065f,.01f),accent);
            }
            else if(slot==2)
            {
                Part(root,"Slide",Vector3.zero,new Vector3(.08f,.08f,.24f),metal);
                Part(root,"Grip",new Vector3(0,-.11f,-.04f),new Vector3(.075f,.17f,.09f),wood);
                Part(root,"Sight",new Vector3(0,.05f,.08f),new Vector3(.015f,.025f,.015f),accent);
            }
            else
            {
                Part(root,"Handle",new Vector3(0,-.07f,0),new Vector3(.065f,.17f,.05f),wood);
                Part(root,"Guard",new Vector3(0,.02f,0),new Vector3(.14f,.035f,.055f),metal);
                Part(root,"Blade",new Vector3(0,.21f,0),new Vector3(.065f,.35f,.015f),accent);
            }
            return root;
        }
        static void Part(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var go=Cube(name,Vector3.zero,scale,material,"LocalViewModel");UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=position;
        }
        [MenuItem("DCG/Build Windows Sniper Lab")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/SniperLab");
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},
                locationPathName="Builds/SniperLab/DCG-SniperLab.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Sniper build failed.");Debug.Log("SNIPER_BUILD_OK");
        }
    }
}
