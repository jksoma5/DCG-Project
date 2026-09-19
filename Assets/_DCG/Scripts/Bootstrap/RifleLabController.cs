using DCG.Gameplay;
using DCG.Classes.Rifle;
using DCG.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DCG.Bootstrap
{
    public sealed class RifleLabController : MonoBehaviour
    {
        public SimulationWorld world;
        public ActorSimulation player;
        public RifleController rifle;
        public RifleInputReader input;
        public ShoulderCameraRig rig;
        public Transform model, gun, adsGun;
        public LineRenderer tracer;
        float tracerUntil;
        void Start()
        {
            foreach (var actor in FindObjectsByType<ActorSimulation>()) world.Register(actor);
            input.ResetRequested = () => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            rifle.Tracer += ShowTracer;
        }
        void ShowTracer(Vector3 start, Vector3 end)
        {
            tracer.SetPosition(0, start); tracer.SetPosition(1, end); tracerUntil = Time.time + .06f;
        }
        void LateUpdate()
        {
            if (!player.Initialized) return;
            var tune = rifle.tuning;
            bool ads = rifle.AimMode == RifleAimMode.Ads;
            bool aim = rifle.AimMode == RifleAimMode.Shoulder;
            Quaternion rotation = rifle.AimRotation * Quaternion.Euler(input.FreeLook.y, input.FreeLook.x, 0);
            rig.Place(rifle.Eye, rotation, ads ? 0 : tune.shoulderOffset * rifle.Shoulder,
                ads ? 0 : aim ? tune.aimDistance : tune.hipDistance,
                ads ? tune.adsFov : aim ? tune.shoulderFov : tune.hipFov, ads ? -rifle.Lean * 8 : 0, Time.deltaTime);
            model.gameObject.SetActive(!ads); gun.gameObject.SetActive(!ads); adsGun.gameObject.SetActive(ads);
            model.localPosition = new Vector3(rifle.Lean * .22f, rifle.Height * .5f, 0);
            model.localScale = rifle.Stance == RifleStance.Prone ? new Vector3(.65f,.3f,1.5f) : new Vector3(.7f,rifle.Height*.5f,.7f);
            model.localRotation = Quaternion.Euler(0,0,-rifle.Lean*12);
            gun.SetPositionAndRotation(rifle.Muzzle, rifle.AimRotation);
            tracer.enabled = Time.time < tracerUntil;
        }
        void OnDestroy() { if (rifle != null) rifle.Tracer -= ShowTracer; }
        void OnGUI()
        {
            GUI.Box(new Rect(20,20,410,174), "M416 / SHOULDER + ADS LAB");
            GUI.Label(new Rect(34,48,390,140),
                "WASD Move | Shift Sprint | Ctrl Walk | Space Jump\nC Crouch | Z Prone | Q / E Lean + shoulder\nLMB Fire | RMB hold Shoulder / tap ADS\nR Reload | B Single / Auto | Alt Free look\nEsc Release | Click Capture | F5 Reset\nPrototype tuning / separate scene");
            GUI.Box(new Rect(20,Screen.height-95,350,75), "");
            GUI.Label(new Rect(34,Screen.height-84,325,64),
                rifle.Ammo + " / " + rifle.Reserve + "   " + rifle.FireMode + "\n" +
                rifle.Stance + " / " + rifle.AimMode + (rifle.Sprinting ? " / SPRINT" : "") +
                (rifle.ReloadRemaining > 0 ? "\nRELOADING " + rifle.ReloadRemaining.ToString("0.0") : ""));
            float x=Screen.width*.5f, y=Screen.height*.5f;
            GUI.color = rifle.HitMarkerRemaining > 0 ? Color.red : Color.white;
            GUI.Label(new Rect(x-5,y-12,25,25), rifle.HitMarkerRemaining > 0 ? "X" : rifle.AimMode==RifleAimMode.Ads ? "." : "+");
            GUI.color = Color.white;
            if (!player.Initialized) return;
            foreach (var actor in world.Actors)
            {
                if(actor==player)continue;
                var p=rig.viewCamera.WorldToScreenPoint(actor.AimPoint+Vector3.up);
                if(p.z>0)GUI.Label(new Rect(p.x-35,Screen.height-p.y,100,25),actor.Health.Current.ToString("0")+" HP");
            }
        }
    }
}
