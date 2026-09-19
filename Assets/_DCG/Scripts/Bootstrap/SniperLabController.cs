using DCG.Gameplay;
using DCG.Classes.Sniper;
using DCG.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DCG.Bootstrap
{
    public sealed class SniperLabController : MonoBehaviour
    {
        public SimulationWorld world;
        public ActorSimulation player;
        public SniperController sniper;
        public SniperInputReader input;
        public FirstPersonScopeRig rig;
        public Transform[] weapons;
        public LineRenderer tracer;
        Vector3[] positions;
        float tracerUntil;
        void Start()
        {
            foreach(var actor in FindObjectsByType<ActorSimulation>())world.Register(actor);
            input.ResetRequested=()=>SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            sniper.Fired+=ShowTracer;
            positions=new Vector3[weapons.Length];
            for(int i=0;i<weapons.Length;i++)positions[i]=weapons[i].localPosition;
        }
        void ShowTracer(Vector3 a,Vector3 b)
        {
            tracer.SetPosition(0,a);tracer.SetPosition(1,b);tracerUntil=Time.time+.06f;
        }
        void LateUpdate()
        {
            if(!player.Initialized)return;
            var tune=sniper.tuning;
            float fov=sniper.ScopeLevel==2?tune.deepScopeFov:sniper.ScopeLevel==1?tune.scopeFov:tune.normalFov;
            rig.Place(sniper.Eye,sniper.AimRotation,fov,sniper.ScopeLevel>0);
            for(int i=0;i<weapons.Length;i++)
            {
                bool active=i+1==(int)sniper.Weapon&&sniper.ScopeLevel==0;
                weapons[i].gameObject.SetActive(active);
                float draw=tune.drawSeconds>0?sniper.DrawRemaining/tune.drawSeconds:0;
                weapons[i].localPosition=positions[i]+new Vector3(0,-draw*.25f,-sniper.Kick*.07f);
                weapons[i].localRotation=Quaternion.Euler(-sniper.Kick*7,0,i==2?-sniper.Kick*40:0);
            }
            tracer.enabled=Time.time<tracerUntil;
        }
        void OnDestroy(){if(sniper!=null)sniper.Fired-=ShowTracer;}
        void OnGUI()
        {
            GUI.Box(new Rect(20,20,425,145),"FIRST PERSON / SNIPER LOADOUT LAB");
            GUI.Label(new Rect(34,49,395,115),"1 Sniper | 2 Pistol | 3 Knife\nWASD Move | Shift Walk | Ctrl Crouch | Space Jump\nLMB Fire / Slash | RMB Scope / Stab | R Reload\nEsc Release | Click Capture | F5 Reset\nPrototype movement and weapon tuning");
            GUI.Box(new Rect(20,Screen.height-102,360,82),"");
            GUI.Label(new Rect(34,Screen.height-92,340,75),
                sniper.Weapon+"   "+(sniper.Weapon==SniperWeapon.Knife?"MELEE":sniper.Ammo+" / "+sniper.Reserve)+
                "\n"+(sniper.Crouched?"CROUCH":"STAND")+"  Scope "+sniper.ScopeLevel+
                (sniper.ReloadRemaining>0?"  RELOAD "+sniper.ReloadRemaining.ToString("0.0"):
                sniper.DrawRemaining>0?"  EQUIP":sniper.RecoveryRemaining>0?"  RECOVERY":"  READY"));
            if(sniper.ScopeLevel==0&&sniper.Weapon!=SniperWeapon.Sniper)
                GUI.Label(new Rect(Screen.width*.5f-5,Screen.height*.5f-12,20,25),"+");
            if(sniper.HitMarkerRemaining>0)
            {
                GUI.color=Color.red;GUI.Label(new Rect(Screen.width*.5f-5,Screen.height*.5f-12,20,25),"X");GUI.color=Color.white;
            }
            if(!player.Initialized||sniper.ScopeLevel>0)return;
            foreach(var actor in world.Actors)
            {
                if(actor==player)continue;
                var p=rig.viewCamera.WorldToScreenPoint(actor.AimPoint+Vector3.up);
                if(p.z>0)GUI.Label(new Rect(p.x-30,Screen.height-p.y,100,25),actor.Health.Current.ToString("0")+" HP");
            }
        }
    }
}
