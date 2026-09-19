using System;
using System.Collections.Generic;
using DCG.Core;
using DCG.Gameplay.Fighting;
namespace DCG.Classes.Paul
{
    public sealed class FightInputTimeline
    {
        readonly Queue<(double time,int key,bool down)> events=new Queue<(double,int,bool)>();
        int keys;
        public void Enqueue(double time,int key,bool down){events.Enqueue((time,key,down));}
        FightButtons Buttons()
        {
            FightButtons b=0;
            for(int i=0;i<4;i++)if((keys&(1<<(i+4)))!=0)b|=(FightButtons)(1<<i);
            if((keys&(1<<8))!=0)b|=FightButtons.LP|FightButtons.RP;
            if((keys&(1<<9))!=0)b|=FightButtons.LK|FightButtons.RK;
            return b;
        }
        public FightInputFrame Sample(double time)
        {
            FightButtons pressed=0,released=0;
            while(events.Count>0&&events.Peek().time<=time)
            {
                var e=events.Dequeue();var before=Buttons();
                if(e.down)keys|=1<<e.key;else keys&=~(1<<e.key);
                var after=Buttons();pressed|=after&~before;released|=before&~after;
            }
            return new FightInputFrame{Direction=FightDirections.Clean((keys&1)!=0,(keys&2)!=0,(keys&4)!=0,(keys&8)!=0),
                Held=Buttons(),Pressed=pressed,Released=released};
        }
        public void Clear(){keys=0;events.Clear();}
    }
}
