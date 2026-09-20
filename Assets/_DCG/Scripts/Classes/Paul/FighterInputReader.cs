using DCG.Core;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DCG.Classes.Paul
{
    public sealed class FighterInputReader : MonoBehaviour
    {
        public InputActionAsset controls;
        // The reader normally switches the Input System to manual event processing for as long as it
        // lives. When something bigger owns that decision for a whole session - the control hub does,
        // because a fight now runs beside the other classes - it sets this to false and the reader
        // leaves the global mode alone.
        public bool ownsUpdateMode=true;
        public FightInputTimeline Timeline { get; }=new FightInputTimeline();
        InputActionAsset runtime;
        InputActionMap map;
        InputSettings.UpdateMode previous;
        bool bound;
        public bool ActiveFocus { get; private set; }=true;
        void OnEnable()
        {
            // No asset, nothing to bind - and in particular the global update mode is left alone.
            if(controls==null)return;
            previous=InputSystem.settings.updateMode;
            if(ownsUpdateMode)InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            runtime=Instantiate(controls);map=runtime.FindActionMap("Fighter",true);
            string[] names={"Left","Right","Up","Down","LP","RP","LK","RK","BothHands","BothFeet"};
            for(int i=0;i<names.Length;i++)
            {
                int key=i;
                map[names[i]].performed+=ctx=>Timeline.Enqueue(ctx.time,key,true);
                map[names[i]].canceled+=ctx=>Timeline.Enqueue(ctx.time,key,false);
            }
            map.Enable();bound=true;
        }
        public void Poll(){InputSystem.Update();}
        public FightInputFrame Sample(double time)=>ActiveFocus?Timeline.Sample(time):new FightInputFrame{Direction=5};
        public bool Pressed(string action)=>map!=null&&map[action].WasPressedThisFrame();
        void OnApplicationFocus(bool focus){ActiveFocus=focus;if(!focus)Timeline.Clear();}
        void OnDisable()
        {
            if(!bound)return;
            if(runtime!=null){runtime.Disable();Destroy(runtime);}
            map=null;Timeline.Clear();
            if(ownsUpdateMode)InputSystem.settings.updateMode=previous;
            bound=false;
        }
    }
}
