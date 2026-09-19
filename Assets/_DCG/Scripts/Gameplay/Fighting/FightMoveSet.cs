using UnityEngine;
namespace DCG.Gameplay.Fighting
{
    [CreateAssetMenu(menuName="DCG/Fighting/Move Set")]
    public sealed class FightMoveSet : ScriptableObject
    {
        public MoveData[] moves;
        public int simultaneousWindow=2,commandWindow=20;
        public float forwardSpeed=2.4f,backSpeed=1.8f,dashSpeed=5,sideSpeed=3,jumpSpeed=6,sideEpsilon=.05f;
        public int tapFrames=6,dashFrames=10,doubleTapWindow=14;
        public bool allowSidestep=true;
        public FighterTags counterStates=FighterTags.AttackStartup|FighterTags.AttackActive;
        public int downFrames=36,getupFrames=18,maxJuggleCost=6;
        public float airDamageScale=.7f,airDamageDecay=.1f,minimumAirDamageScale=.2f;
    }
}
