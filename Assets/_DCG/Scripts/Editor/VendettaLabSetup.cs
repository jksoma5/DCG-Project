using System;
using System.IO;
using System.Linq;
using DCG.Core;
using DCG.Gameplay;
using DCG.Classes.Vendetta;
using DCG.Presentation;
using DCG.Bootstrap;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
namespace DCG.Editor
{
    public static class VendettaLabSetup
    {
        const string Root="Assets/_DCG/";
        public const string ScenePath=Root+"Scenes/VendettaLab.unity";
        static Material floor, cover, body, enemy, blade;
        [MenuItem("DCG/Generate Vendetta Lab")]
        public static void Generate()
        {
            ProjectSetup.ValidateRestoration();
            foreach(string dir in new[]{"Data/Classes","Prefabs/Actors","Materials","Scenes"})
                Directory.CreateDirectory(Root+dir);
            AssetDatabase.Refresh();
            var previous=SceneManager.GetActiveScene();
            var mode = Application.isBatchMode && string.IsNullOrEmpty(previous.path)
                ? NewSceneMode.Single : NewSceneMode.Additive;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,mode);
            SceneManager.SetActiveScene(scene);
            try
            {
                floor=Material("VendettaFloor",new Color(.065f,.075f,.095f));
                cover=Material("VendettaCover",new Color(.23f,.27f,.32f));
                body=Material("VendettaBody",new Color(.55f,.14f,.24f));
                enemy=Material("VendettaTarget",new Color(.17f,.68f,.75f));
                blade=Material("VendettaBlade",new Color(1,.3f,.12f),true);
                var tune=Asset<VendettaTuning>("Vendetta_TuningPending");
                var common=Asset<PrototypeTuning>("Vendetta_Common");
                var world=new GameObject("Vendetta Simulation").AddComponent<SimulationWorld>();
                Cube("Ground",new Vector3(0,-.25f,0),new Vector3(32,.5f,32),floor,"NavigationSurface");
                Cube("North wall",new Vector3(0,1.5f,16),new Vector3(32,3,.5f),cover,"World");
                Cube("South wall",new Vector3(0,1.5f,-16),new Vector3(32,3,.5f),cover,"World");
                Cube("East wall",new Vector3(16,1.5f,0),new Vector3(.5f,3,32),cover,"World");
                Cube("West wall",new Vector3(-16,1.5f,0),new Vector3(.5f,3,32),cover,"World");
                Cube("Dash collision wall",new Vector3(5,1.5f,1),new Vector3(3,3,6),cover,"World");
                Cube("Flight platform",new Vector3(-7,1.5f,6),new Vector3(5,3,5),cover,"World");
                Cube("Platform stripe",new Vector3(-7,3.015f,6),new Vector3(4.8f,.03f,.12f),blade,"LocalViewModel");
                var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;
                sun.intensity=1.4f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(50,-30,0);
                RenderSettings.ambientLight=new Color(.6f,.65f,.72f);
                var player=Actor("Vendetta",101,0,new Vector3(0,0,-8),common,body);
                player.classId=ClassId.Vendetta;
                var controller=player.gameObject.AddComponent<VendettaController>();controller.tuning=tune;
                var sword=Cube("Palatine placeholder",Vector3.zero,new Vector3(.2f,1.8f,.12f),blade,"LocalViewModel");
                UnityEngine.Object.DestroyImmediate(sword.GetComponent<Collider>());
                sword.transform.SetParent(player.transform,false);sword.transform.localPosition=new Vector3(.6f,1.1f,.5f);
                sword.transform.localRotation=Quaternion.Euler(30,0,-15);
                var prefab=PrefabUtility.SaveAsPrefabAsset(player.gameObject,Root+"Prefabs/Actors/Vendetta.prefab");
                Actor("Spin target",102,1,new Vector3(0,0,0),common,enemy);
                Actor("Side target",103,1,new Vector3(-2,0,1),common,enemy);
                Actor("Platform target",104,1,new Vector3(-7,3,6),common,enemy);
                var camera=new GameObject("Vendetta local camera").AddComponent<Camera>();
                camera.tag="MainCamera";camera.fieldOfView=65;camera.nearClipPlane=.08f;
                camera.backgroundColor=new Color(.025f,.03f,.045f);camera.clearFlags=CameraClearFlags.SolidColor;
                camera.gameObject.AddComponent<AudioListener>();
                var rig=camera.gameObject.AddComponent<ThirdPersonRig>();rig.target=player.transform;
                rig.pivotOffset=tune.cameraPivot;rig.distance=tune.cameraDistance;rig.radius=tune.cameraRadius;
                var input=camera.gameObject.AddComponent<VendettaInputReader>();
                input.actor=player;input.tuning=tune;
                input.controls=AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root+"Input/DCGControls.inputactions");
                var lab=new GameObject("Vendetta Lab").AddComponent<VendettaLabController>();
                lab.world=world;lab.player=player;lab.controller=controller;lab.input=input;lab.cameraRig=rig;lab.heldSword=sword.transform;
                var thrown=Cube("Thrown sword",Vector3.zero,new Vector3(.2f,1.8f,.12f),blade,"LocalViewModel");
                UnityEngine.Object.DestroyImmediate(thrown.GetComponent<Collider>());lab.thrownSword=thrown.transform;thrown.SetActive(false);
                lab.spinRing=Line("Spin area",65,.045f);lab.tether=Line("Sword path",2,.018f);
                lab.gameObject.AddComponent<VendettaSmokeProbe>();
                var definition=Asset<ClassDefinition>("Vendetta");definition.classId=ClassId.Vendetta;
                definition.referenceGame="Overwatch Vendetta / user-specified Shift and E / third-person";
                definition.referenceStatus=ReferenceStatus.TuningPending;definition.playableInLab=true;
                definition.actorPrefab=prefab;definition.prototypeTuning=common;
                definition.pendingNotes="Separate VendettaLab. Only Shift dash/spin and E throw/fly. No primary attack, block, ultimate or passive.";
                EditorUtility.SetDirty(definition);
                EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
                if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath))
                    EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
                Debug.Log("VENDETTA_SETUP_OK");
            }
            finally
            {
                if(previous.IsValid())SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene,true);
            }
        }
        static T Asset<T>(string name) where T:ScriptableObject
        {
            string path=Root+"Data/Classes/"+name+".asset";
            var value=AssetDatabase.LoadAssetAtPath<T>(path);
            if(value==null){value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);}
            return value;
        }
        static Material Material(string name,Color color,bool unlit=false)
        {
            string path=Root+"Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);EditorUtility.SetDirty(material);return material;
        }
        static GameObject Cube(string name,Vector3 at,Vector3 size,Material material,string layer)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=at;
            go.transform.localScale=size;go.layer=LayerMask.NameToLayer(layer);go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static ActorSimulation Actor(string name,uint id,int team,Vector3 at,PrototypeTuning tuning,Material material)
        {
            var go=new GameObject(name);go.transform.position=at;go.layer=LayerMask.NameToLayer("CharacterBody");
            var cc=go.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.35f;cc.center=Vector3.up*.9f;cc.skinWidth=.025f;
            go.AddComponent<CharacterMotor>();var actor=go.AddComponent<ActorSimulation>();actor.actorNumber=id;actor.team=team;actor.tuning=tuning;
            var hit=new GameObject("Hurtbox");hit.transform.SetParent(go.transform,false);hit.layer=LayerMask.NameToLayer("Hurtbox");
            var hc=hit.AddComponent<CapsuleCollider>();hc.height=1.8f;hc.radius=.36f;hc.center=Vector3.up*.9f;hc.isTrigger=true;
            var mesh=GameObject.CreatePrimitive(PrimitiveType.Capsule);UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.transform.SetParent(go.transform,false);mesh.transform.localPosition=Vector3.up*.9f;mesh.transform.localScale=new Vector3(.7f,.85f,.7f);
            mesh.GetComponent<Renderer>().sharedMaterial=material;
            var view=go.AddComponent<ActorView>();view.actor=actor;view.body=mesh.GetComponent<Renderer>();
            return actor;
        }
        static LineRenderer Line(string name,int count,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.sharedMaterial=blade;line.positionCount=count;
            line.startWidth=line.endWidth=width;line.enabled=false;return line;
        }
        [MenuItem("DCG/Build Windows Vendetta Lab")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/VendettaLab");
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{ScenePath},locationPathName="Builds/VendettaLab/DCG-VendettaLab.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Vendetta build failed.");
            Debug.Log("VENDETTA_BUILD_OK");
        }
    }
}
