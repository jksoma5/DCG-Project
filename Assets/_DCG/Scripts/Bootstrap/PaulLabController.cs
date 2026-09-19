using System.Collections.Generic;
using DCG.Core;
using DCG.Gameplay.Fighting;
using DCG.Classes.Paul;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DCG.Bootstrap
{
    public enum FightDummyMode { Idle,AllGuard,CrouchGuard,Jab,Replay }
    public sealed class PaulLabController : MonoBehaviour
    {
        public FighterMatch match;
        public FighterInputReader input;
        public bool automatic=true;
        public FightDummyMode dummyMode;
        public bool Recording { get; private set; }
        public int RecordedFrames=>tape.Count;
        readonly List<FightInputFrame> tape=new List<FightInputFrame>();
        readonly FrameClock clock=new FrameClock();
        int replayIndex;
        uint sequence;
        double sampleTime;
        void Start(){match.Initialize();sampleTime=Time.realtimeSinceStartupAsDouble;}
        void Update()
        {
            if(!automatic)return;
            int count=clock.Advance(Time.unscaledDeltaTime);
            for(int i=0;i<count;i++)
            {
                sampleTime+=FrameClock.StepSeconds;input.Poll();
                if(input.Pressed("Reset")){SceneManager.LoadScene("PaulLab");return;}
                if(input.Pressed("Idle"))dummyMode=FightDummyMode.Idle;
                if(input.Pressed("AllGuard"))dummyMode=FightDummyMode.AllGuard;
                if(input.Pressed("CrouchGuard"))dummyMode=FightDummyMode.CrouchGuard;
                if(input.Pressed("Jab"))dummyMode=FightDummyMode.Jab;
                if(input.Pressed("Record"))SetRecording(!Recording);
                if(input.Pressed("Replay"))StartReplay();
                Tick(input.Sample(sampleTime));
            }
        }
        public void SetRecording(bool value)
        {Recording=value;if(value)tape.Clear();}
        public void StartReplay()
        {Recording=false;dummyMode=FightDummyMode.Replay;replayIndex=0;}
        public void Tick(FightInputFrame player)
        {
            if(Recording&&tape.Count<3600)
            {
                var saved=player;saved.Direction=FightDirections.Relative(saved.Direction,match.first.Side);tape.Add(saved);
            }
            var dummy=new FightInputFrame{Direction=5};
            match.second.HoldPosition=dummyMode!=FightDummyMode.Replay;
            if(dummyMode==FightDummyMode.AllGuard)
                dummy.Direction=match.first.State.Move!=null&&match.first.State.Move.level==HitLevel.Low?1:4;
            if(dummyMode==FightDummyMode.CrouchGuard)dummy.Direction=1;
            if(dummyMode==FightDummyMode.Jab&&match.Frame%40==0)dummy.Pressed=FightButtons.LP;
            if(dummyMode==FightDummyMode.Replay&&tape.Count>0)
            {
                dummy=tape[replayIndex++%tape.Count];
            }
            dummy.Direction=FightDirections.Relative(dummy.Direction,match.second.Side);
            Send(match.first,player);Send(match.second,dummy);match.StepFrame();
        }
        void Send(FighterAgent fighter,FightInputFrame frame)
        {
            match.world.Session.Submit(fighter.Actor.Id,new PlayerCommand{
                Envelope=new CommandEnvelope{ActorId=fighter.Actor.Id,Sequence=++sequence,CommandType=CommandType.FightInput},Fight=frame});
        }
        void OnGUI()
        {
            GUI.Box(new Rect(20,20,500,150),"PAUL / TEKKEN 7 INPUT LAB / 60 Hz");
            GUI.Label(new Rect(34,48,470,115),"WASD Direction | U/I LP/RP | J/K LK/RK\nO Both hands | L Both feet | S, S+D, D+I Phoenix\nF1 Idle | F2 All guard | F3 Crouch guard | F4 Jab\nF5 Reset | F6 Record player | F7 Replay on dummy\nPrototype moves / no Heat, Rage, throws or air combos");
            if(match.first.Actor==null)return;
            GUI.Label(new Rect(Screen.width-280,20,265,100),"FRAME "+match.Frame+" / "+dummyMode+
                "\n"+(Recording?"REC ":"TAPE ")+tape.Count+" frames\n"+match.first.Actor.Health.Current+" HP  vs  "+match.second.Actor.Health.Current+" HP");
            var a=match.first;
            GUI.Box(new Rect(20,Screen.height-125,600,105),"");
            GUI.Label(new Rect(34,Screen.height-115,570,95),a.Side+" | "+a.State.Phase+" | "+a.LastCommand+
                "\n"+a.LastResult+" | advantage "+a.LastAdvantage+"f | move frame "+a.State.Age+" | stun "+a.State.Stun+
                "\nDefender: "+match.second.State.Phase+" | juggle "+match.second.State.JuggleCost+"\nInput: "+History(a));
        }
        string History(FighterAgent agent)
        {
            string value="";int previous=-1;
            for(int i=System.Math.Min(15,agent.Buffer.Count)-1;i>=0;i--)
            {
                var f=agent.Buffer.Recent(i);
                if(f.Relative!=previous||f.Raw.Pressed!=0)value+=f.Relative+(f.Raw.Pressed!=0?"+"+f.Raw.Pressed:"")+"  ";
                previous=f.Relative;
            }
            return value;
        }
    }
}
