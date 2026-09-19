using DCG.Core;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DCG.Classes.Paul
{
    public sealed class FighterInputReader : MonoBehaviour
    {
        public InputActionAsset controls;
        public FightInputTimeline Timeline { get; }=new FightInputTimeline();
        InputActionAsset runtime;
        InputActionMap map;
        InputSettings.UpdateMode previous;
        public bool ActiveFocus { get; private set; }=true;
        void OnEnable()
        {
            previous=InputSystem.settings.updateMode;InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            runtime=Instantiate(controls);map=runtime.FindActionMap("Fighter",true);
            string[] names={"Left","Right","Up","Down","LP","RP","LK","RK","BothHands","BothFeet"};
            for(int i=0;i<names.Length;i++)
            {
                int key=i;
                map[names[i]].performed+=ctx=>Timeline.Enqueue(ctx.time,key,true);
                map[names[i]].canceled+=ctx=>Timeline.Enqueue(ctx.time,key,false);
            }
            map.Enable();
        }
        public void Poll(){InputSystem.Update();}
        public FightInputFrame Sample(double time)=>ActiveFocus?Timeline.Sample(time):new FightInputFrame{Direction=5};
        public bool Pressed(string action)=>map!=null&&map[action].WasPressedThisFrame();
        void OnApplicationFocus(bool focus){ActiveFocus=focus;if(!focus)Timeline.Clear();}
        void OnDisable()
        {
            if(runtime!=null){runtime.Disable();Destroy(runtime);}
            Timeline.Clear();InputSystem.settings.updateMode=previous;
        }
    }
}
