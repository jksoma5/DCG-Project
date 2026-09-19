using System;
using DCG.Core;
using DCG.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DCG.Classes.Sniper
{
    public sealed class SniperInputReader : MonoBehaviour
    {
        public ActorSimulation actor;
        public SniperController sniper;
        public InputActionAsset controls;
        public Action ResetRequested;
        public Vector2 Aim { get; private set; }
        public bool Captured { get; private set; }
        InputActionAsset runtime;InputActionMap map;uint sequence;
        void OnEnable()
        {
            if(controls==null)return;
            runtime=Instantiate(controls);map=runtime.FindActionMap("Sniper",true);map.Enable();Capture();
        }
        void Capture(){Captured=true;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        CommandEnvelope Envelope(CommandType type)=>new CommandEnvelope{ActorId=actor.Id,Sequence=++sequence,ClientTick=actor.World.Tick,CommandType=type};
        void Update()
        {
            if(map==null||actor==null||!actor.Initialized)return;
            if(map["Reset"].WasPressedThisFrame()){ResetRequested?.Invoke();return;}
            if(map["ReleaseCursor"].WasPressedThisFrame()){Release();return;}
            if(!Captured){if(map["Fire"].WasPressedThisFrame())Capture();return;}
            if(!actor.Health.IsAlive)return;
            float sensitivity=sniper.ScopeLevel>0?sniper.tuning.scopeSensitivity/(sniper.ScopeLevel==2?2:1):sniper.tuning.mouseSensitivity;
            Vector2 delta=map["Look"].ReadValue<Vector2>()*sensitivity;
            Aim=new Vector2(Mathf.Repeat(Aim.x+delta.x,360),Mathf.Clamp(Aim.y-delta.y,-80,80));
            // Switch first, then consume this frame's inputs on the new weapon.
            for(int slot=1;slot<=3;slot++)
                if(map["Slot"+slot].WasPressedThisFrame())actor.World.Session.Submit(actor.Id,new PlayerCommand{
                    Envelope=Envelope(CommandType.Action),Action=new ActionCommand{ActionId=slot}});
            ControlButtons held=0,pressed=0;
            if(map["Walk"].IsPressed())held|=ControlButtons.Walk;
            if(map["Crouch"].IsPressed())held|=ControlButtons.Crouch;
            if(map["Fire"].WasPressedThisFrame())pressed|=ControlButtons.Fire;
            if(map["Secondary"].WasPressedThisFrame())pressed|=ControlButtons.Aim;
            if(map["Reload"].WasPressedThisFrame())pressed|=ControlButtons.Reload;
            if(map["Jump"].WasPressedThisFrame())pressed|=ControlButtons.Jump;
            actor.World.Session.Submit(actor.Id,new PlayerCommand{
                Envelope=Envelope(CommandType.DirectControl),
                Direct=new DirectControlFrame{MoveAxes=Vector2.ClampMagnitude(map["Move"].ReadValue<Vector2>(),1),
                    AimYawPitch=Aim,HeldButtons=held,PressedButtons=pressed}});
        }
        public void Release()
        {
            Captured=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(actor!=null&&actor.Initialized&&actor.World!=null)
                actor.World.Session.Submit(actor.Id,new PlayerCommand{Envelope=Envelope(CommandType.Stop)});
        }
        void OnApplicationFocus(bool focus){if(!focus)Release();}
        void OnDisable(){Release();if(runtime!=null){runtime.Disable();Destroy(runtime);}}
    }
}
