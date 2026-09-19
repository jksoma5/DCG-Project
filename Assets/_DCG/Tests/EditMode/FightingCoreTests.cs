using System;
using DCG.Core;
using DCG.Classes.Paul;
using DCG.Gameplay.Fighting;
using NUnit.Framework;
using UnityEngine;
namespace DCG.Tests
{
    public sealed class FightingCoreTests
    {
        MoveData move;
        [TearDown] public void Cleanup(){if(move!=null)UnityEngine.Object.DestroyImmediate(move);}
        MoveData Make(string command){move=ScriptableObject.CreateInstance<MoveData>();move.command=command;move.moveId="test";return move;}
        [Test] public void OpposingDirectionsAreNeutralAndSideMirrorsOnlyHorizontal()
        {
            Assert.That(FightDirections.Clean(true,true,false,false),Is.EqualTo(5));
            Assert.That(FightDirections.Clean(false,false,true,true),Is.EqualTo(5));
            Assert.That(FightDirections.Relative(3,FightSide.Reversed),Is.EqualTo(1));
            Assert.That(FightDirections.Relative(8,FightSide.Reversed),Is.EqualTo(8));
        }
        [Test] public void SideUsesEastWestWithDeadZone()
        {
            Assert.That(FightDirections.Side(-1,1,FightSide.Reversed,.05f),Is.EqualTo(FightSide.Normal));
            Assert.That(FightDirections.Side(1,-1,FightSide.Normal,.05f),Is.EqualTo(FightSide.Reversed));
            Assert.That(FightDirections.Side(0,.02f,FightSide.Reversed,.05f),Is.EqualTo(FightSide.Reversed));
        }
        [Test] public void HistoryDoesNotReinterpretEarlierFacing()
        {
            var b=new InputBuffer();b.Push(0,new FightInputFrame{Direction=6},FightSide.Normal,2);
            b.Push(1,new FightInputFrame{Direction=6},FightSide.Reversed,2);
            Assert.That(b.Recent(0).Relative,Is.EqualTo(4));Assert.That(b.Recent(1).Relative,Is.EqualTo(6));
        }
        [Test] public void ChordsAcceptTwoFrameGapButSplitLaterPresses()
        {
            var b=new InputBuffer();
            b.Push(0,new FightInputFrame{Direction=5,Pressed=FightButtons.LP},FightSide.Normal,2);
            Assert.That(b.Commit(0,2,out _),Is.False);
            b.Push(2,new FightInputFrame{Direction=5,Pressed=FightButtons.RP},FightSide.Normal,2);
            Assert.That(b.Commit(2,2,out var chord),Is.True);Assert.That(chord.Buttons,Is.EqualTo(FightButtons.LP|FightButtons.RP));
            b.Push(3,new FightInputFrame{Direction=5,Pressed=FightButtons.LK},FightSide.Normal,2);
            Assert.That(b.Commit(5,2,out chord),Is.True);Assert.That(chord.Buttons,Is.EqualTo(FightButtons.LK));
        }
        [Test] public void QuarterCircleMatchesOnBothSides()
        {
            foreach(var side in new[]{FightSide.Normal,FightSide.Reversed})
            {
                var b=new InputBuffer();
                int[] directions={2,3,6};
                for(int f=0;f<3;f++)b.Push(f,new FightInputFrame{Direction=FightDirections.Relative(directions[f],side),
                    Pressed=f==2?FightButtons.RP:0},side,2);
                b.Commit(4,2,out var chord);
                Assert.That(new CommandParser().Matches("d,df,f+2",b,chord,20),Is.True);
            }
        }
        [Test] public void IncompleteMotionAndWrongChordDoNotMatch()
        {
            var b=new InputBuffer();b.Push(0,new FightInputFrame{Direction=6,Pressed=FightButtons.RP},FightSide.Normal,2);b.Commit(2,2,out var c);
            Assert.That(new CommandParser().Matches("d,df,f+2",b,c,20),Is.False);
            Assert.That(new CommandParser().Matches("1+2",b,c,20),Is.False);
        }
        [Test] public void TimelinePreservesDirectionChangesAcrossRenderBatch()
        {
            var t=new FightInputTimeline();t.Enqueue(.01,3,true);t.Enqueue(.02,1,true);t.Enqueue(.03,3,false);t.Enqueue(.03,5,true);
            Assert.That(t.Sample(.015).Direction,Is.EqualTo(2));Assert.That(t.Sample(.025).Direction,Is.EqualTo(3));
            var f=t.Sample(.035);Assert.That(f.Direction,Is.EqualTo(6));Assert.That(f.Pressed,Is.EqualTo(FightButtons.RP));
        }
        [Test] public void MacrosAreTheSameBitsAndDoNotReleaseHeldPhysicalButtons()
        {
            var t=new FightInputTimeline();t.Enqueue(0,8,true);Assert.That(t.Sample(0).Pressed,Is.EqualTo(FightButtons.LP|FightButtons.RP));
            t.Enqueue(1,4,true);t.Enqueue(1,8,false);var f=t.Sample(1);
            Assert.That(f.Held,Is.EqualTo(FightButtons.LP));Assert.That(f.Released,Is.EqualTo(FightButtons.RP));
            t.Enqueue(2,9,true);t.Enqueue(2,5,true);f=t.Sample(2);Assert.That((int)f.Held,Is.EqualTo(15));
        }
        [Test] public void FrameClockMaintainsSixtyTicksAndKeepsCatchupDebt()
        {
            var c=new FrameClock();int total=0;for(int i=0;i<50;i++)total+=c.Advance(.02);
            Assert.That(total,Is.EqualTo(60));Assert.That(c.Remainder,Is.EqualTo(0).Within(1e-8));
            c=new FrameClock();total=c.Advance(1,8);Assert.That(total,Is.EqualTo(8));
            while(c.Remainder>1e-8)total+=c.Advance(0,8);
            Assert.That(total,Is.EqualTo(60));
        }
        [Test] public void AttackFramesAndStunHaveExactDurations()
        {
            var m=Make("1");m.startup=3;m.active=2;m.recovery=4;
            var s=new FighterStateMachine();s.Start(m);Assert.That(s.Active,Is.False);
            s.Advance();s.Advance();Assert.That(s.Active,Is.False);s.Advance();Assert.That(s.Active,Is.True);
            s.Advance();Assert.That(s.Active,Is.True);s.Advance();Assert.That(s.Active,Is.False);
            for(int i=0;i<4;i++)s.Advance();Assert.That(s.Move,Is.Null);
            s.Impact(3,true);s.Advance();s.Advance();Assert.That(s.Stun,Is.EqualTo(1));s.Advance();Assert.That(s.Stun,Is.Zero);
        }
    }
}
