using System;
using System.Collections;
using System.IO;
using DCG.Core;
using DCG.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCG.Bootstrap
{
    // Explicit opt-in automated player probe. Not run during normal play.
    public sealed class LabSmokeProbe : MonoBehaviour
    {
        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "-dcgReportPath");
            if (flag < 0 || flag + 1 >= args.Length) yield break;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            string folder = args[flag + 1]; Directory.CreateDirectory(folder);
            yield return null;
            var lab = GetComponent<LabController>();
            for (int i = 0; i < 45; i++) yield return null;
            CaptureCamera(lab.input.worldCamera, Path.Combine(folder, "control-lab-start.png"));
            Vector3 initial = lab.player.transform.position;
            lab.world.Session.Submit(lab.player.Id, new PlayerCommand {
                Envelope = new CommandEnvelope { ActorId = lab.player.Id, Sequence = 1, CommandType = CommandType.MoveTo },
                Move = new MoveToCommand { Destination = new Vector3(-9,0,-1) }
            });
            yield return new WaitForSeconds(2);
            float distance = Vector3.Distance(initial, lab.player.transform.position);
            lab.world.Session.Submit(lab.player.Id, new PlayerCommand {
                Envelope = new CommandEnvelope { ActorId = lab.player.Id, Sequence = 2, CommandType = CommandType.Target },
                Target = new TargetCommand { TargetActorId = new ActorId(3), OrderType = OrderType.AttackTarget }
            });
            yield return new WaitForSeconds(5);
            var victim = lab.world.Find(new ActorId(3));
            bool passed = distance > 2 && victim.Health.Current < victim.Health.Maximum;
            CaptureCamera(lab.input.worldCamera, Path.Combine(folder, "control-lab-combat.png"));
            File.WriteAllText(Path.Combine(folder, "player-smoke.json"),
                "{\"passed\":" + (passed ? "true" : "false") + ",\"movedDistance\":" +
                distance.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"targetHealth\":" +
                victim.Health.Current.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
            for (int i = 0; i < 15; i++) yield return null;
            Application.Quit(passed ? 0 : 1);
        }
        static void CaptureCamera(Camera camera, string path)
        {
            var texture = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                texture.Create();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = texture });
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                texture.Release(); Destroy(texture); Destroy(pixels);
            }
        }
    }
}
