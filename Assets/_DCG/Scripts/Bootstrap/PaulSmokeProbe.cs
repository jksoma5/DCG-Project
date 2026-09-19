using System;
using System.Collections;
using System.IO;
using DCG.Core;
using DCG.Gameplay.Fighting;
using UnityEngine;
using UnityEngine.Rendering;
namespace DCG.Bootstrap
{
    public sealed class PaulSmokeProbe : MonoBehaviour
    {
        [Serializable] sealed class Report{public bool passed,launched,down,recovered;public string move,result;public float damage,blockedDamage,launchY,blowDistance;public int frame,advantage;}
        PaulLabController lab;
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-paulReportPath");if(at<0||at+1>=args.Length)yield break;
            Application.runInBackground=true;yield return null;
            lab=GetComponent<PaulLabController>();lab.automatic=false;lab.input.enabled=false;
            string path=args[at+1];Directory.CreateDirectory(path);
            for(int i=0;i<4;i++)Tick();
            Tick(3,FightButtons.RP);for(int i=0;i<29;i++)Tick();
            bool launched=lab.match.second.State.IsAirborne;float launchY=lab.match.second.transform.position.y;
            yield return null;Capture(Path.Combine(path,"paul-uppercut-airborne.png"));
            for(int i=0;i<90;i++)Tick();bool recovered=!lab.match.second.State.Busy;
            ResetPositions();Tick();
            float initial=lab.match.second.Actor.Health.Current,startX=lab.match.second.transform.position.x;
            Tick(2);Tick(3);Tick(6,FightButtons.RP);Tick();Tick();
            string move=lab.match.first.LastCommand;
            for(int i=0;i<20;i++)Tick();
            yield return null;Capture(Path.Combine(path,"paul-phoenix.png"));
            for(int i=0;i<20;i++)Tick();
            bool down=lab.match.second.State.IsDown;float blowDistance=lab.match.second.transform.position.x-startX;
            yield return null;Capture(Path.Combine(path,"paul-down.png"));
            for(int i=0;i<90;i++)Tick();
            float after=lab.match.second.Actor.Health.Current;
            ResetPositions();Tick();lab.dummyMode=FightDummyMode.AllGuard;
            Tick(5,FightButtons.LP);for(int i=0;i<40;i++)Tick();
            yield return null;Capture(Path.Combine(path,"paul-guard.png"));
            var report=new Report{move=move,result=lab.match.first.LastResult,damage=initial-after,
                blockedDamage=after-lab.match.second.Actor.Health.Current,frame=lab.match.Frame,advantage=lab.match.first.LastAdvantage,
                launched=launched,launchY=launchY,down=down,recovered=recovered,blowDistance=blowDistance};
            report.passed=launched&&launchY>.3f&&down&&recovered&&blowDistance>.5f&&move=="d,df,f+2"&&report.damage==30&&report.blockedDamage==0&&report.result=="Blocked";
            File.WriteAllText(Path.Combine(path,"paul-smoke.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);
        }
        void ResetPositions()
        {
            foreach(var fighter in new[]{lab.match.first,lab.match.second})
            {
                var cc=fighter.GetComponent<CharacterController>();cc.enabled=false;
                fighter.transform.position=new Vector3(fighter==lab.match.first?-.85f:.85f,0,0);cc.enabled=true;
                fighter.State.Reset();fighter.Actor.Motor.ClearVerticalVelocity();
            }
            Physics.SyncTransforms();
        }
        void Tick(int direction=5,FightButtons buttons=0)=>lab.Tick(new FightInputFrame{Direction=direction,Pressed=buttons,Held=buttons});
        void Capture(string path)
        {
            var rt=new RenderTexture(1280,800,24);var tex=new Texture2D(1280,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try{rt.Create();RenderPipeline.SubmitRenderRequest(Camera.main,new RenderPipeline.StandardRequest{destination=rt});
                RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,800),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
            finally{RenderTexture.active=old;rt.Release();Destroy(rt);Destroy(tex);}
        }
    }
}
