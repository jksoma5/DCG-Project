using System;
using System.Collections;
using System.IO;
using DCG.Core;
using DCG.Classes.Sniper;
using UnityEngine;
using UnityEngine.Rendering;
namespace DCG.Bootstrap
{
    public sealed class SniperSmokeProbe : MonoBehaviour
    {
        [Serializable] sealed class Report{public bool passed;public int sniperAmmo,pistolAmmo,shots;public string weapon;public float targetHealth,sniperTargetHealth;public int scopeLevel;}
        SniperLabController lab;uint sequence=10000;
        void Frame(ControlButtons pressed=0)
        {
            lab.world.Session.Submit(lab.player.Id,new PlayerCommand{
                Envelope=new CommandEnvelope{ActorId=lab.player.Id,Sequence=sequence++,CommandType=CommandType.DirectControl},
                Direct=new DirectControlFrame{PressedButtons=pressed}});
            Step(1);
        }
        void Slot(int id)
        {
            lab.world.Session.Submit(lab.player.Id,new PlayerCommand{
                Envelope=new CommandEnvelope{ActorId=lab.player.Id,Sequence=sequence++,CommandType=CommandType.Action},
                Action=new ActionCommand{ActionId=id}});
            Step(Mathf.CeilToInt(lab.sniper.tuning.drawSeconds/.02f)+2);
        }
        void Step(int count){for(int i=0;i<count;i++){lab.world.Step(.02f);Physics.SyncTransforms();}}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-sniperReportPath");if(at<0||at+1>=args.Length)yield break;
            Application.runInBackground=true;Application.targetFrameRate=60;
            string folder=args[at+1];Directory.CreateDirectory(folder);yield return null;
            lab=GetComponent<SniperLabController>();lab.input.enabled=false;lab.world.AutomaticTicks=false;Step(4);
            yield return new WaitForSeconds(.2f);Capture(Path.Combine(folder,"sniper-hip.png"));
            Frame(ControlButtons.Aim);yield return null;yield return null;
            int scopeLevel=lab.sniper.ScopeLevel;Capture(Path.Combine(folder,"sniper-scope.png"));
            Frame(ControlButtons.Fire);float sniperHealth=lab.world.Find(new ActorId(302)).Health.Current;yield return null;
            Slot(2);yield return new WaitForSeconds(.3f);Frame(ControlButtons.Fire);yield return new WaitForSeconds(.1f);
            Capture(Path.Combine(folder,"sniper-pistol.png"));
            Slot(3);yield return new WaitForSeconds(.3f);Capture(Path.Combine(folder,"sniper-knife.png"));
            var result=new Report{sniperAmmo=lab.sniper.AmmoFor(SniperWeapon.Sniper),pistolAmmo=lab.sniper.AmmoFor(SniperWeapon.Pistol),
                shots=lab.sniper.ShotsFired,weapon=lab.sniper.Weapon.ToString(),scopeLevel=scopeLevel,sniperTargetHealth=sniperHealth,targetHealth=lab.world.Find(new ActorId(302)).Health.Current};
            result.passed=result.sniperAmmo==4&&result.pistolAmmo==11&&result.shots==2&&result.weapon=="Knife"&&result.scopeLevel==1&&result.sniperTargetHealth==0;
            File.WriteAllText(Path.Combine(folder,"sniper-smoke.json"),JsonUtility.ToJson(result,true));Application.Quit(result.passed?0:1);
        }
        void Capture(string path)
        {
            var rt=new RenderTexture(1280,800,24);var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try{rt.Create();RenderPipeline.SubmitRenderRequest(lab.rig.viewCamera,new RenderPipeline.StandardRequest{destination=rt});
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{RenderTexture.active=old;rt.Release();Destroy(rt);Destroy(pixels);}
        }
    }
}
