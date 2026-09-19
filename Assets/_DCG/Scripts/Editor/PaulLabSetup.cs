using System;
using System.IO;
using System.Linq;
using DCG.Core;
using DCG.Gameplay;
using DCG.Gameplay.Fighting;
using DCG.Classes.Paul;
using DCG.Presentation;
using DCG.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace DCG.Editor
{
    public static class PaulLabSetup
    {
        const string Root="Assets/_DCG/";
        public const string ScenePath=Root+"Scenes/PaulLab.unity";
        static Material floor,cover,red,blue,accent;
        [MenuItem("DCG/Generate Paul Lab")]
        public static void Generate()
        {
            ProjectSetup.ValidateRestoration();Directory.CreateDirectory(Root+"Data/Fighting");AssetDatabase.Refresh();
            var old=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode&&string.IsNullOrEmpty(old.path)?NewSceneMode.Single:NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                floor=Material("PaulFloor",new Color(.12f,.12f,.14f));cover=Material("PaulCover",new Color(.25f,.28f,.32f));
                red=Material("PaulRed",new Color(.75f,.16f,.12f));blue=Material("PaulBlue",new Color(.13f,.45f,.8f));
                accent=Material("PaulHit",new Color(1,.7f,.1f),true);
                var moves=Asset<FightMoveSet>("Data/Fighting/Paul_Moves.asset");
                moves.moves=new[]{
                    Move("LP","1",0,8,2,12,HitLevel.High,8,1.9f,4,-1),
                    Move("RP","2",0,12,2,16,HitLevel.High,12,2,5,-5),
                    Move("LK","3",0,14,3,18,HitLevel.Mid,14,2.1f,5,-6),
                    Move("RK","4",0,13,2,18,HitLevel.High,16,2.2f,5,-7),
                    Move("LowKick","d+4",20,16,2,20,HitLevel.Low,12,2,2,-12),
                    Move("MidLP","df+1",20,12,2,17,HitLevel.Mid,12,2,3,-4),
                    Move("Uppercut","df+2",40,15,2,25,HitLevel.Mid,13,2.1f,3,-8),
                    Move("BackRP","b+2",20,15,2,18,HitLevel.Mid,18,2.2f,5,-7),
                    Move("ForwardRP","f+2",20,15,2,20,HitLevel.High,20,2.4f,6,-9),
                    Move("BothHands","1+2",30,18,3,22,HitLevel.Mid,22,2.2f,7,-10),
                    Move("BothFeet","3+4",30,20,3,24,HitLevel.Mid,24,2.4f,8,-12),
                    Move("Phoenix","d,df,f+2",100,14,3,22,HitLevel.Mid,30,2.6f,8,-12),
                    Move("LP_RP","1,2",50,8,2,16,HitLevel.High,10,2,5,-5,"LP"),
                    Move("LP_RP_RK","1,2,4",60,12,3,22,HitLevel.Mid,18,2.2f,6,-10,"LP_RP")
                };
                foreach(var move in moves.moves)ConfigureOutcomes(move);
                moves.counterStates=FighterTags.AttackStartup|FighterTags.AttackActive;
                EditorUtility.SetDirty(moves);
                var common=Asset<PrototypeTuning>("Data/Classes/Paul_Common.asset");common.maxHealth=200;EditorUtility.SetDirty(common);
                var world=new GameObject("Fighting world").AddComponent<SimulationWorld>();world.AutomaticTicks=false;
                Cube("Arena",new Vector3(0,-.25f,0),new Vector3(20,.5f,12),floor,"NavigationSurface");
                Cube("North",new Vector3(0,1,6),new Vector3(20,2,.4f),cover,"World");
                Cube("South",new Vector3(0,1,-6),new Vector3(20,2,.4f),cover,"World");
                Cube("East",new Vector3(10,1,0),new Vector3(.4f,2,12),cover,"World");
                Cube("West",new Vector3(-10,1,0),new Vector3(.4f,2,12),cover,"World");
                for(int x=-9;x<=9;x++)Cube("Grid",new Vector3(x,.01f,0),new Vector3(.015f,.015f,11.5f),cover,"LocalViewModel");
                var first=Actor("Paul prototype",401,new Vector3(-.85f,0,0),red,common,moves);
                var second=Actor("Practice dummy",402,new Vector3(.85f,0,0),blue,common,moves);
                second.GetComponent<ActorSimulation>().team=1;
                var prefab=PrefabUtility.SaveAsPrefabAsset(first.gameObject,Root+"Prefabs/Actors/Paul.prefab");
                var match=new GameObject("Fighter match 60Hz").AddComponent<FighterMatch>();match.world=world;match.first=first;match.second=second;
                var camera=new GameObject("South side camera").AddComponent<Camera>();camera.tag="MainCamera";camera.nearClipPlane=.05f;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.045f,.075f);camera.gameObject.AddComponent<AudioListener>();
                var rig=camera.gameObject.AddComponent<FightCameraRig>();rig.first=first.transform;rig.second=second.transform;rig.viewCamera=camera;
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.7f,.7f,.75f);
                var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.4f;sun.transform.rotation=Quaternion.Euler(50,-30,0);
                var lab=new GameObject("Paul Lab").AddComponent<PaulLabController>();lab.match=match;
                var input=lab.gameObject.AddComponent<FighterInputReader>();input.controls=AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root+"Input/DCGControls.inputactions");lab.input=input;
                lab.gameObject.AddComponent<PaulSmokeProbe>();
                var definition=Asset<ClassDefinition>("Data/Classes/Paul.asset");definition.classId=ClassId.Paul;definition.actorPrefab=prefab;
                definition.prototypeTuning=common;definition.playableInLab=true;definition.referenceStatus=ReferenceStatus.TuningPending;
                definition.referenceGame="Tekken 7 Paul / prototype input and frame system";
                definition.pendingNotes="Prototype reactions: launch, air hits, knockdown. Pending original tuning, Sway, throws and Rage.";
                EditorUtility.SetDirty(definition);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
                if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
                Debug.Log("PAUL_SETUP_OK");
            }
            finally{if(old.IsValid())SceneManager.SetActiveScene(old);EditorSceneManager.CloseScene(scene,true);}
        }
        static T Asset<T>(string path) where T:ScriptableObject
        {
            var value=AssetDatabase.LoadAssetAtPath<T>(Root+path);if(value==null){value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,Root+path);}return value;
        }
        static MoveData Move(string id,string command,int priority,int startup,int active,int recovery,HitLevel level,float damage,float range,int hit,int block,string follows="")
        {
            var move=Asset<MoveData>("Data/Fighting/Paul_"+id+".asset");move.moveId=id;move.command=command;move.priority=priority;
            move.startup=startup;move.active=active;move.recovery=recovery;move.level=level;move.damage=damage;move.range=range;
            move.onHit=hit;move.onBlock=block;move.onCounter=hit+3;move.follows=follows;EditorUtility.SetDirty(move);return move;
        }
        static void ConfigureOutcomes(MoveData move)
        {
            move.hitOutcome=new HitOutcome{advantageFrames=move.onHit,launchHeight=0,horizontalSpeed=0};
            move.counterOutcome=new HitOutcome{advantageFrames=move.onCounter,launchHeight=0,horizontalSpeed=0};
            move.hasCounterOutcome=true;
            move.blockOutcome=new BlockOutcome{advantageFrames=move.onBlock};
            move.hasAirborneOutcome=true;
            move.airborneOutcome=new HitOutcome{reaction=HitReaction.Launch,launchHeight=.8f,horizontalSpeed=.6f,juggleCost=1};
            move.hasGroundOutcome=false;move.hasCrouchingOutcome=false;
            if(move.moveId=="LowKick")
            {
                move.hitOutcome.reaction=HitReaction.CrouchStagger;
                move.counterOutcome.reaction=HitReaction.Knockdown;
            }
            if(move.moveId=="BackRP")
            {
                move.hitOutcome.reaction=move.counterOutcome.reaction=HitReaction.CrouchStagger;
                move.blockOutcome.forceCrouch=true;
            }
            if(move.moveId=="RK")
                move.counterOutcome=new HitOutcome{reaction=HitReaction.CounterLaunch,launchHeight=1.8f,horizontalSpeed=.6f};
            if(move.moveId=="Uppercut")
            {
                move.hitOutcome=new HitOutcome{reaction=HitReaction.Launch,launchHeight=1.8f,horizontalSpeed=.3f};
                move.counterOutcome=new HitOutcome{reaction=HitReaction.Launch,launchHeight=1.8f,horizontalSpeed=.3f};
                move.hasCrouchingOutcome=true;move.crouchingOutcome=new HitOutcome{reaction=HitReaction.CrouchStagger,advantageFrames=3};
            }
            if(move.moveId=="Phoenix")
            {
                move.hitOutcome=new HitOutcome{reaction=HitReaction.BlowAway,launchHeight=.6f,horizontalSpeed=5};
                move.counterOutcome=new HitOutcome{reaction=HitReaction.BlowAway,launchHeight=.6f,horizontalSpeed=5};
                move.airborneOutcome=new HitOutcome{reaction=HitReaction.BlowAway,launchHeight=.25f,horizontalSpeed=5,juggleCost=2};
            }
            EditorUtility.SetDirty(move);
        }
        static Material Material(string name,Color color,bool unlit=false)
        {
            string path=Root+"Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);return m;
        }
        static GameObject Cube(string name,Vector3 position,Vector3 scale,Material material,string layer)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;go.layer=LayerMask.NameToLayer(layer);return go;
        }
        static Transform Part(Transform parent,string name,Vector3 scale,Material material)
        {
            var p=Cube(name,Vector3.zero,scale,material,"LocalViewModel");UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());p.transform.SetParent(parent,false);return p.transform;
        }
        static FighterAgent Actor(string name,uint id,Vector3 position,Material material,PrototypeTuning common,FightMoveSet moves)
        {
            var go=new GameObject(name);go.transform.position=position;go.layer=LayerMask.NameToLayer("CharacterBody");
            var cc=go.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.3f;cc.skinWidth=.025f;
            go.AddComponent<CharacterMotor>();var actor=go.AddComponent<ActorSimulation>();actor.actorNumber=id;actor.classId=ClassId.Paul;actor.tuning=common;
            var fighter=go.AddComponent<FighterAgent>();fighter.moveSet=moves;
            var view=go.AddComponent<FighterView>();view.fighter=fighter;
            view.body=Part(go.transform,"Body",new Vector3(.65f,1.8f,.55f),material);
            view.leftHand=Part(go.transform,"Left fist",Vector3.one*.23f,material);view.rightHand=Part(go.transform,"Right fist",Vector3.one*.23f,material);
            view.leftFoot=Part(go.transform,"Left foot",new Vector3(.2f,.2f,.35f),material);view.rightFoot=Part(go.transform,"Right foot",new Vector3(.2f,.2f,.35f),material);
            view.hitbox=Part(go.transform,"Active hitbox",Vector3.one,accent).GetComponent<Renderer>();view.hitbox.enabled=false;
            return fighter;
        }
        [MenuItem("DCG/Build Windows Paul Lab")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/PaulLab");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/PaulLab/DCG-PaulLab.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Paul build failed.");Debug.Log("PAUL_BUILD_OK");
        }
    }
}
