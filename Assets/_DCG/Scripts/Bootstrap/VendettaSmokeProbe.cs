using System;
using System.Collections;
using System.IO;
using DCG.Core;
using DCG.Classes.Vendetta;
using UnityEngine;
using UnityEngine.Rendering;
namespace DCG.Bootstrap
{
    public sealed class VendettaSmokeProbe : MonoBehaviour
    {
        [Serializable] sealed class Report { public bool passed; public float targetHealth; public float flightError; public string phase; }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-vendettaReportPath");
            if(at<0||at+1>=args.Length)yield break;
            Application.runInBackground=true;Application.targetFrameRate=60;
            string folder=args[at+1];Directory.CreateDirectory(folder);
            yield return null;
            var lab=GetComponent<VendettaLabController>();lab.input.enabled=false;
            for(int i=0;i<20;i++)yield return null;
            Capture(lab.cameraRig.GetComponent<Camera>(),Path.Combine(folder,"vendetta-start.png"));
            uint seq=100;
            var aim=new PlayerCommand{Envelope=new CommandEnvelope{ActorId=lab.player.Id,Sequence=seq++,CommandType=CommandType.DirectControl},
                Direct=new DirectControlFrame{AimYawPitch=Vector2.zero}};
            lab.world.Session.Submit(lab.player.Id,aim);
            var shift=new PlayerCommand{Envelope=new CommandEnvelope{ActorId=lab.player.Id,Sequence=seq++,CommandType=CommandType.Action},
                Action=new ActionCommand{ActionId=VendettaController.ShiftAction}};
            lab.world.Session.Submit(lab.player.Id,shift);
            yield return new WaitForSeconds(.32f);
            Capture(lab.cameraRig.GetComponent<Camera>(),Path.Combine(folder,"vendetta-spin.png"));
            yield return new WaitForSeconds(.5f);
            float hp=lab.world.Find(new ActorId(102)).Health.Current;
            var e=new PlayerCommand{Envelope=new CommandEnvelope{ActorId=lab.player.Id,Sequence=seq++,CommandType=CommandType.Action},
                Action=new ActionCommand{ActionId=VendettaController.EAction,TargetPoint=lab.player.AimPoint+new Vector3(-.6f,.8f,1).normalized*10}};
            lab.world.Session.Submit(lab.player.Id,e);
            yield return new WaitForSeconds(.15f);
            Capture(lab.cameraRig.GetComponent<Camera>(),Path.Combine(folder,"vendetta-throw.png"));
            yield return new WaitForSeconds(.25f);
            Capture(lab.cameraRig.GetComponent<Camera>(),Path.Combine(folder,"vendetta-flight.png"));
            float deadline=Time.realtimeSinceStartup+5;
            while(lab.controller.Phase!=VendettaPhase.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
            Vector3 delta=lab.player.transform.position-lab.controller.FlightDestination;delta.y=0;
            var result=new Report{targetHealth=hp,flightError=delta.magnitude,phase=lab.controller.Phase.ToString(),
                passed=hp<100&&lab.controller.Phase==VendettaPhase.Ready&&delta.magnitude<.3f};
            File.WriteAllText(Path.Combine(folder,"vendetta-smoke.json"),JsonUtility.ToJson(result,true));
            Application.Quit(result.passed?0:1);
        }
        static void Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1280,800,24,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                rt.Create();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally {RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(pixels);}
        }
    }
}
