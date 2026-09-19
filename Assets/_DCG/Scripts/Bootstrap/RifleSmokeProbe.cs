using System;
using System.Collections;
using System.IO;
using DCG.Core;
using DCG.Classes.Rifle;
using UnityEngine;
using UnityEngine.Rendering;
namespace DCG.Bootstrap
{
    public sealed class RifleSmokeProbe : MonoBehaviour
    {
        [Serializable] sealed class Report { public bool passed; public int shots, ammo; public float targetHealth; public string aim; }
        uint sequence=10000;
        RifleLabController lab;
        void Input(ControlButtons held,ControlButtons pressed=0)
        {
            lab.world.Session.Submit(lab.player.Id,new PlayerCommand{
                Envelope=new CommandEnvelope{ActorId=lab.player.Id,Sequence=sequence++,CommandType=CommandType.DirectControl},
                Direct=new DirectControlFrame{HeldButtons=held,PressedButtons=pressed,AimTarget=lab.world.Find(new ActorId(202)).AimPoint}
            });
        }
        IEnumerator Start()
        {
            string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-rifleReportPath");
            if(at<0||at+1>=args.Length)yield break;
            Application.runInBackground=true;Application.targetFrameRate=60;
            string folder=args[at+1];Directory.CreateDirectory(folder);yield return null;
            lab=GetComponent<RifleLabController>();lab.input.enabled=false;
            yield return new WaitForSeconds(.2f);
            Capture(Path.Combine(folder,"rifle-shoulder.png"));
            Input(ControlButtons.Aim);yield return new WaitForSeconds(.15f);
            Capture(Path.Combine(folder,"rifle-aim.png"));
            Input(ControlButtons.Ads);yield return new WaitForSeconds(.15f);
            Capture(Path.Combine(folder,"rifle-ads.png"));
            for(int i=0;i<25;i++){Input(ControlButtons.Fire|ControlButtons.Ads);yield return new WaitForSeconds(.02f);}
            Input(0,ControlButtons.Reload);
            yield return new WaitForSeconds(lab.rifle.tuning.reloadSeconds+.2f);
            float health=lab.world.Find(new ActorId(202)).Health.Current;
            var report=new Report{shots=lab.rifle.ShotsFired,ammo=lab.rifle.Ammo,targetHealth=health,aim=lab.rifle.AimMode.ToString(),
                passed=health<100&&lab.rifle.ShotsFired>=3&&lab.rifle.Ammo==lab.rifle.tuning.magazineSize};
            File.WriteAllText(Path.Combine(folder,"rifle-smoke.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.passed?0:1);
        }
        void Capture(string path)
        {
            var rt=new RenderTexture(1280,800,24);var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                rt.Create();RenderPipeline.SubmitRenderRequest(lab.rig.viewCamera,new RenderPipeline.StandardRequest{destination=rt});
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(pixels);}
        }
    }
}
