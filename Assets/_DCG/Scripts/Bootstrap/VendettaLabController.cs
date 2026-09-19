using DCG.Core;
using DCG.Gameplay;
using DCG.Classes.Vendetta;
using DCG.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DCG.Bootstrap
{
    public sealed class VendettaLabController : MonoBehaviour
    {
        public SimulationWorld world;
        public ActorSimulation player;
        public VendettaController controller;
        public VendettaInputReader input;
        public ThirdPersonRig cameraRig;
        public Transform heldSword, thrownSword, model;
        public LineRenderer spinRing, tether;
        Vector3 swordLocal;
        Quaternion swordRotation;
        void Start()
        {
            foreach (var actor in FindObjectsByType<ActorSimulation>()) world.Register(actor);
            input.ResetRequested = () => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            swordLocal = heldSword.localPosition; swordRotation = heldSword.localRotation;
        }
        void Update()
        {
            cameraRig.aim = input.Aim;
            bool airborneSword = controller.Phase == VendettaPhase.SwordThrow || controller.Phase == VendettaPhase.Flight;
            heldSword.gameObject.SetActive(!airborneSword);
            thrownSword.gameObject.SetActive(airborneSword);
            if (airborneSword)
            {
                thrownSword.position = controller.SwordPosition;
                var direction = controller.SwordDestination - player.AimPoint;
                if (direction.sqrMagnitude > .001f) thrownSword.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90,0,0);
            }
            bool spinning = controller.Phase == VendettaPhase.Spin;
            heldSword.localPosition = spinning ? Quaternion.Euler(0,controller.PhaseProgress*360,0) * new Vector3(0,1,1.2f) : swordLocal;
            heldSword.localRotation = spinning ? Quaternion.Euler(90,controller.PhaseProgress*360,0) : swordRotation;
            spinRing.enabled = spinning;
            if (spinning)
            {
                for (int i=0;i<spinRing.positionCount;i++)
                {
                    float angle=i*2*Mathf.PI/(spinRing.positionCount-1);
                    spinRing.SetPosition(i,player.transform.position+new Vector3(Mathf.Cos(angle),.12f,Mathf.Sin(angle))*controller.tuning.spinRadius);
                }
            }
            tether.enabled = airborneSword;
            if (airborneSword) { tether.SetPosition(0,player.AimPoint); tether.SetPosition(1,controller.SwordPosition); }
        }
        void OnGUI()
        {
            GUI.Box(new Rect(20,20,355,132),"VENDETTA / SEPARATE SKILL LAB");
            GUI.Label(new Rect(35,50,330,90),"WASD  Move    Mouse  Look    Space  Jump\nShift  Dash -> spinning slash\nE  Throw sword -> fly to sword -> stop\nEsc  Release cursor    Click  Capture    F5  Reset");
            GUI.Box(new Rect(20,Screen.height-100,355,80),"");
            GUI.Label(new Rect(35,Screen.height-90,330,65),
                "SHIFT  " + controller.ShiftRemaining.ToString("0.0") + "s     E  " + controller.ERemaining.ToString("0.0") +
                "s\nState: " + controller.Phase + "\nTUNING PENDING / no primary, block or ultimate");
            GUI.Label(new Rect(Screen.width/2f-5,Screen.height/2f-12,20,25),"+");
            if (!player.Initialized) return;
            Camera camera = cameraRig.GetComponent<Camera>();
            foreach (var actor in world.Actors)
            {
                if (actor == player) continue;
                Vector3 p=camera.WorldToScreenPoint(actor.AimPoint+Vector3.up);
                if(p.z>0) GUI.Label(new Rect(p.x-35,Screen.height-p.y,100,25),actor.Health.Current.ToString("0")+" HP");
            }
        }
    }
}
