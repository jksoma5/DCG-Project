using System;
using DCG.Core;
using DCG.Gameplay;
using UnityEngine;

namespace DCG.Classes.Sniper
{
    public enum SniperWeapon { Sniper=1, Pistol=2, Knife=3 }
    public sealed class SniperController : MonoBehaviour, IActorActionPolicy, IActorAmmoSource
    {
        public SniperTuning tuning;
        ActorSimulation actor;
        CharacterController body;
        CapsuleCollider hurtbox;
        DirectControlFrame frame;
        ControlButtons edges;
        readonly int[] ammo=new int[4], reserve=new int[4];
        readonly float[] recovery=new float[4];
        bool initialized;
        float inputAge;
        ulong attackId;
        public SniperWeapon Weapon { get; private set; }=SniperWeapon.Sniper;
        public int ScopeLevel { get; private set; }
        public bool Crouched { get; private set; }
        public float DrawRemaining { get; private set; }
        public float ReloadRemaining { get; private set; }
        public float Kick { get; private set; }
        public float HitMarkerRemaining { get; private set; }
        public int ShotsFired { get; private set; }
        public int KnifeAttacks { get; private set; }
        public int Ammo { get { Ensure();return ammo[(int)Weapon]; } }
        public int Reserve { get { Ensure();return reserve[(int)Weapon]; } }
        public int AmmoFor(SniperWeapon weapon) { Ensure();return ammo[(int)weapon]; }
        public float RecoveryRemaining => recovery[(int)Weapon];
        public float Height => body != null ? body.height : tuning.standingHeight;
        public Vector3 Eye => transform.position+Vector3.up*(Height-.15f);
        public Quaternion AimRotation => Quaternion.Euler(frame.AimYawPitch.y,frame.AimYawPitch.x,0);
        public ActionState ActionState => ReloadRemaining>0 ? ActionState.Reload : DrawRemaining>0 ? ActionState.Windup :
            RecoveryRemaining>0 ? ActionState.Recovery : ActionState.Ready;
        public bool UseFullVelocity => false;
        public event Action<Vector3,Vector3> Fired;
        void Awake() { Ensure(); }
        void Ensure()
        {
            if(initialized||tuning==null)return;
            actor=GetComponent<ActorSimulation>();body=GetComponent<CharacterController>();hurtbox=GetComponentInChildren<CapsuleCollider>();
            ammo[1]=tuning.sniperMagazine;reserve[1]=tuning.sniperReserve;
            ammo[2]=tuning.pistolMagazine;reserve[2]=tuning.pistolReserve;
            initialized=true;
        }
        public bool Accepts(PlayerCommand command)
        {
            if(tuning==null)return false;
            if(command.Envelope.CommandType==CommandType.Stop)return true;
            if(command.Envelope.CommandType==CommandType.Action)return command.Action.ActionId>=1&&command.Action.ActionId<=3;
            if(command.Envelope.CommandType!=CommandType.DirectControl)return false;
            var d=command.Direct;
            const ControlButtons valid=ControlButtons.Fire|ControlButtons.Aim|ControlButtons.Reload|ControlButtons.Jump|ControlButtons.Walk|ControlButtons.Crouch;
            return CommandValidation.IsFinite(d.MoveAxes.x)&&CommandValidation.IsFinite(d.MoveAxes.y)&&d.MoveAxes.sqrMagnitude<=1.01f&&
                CommandValidation.IsFinite(d.AimYawPitch.x)&&CommandValidation.IsFinite(d.AimYawPitch.y)&&Mathf.Abs(d.AimYawPitch.y)<=85&&
                (d.HeldButtons&~valid)==0&&(d.PressedButtons&~valid)==0;
        }
        public void Receive(PlayerCommand command)
        {
            Ensure();if(!Accepts(command))return;
            if(command.Envelope.CommandType==CommandType.Stop){ClearInput();ScopeLevel=0;return;}
            if(command.Envelope.CommandType==CommandType.DirectControl)
            {frame=command.Direct;edges|=frame.PressedButtons;inputAge=0;return;}
            var next=(SniperWeapon)command.Action.ActionId;
            if(next==Weapon)return;
            Weapon=next;ScopeLevel=0;ReloadRemaining=0;DrawRemaining=tuning.drawSeconds;edges=0;Kick=0;
            // Ammo and recovery belong to each weapon, not the currently selected slot.
        }
        bool Held(ControlButtons button)=>(frame.HeldButtons&button)!=0;
        bool Pressed(ControlButtons button)=>(edges&button)!=0;
        int Capacity=>Weapon==SniperWeapon.Sniper?tuning.sniperMagazine:tuning.pistolMagazine;
        void SetCrouch(bool value)
        {
            float height=value?tuning.crouchHeight:tuning.standingHeight;
            if(Crouched==value&&Mathf.Approximately(body.height,height))return;
            if(height>body.height)
            {
                float radius=body.radius*.95f;
                if(Physics.CheckCapsule(transform.position+Vector3.up*(body.height-radius+.03f),
                    transform.position+Vector3.up*(height-radius),radius,LayerMask.GetMask("World"),QueryTriggerInteraction.Ignore))return;
            }
            Crouched=value;body.height=height;body.center=Vector3.up*height*.5f;
            if(hurtbox!=null){hurtbox.height=height;hurtbox.center=body.center;}
        }
        public Vector3 DesiredVelocity(float dt)
        {
            Ensure();inputAge+=dt;if(inputAge>tuning.inputTimeout){ClearInput();ScopeLevel=0;}
            for(int i=1;i<=3;i++)recovery[i]=Mathf.Max(0,recovery[i]-dt);
            DrawRemaining=Mathf.Max(0,DrawRemaining-dt);Kick=Mathf.MoveTowards(Kick,0,dt*6);
            HitMarkerRemaining=Mathf.Max(0,HitMarkerRemaining-dt);
            if(ReloadRemaining>0)
            {
                ReloadRemaining=Mathf.Max(0,ReloadRemaining-dt);
                if(ReloadRemaining<=0){int count=Mathf.Min(Capacity-ammo[(int)Weapon],reserve[(int)Weapon]);ammo[(int)Weapon]+=count;reserve[(int)Weapon]-=count;}
            }
            SetCrouch(Held(ControlButtons.Crouch));
            actor.Motor.Face(Quaternion.Euler(0,frame.AimYawPitch.x,0)*Vector3.forward,100000,dt);
            if(Pressed(ControlButtons.Jump))actor.Motor.Jump(tuning.jumpSpeed);
            if(Pressed(ControlButtons.Reload)&&Weapon!=SniperWeapon.Knife&&DrawRemaining<=0&&ReloadRemaining<=0&&Ammo<Capacity&&Reserve>0)
            {ScopeLevel=0;ReloadRemaining=Weapon==SniperWeapon.Sniper?tuning.sniperReload:tuning.pistolReload;}
            if(Pressed(ControlButtons.Aim)&&Weapon==SniperWeapon.Sniper&&DrawRemaining<=0&&ReloadRemaining<=0&&RecoveryRemaining<=0)
                ScopeLevel=(ScopeLevel+1)%3;
            float speed=Weapon==SniperWeapon.Sniper?tuning.sniperSpeed:Weapon==SniperWeapon.Pistol?tuning.pistolSpeed:tuning.knifeSpeed;
            if(Held(ControlButtons.Walk))speed*=tuning.walkMultiplier;
            if(Crouched)speed*=tuning.crouchMultiplier;
            return Quaternion.Euler(0,frame.AimYawPitch.x,0)*new Vector3(frame.MoveAxes.x,0,frame.MoveAxes.y)*speed;
        }
        public void AfterMove(float dt,uint tick)
        {
            if(DrawRemaining<=0&&ReloadRemaining<=0&&RecoveryRemaining<=0)
            {
                if(Weapon==SniperWeapon.Knife)
                {
                    if(Pressed(ControlButtons.Aim))Slash(true);
                    else if(Pressed(ControlButtons.Fire))Slash(false);
                }
                else if(Pressed(ControlButtons.Fire)&&Ammo>0)Shoot();
            }
            edges=0;
        }
        void Shoot()
        {
            bool sniper=Weapon==SniperWeapon.Sniper;
            float spread=sniper?(ScopeLevel>0?tuning.sniperScopedSpread:tuning.sniperHipSpread):tuning.pistolSpread;
            if(frame.MoveAxes.sqrMagnitude>.01f)spread*=2;
            if(actor.Motor.State==MovementState.Airborne)spread*=3;
            if(Crouched)spread*=.65f;
            ShotsFired++;attackId++;ammo[(int)Weapon]--;
            float angle=ShotsFired*2.39996323f;
            float radius=spread*Mathf.Sqrt(((ShotsFired*37)%101)/100f);
            Vector3 direction=AimRotation*Quaternion.Euler(Mathf.Sin(angle)*radius,Mathf.Cos(angle)*radius,0)*Vector3.forward;
            Vector3 endpoint=Eye+direction*tuning.gunRange;
            if(Cast(Eye,direction,tuning.gunRange,out var hit))
            {
                endpoint=hit.point;
                var target=hit.collider.GetComponentInParent<ActorSimulation>();
                Apply(target,sniper?tuning.sniperDamage:tuning.pistolDamage);
            }
            Fired?.Invoke(Eye,endpoint);
            recovery[(int)Weapon]=sniper?tuning.sniperCycle:tuning.pistolCycle;
            ScopeLevel=0;Kick=sniper?1:.35f;
        }
        void Slash(bool heavy)
        {
            KnifeAttacks++;attackId++;Kick=heavy?1.2f:.7f;
            recovery[3]=heavy?tuning.knifeHeavyCycle:tuning.knifeCycle;
            Vector3 direction=AimRotation*Vector3.forward;
            if(Cast(Eye,direction,tuning.knifeRange,out var hit))
                Apply(hit.collider.GetComponentInParent<ActorSimulation>(),heavy?tuning.knifeHeavyDamage:tuning.knifeDamage);
        }
        void Apply(ActorSimulation target,float damage)
        {
            if(target==null||target==actor||target.team==actor.team)return;
            if(actor.World.Damage.Apply(new DamageRequest(actor.Id,target.Id,attackId,damage),target))HitMarkerRemaining=.15f;
        }
        bool Cast(Vector3 origin,Vector3 direction,float distance,out RaycastHit nearest)
        {
            nearest=default;float closest=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(origin,direction,distance,LayerMask.GetMask("World","NavigationSurface","Hurtbox"),QueryTriggerInteraction.Collide))
            {
                if(hit.collider.GetComponentInParent<ActorSimulation>()==actor||hit.distance>=closest)continue;
                nearest=hit;closest=hit.distance;
            }
            return closest<float.PositiveInfinity;
        }
        void ClearInput(){frame.MoveAxes=Vector2.zero;frame.HeldButtons=0;edges=0;}
        public void Stop(){ClearInput();ScopeLevel=0;ReloadRemaining=0;DrawRemaining=0;Kick=0;}
    }
}
