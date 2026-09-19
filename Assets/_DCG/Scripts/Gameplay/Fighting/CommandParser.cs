using System;
using DCG.Core;
namespace DCG.Gameplay.Fighting
{
    public sealed class CommandParser
    {
        public MoveData Match(InputBuffer buffer,FightChord chord,MoveData[] moves,int window)
        {
            MoveData best=null;
            foreach(var move in moves)
            {
                if(move==null||!Matches(move.command,buffer,chord,window))continue;
                if(best==null||move.priority>best.priority||
                    (move.priority==best.priority&&move.command.Length>best.command.Length))best=move;
            }
            return best;
        }
        public bool Matches(string command,InputBuffer buffer,FightChord chord,int window)
        {
            string[] tokens=command.ToLowerInvariant().Replace(" ","").Split(',');
            int chordIndex=buffer.Chords.Count-1;
            int cursor=chord.Frame;
            for(int t=tokens.Length-1;t>=0;t--)
            {
                Parse(tokens[t],out int direction,out FightButtons buttons);
                if(buttons!=0)
                {
                    if(chordIndex<0)return false;
                    var press=buffer.Chords[chordIndex--];
                    if(press.Buttons!=buttons||chord.Frame-press.Frame>window)return false;
                    cursor=press.Frame;
                    if(direction!=0&&!DirectionAt(buffer,cursor,direction))return false;
                }
                else
                {
                    bool found=false;
                    for(int i=0;i<buffer.Count;i++)
                    {
                        var sample=buffer.Recent(i);
                        if(sample.Frame>=cursor||chord.Frame-sample.Frame>window)continue;
                        if(sample.Relative==direction){cursor=sample.Frame;found=true;break;}
                        if(sample.Relative!=5&&i>0&&buffer.Recent(i-1).Relative!=sample.Relative)return false;
                    }
                    if(!found)return false;
                }
            }
            return tokens.Length>0;
        }
        static bool DirectionAt(InputBuffer buffer,int frame,int direction)
        {
            for(int i=0;i<buffer.Count;i++)if(buffer.Recent(i).Frame==frame)return buffer.Recent(i).Relative==direction;
            return false;
        }
        static void Parse(string token,out int direction,out FightButtons buttons)
        {
            direction=0;buttons=0;
            foreach(var part in token.Split('+'))
            {
                switch(part)
                {
                    case "1":buttons|=FightButtons.LP;break;case "2":buttons|=FightButtons.RP;break;
                    case "3":buttons|=FightButtons.LK;break;case "4":buttons|=FightButtons.RK;break;
                    case "d":direction=2;break;case "df":direction=3;break;case "f":direction=6;break;
                    case "b":direction=4;break;case "db":direction=1;break;case "u":direction=8;break;
                    case "uf":direction=9;break;case "ub":direction=7;break;case "n":direction=5;break;
                }
            }
        }
    }
}
