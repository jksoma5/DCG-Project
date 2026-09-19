using DCG.Gameplay.Fighting;
using UnityEngine;
namespace DCG.Presentation
{
    public sealed class FighterView : MonoBehaviour
    {
        public FighterAgent fighter;
        public Transform body,leftHand,rightHand,leftFoot,rightFoot;
        public Renderer hitbox;
        void LateUpdate()
        {
            if(fighter.Actor==null)return;
            float height=fighter.Crouched?.55f:.9f;
            body.localPosition=Vector3.up*height;body.localScale=new Vector3(.65f,height,.55f);
            leftHand.localPosition=new Vector3(-.35f,height+.35f,.25f);
            rightHand.localPosition=new Vector3(.35f,height+.3f,.25f);
            leftFoot.localPosition=new Vector3(-.18f,.18f,0);rightFoot.localPosition=new Vector3(.18f,.18f,0);
            body.localRotation=Quaternion.identity;
            if(fighter.State.IsDown)
            {
                body.localPosition=Vector3.up*.25f;body.localRotation=Quaternion.Euler(fighter.State.DownPose==KnockdownPose.FaceUp?90:-90,fighter.State.HeadTowardAttacker?180:0,0);
                leftHand.localPosition=new Vector3(-.35f,.2f,.5f);rightHand.localPosition=new Vector3(.35f,.2f,.5f);
            }
            else if(fighter.State.IsAirborne)body.localRotation=Quaternion.Euler(-25,0,fighter.State.Reaction==HitReaction.Screw?Time.time*720:0);
            else if(fighter.State.ReactionState==FighterReactionState.Hitstun)body.localRotation=Quaternion.Euler(-15,0,fighter.State.Reaction==HitReaction.Spin?Time.time*360:0);
            hitbox.enabled=fighter.State.Active;
            if(fighter.State.Move==null)return;
            var move=fighter.State.Move;
            float swing=fighter.State.Age<move.startup?-.15f:fighter.State.Active?.65f:.1f;
            if(move.command.EndsWith("4"))rightFoot.localPosition+=new Vector3(0,.25f,swing);
            else if(move.command.EndsWith("3"))leftFoot.localPosition+=new Vector3(0,.25f,swing);
            else if(move.command.EndsWith("1"))leftHand.localPosition+=Vector3.forward*swing;
            else rightHand.localPosition+=Vector3.forward*swing;
            hitbox.transform.localPosition=new Vector3(0,move.level==HitLevel.Low?.3f:move.level==HitLevel.High?1.4f:1,move.range*.5f);
            hitbox.transform.localScale=new Vector3(.6f,.25f,move.range);
        }
    }
}
