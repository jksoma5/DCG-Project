using System;
using System.Collections.Generic;
using DCG.Core;
namespace DCG.Gameplay.Fighting
{
    public enum FightSide { Normal, Reversed }
    public static class FightDirections
    {
        public static int Clean(bool left,bool right,bool up,bool down)
        { int x=left==right?0:left?-1:1,y=up==down?0:up?1:-1;return 5+x+3*y; }
        public static int Relative(int raw,FightSide side)
        { return side==FightSide.Normal?raw:((raw-1)/3)*3+3-(raw-1)%3; }
        public static FightSide Side(float firstX,float secondX,FightSide previous,float epsilon)
        { return Math.Abs(firstX-secondX)<=epsilon?previous:firstX<secondX?FightSide.Normal:FightSide.Reversed; }
        public static bool Down(int d)=>d<=3;
        public static bool Up(int d)=>d>=7;
    }
    public readonly struct FightSample
    {
        public readonly int Frame,Relative;
        public readonly FightInputFrame Raw;
        public readonly FightSide Side;
        public FightSample(int frame,FightInputFrame raw,FightSide side){Frame=frame;Raw=raw;Side=side;Relative=FightDirections.Relative(raw.Direction,side);}
    }
    public readonly struct FightChord
    {
        public readonly int Frame;
        public readonly FightButtons Buttons;
        public FightChord(int frame,FightButtons buttons){Frame=frame;Buttons=buttons;}
    }
    public sealed class InputBuffer
    {
        readonly FightSample[] samples=new FightSample[60];
        readonly List<FightChord> chords=new List<FightChord>();
        int head,count,pendingFrame;
        FightButtons pending;
        public int Count=>count;
        public IReadOnlyList<FightChord> Chords=>chords;
        public FightSample Recent(int offset)=>samples[(head-1-offset+60)%60];
        public void Push(int frame,FightInputFrame raw,FightSide side,int chordWindow)
        {
            samples[head]=new FightSample(frame,raw,side);head=(head+1)%60;count=Math.Min(60,count+1);
            if(raw.Pressed!=0)
            {
                if(pending==0)pendingFrame=frame;
                pending|=raw.Pressed;
            }
        }
        public bool Commit(int frame,int window,out FightChord chord)
        {
            chord=default;
            if(pending==0||frame-pendingFrame<window)return false;
            chord=new FightChord(pendingFrame,pending);chords.Add(chord);
            chords.RemoveAll(c=>frame-c.Frame>=60);pending=0;return true;
        }
        public void Clear(){head=count=0;pending=0;chords.Clear();}
    }
    public sealed class FrameClock
    {
        public const double StepSeconds=1.0/60.0;
        double accumulated;
        public double Remainder=>accumulated;
        public int Advance(double seconds,int maxFrames=12)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)return 0;
            accumulated+=seconds;
            int frames=(int)Math.Min(maxFrames,Math.Floor((accumulated+1e-10)/StepSeconds));
            accumulated-=frames*StepSeconds;return frames;
        }
    }
}
