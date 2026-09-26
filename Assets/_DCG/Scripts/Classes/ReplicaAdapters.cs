using DCG.Core;
using UnityEngine;

namespace DCG.Classes.Rifle
{
    public sealed partial class RifleController : IReplicaStateSource
    {
        public ReplicaState CaptureReplica() => new ReplicaState {
            Mode=(int)FireMode, Stance=(int)Stance, AimMode=(int)AimMode, Reserve=Reserve,
            Reload=ReloadRemaining, Kick=Recoil, Lean=Lean, Height=Height, Aim=frame.AimYawPitch
        };
        public void ApplyReplica(ReplicaState value)
        {
            Ensure(); FireMode=(RifleFireMode)value.Mode; SetStance((RifleStance)value.Stance);
            AimMode=(RifleAimMode)value.AimMode; Reserve=value.Reserve; ReloadRemaining=value.Reload;
            Recoil=value.Kick; Lean=value.Lean; frame.AimYawPitch=value.Aim;
        }
        public void ShowNetworkShot(Vector3 origin, Vector3 impact) => Tracer?.Invoke(origin, impact);
    }
}
namespace DCG.Classes.Sniper
{
    public sealed partial class SniperController : IReplicaStateSource
    {
        public ReplicaState CaptureReplica() => new ReplicaState {
            Mode=(int)Weapon, Scope=ScopeLevel, Crouched=Crouched, Reserve=Reserve,
            Reload=ReloadRemaining, Recovery=RecoveryRemaining, Cooldown=DrawRemaining, Kick=Kick, Aim=frame.AimYawPitch
        };
        public void ApplyReplica(ReplicaState value)
        {
            Ensure(); Weapon=(SniperWeapon)Mathf.Clamp(value.Mode,1,3); ScopeLevel=value.Scope;
            SetCrouch(value.Crouched); reserve[(int)Weapon]=value.Reserve; ReloadRemaining=value.Reload;
            recovery[(int)Weapon]=value.Recovery; DrawRemaining=value.Cooldown; Kick=value.Kick; frame.AimYawPitch=value.Aim;
        }
        public void ShowNetworkShot(Vector3 origin, Vector3 impact) => Fired?.Invoke(origin, impact);
    }
}
namespace DCG.Classes.Vendetta
{
    public sealed partial class VendettaController : IReplicaStateSource
    {
        public ReplicaState CaptureReplica() => new ReplicaState {
            Mode=(int)Phase, PhaseTime=phaseTime, Cooldown=ShiftRemaining, Cooldown2=ERemaining,
            SwordPosition=SwordPosition, SwordDestination=SwordDestination, Aim=aim
        };
        public void ApplyReplica(ReplicaState value)
        {
            Phase=(VendettaPhase)value.Mode; phaseTime=value.PhaseTime; ShiftRemaining=value.Cooldown;
            ERemaining=value.Cooldown2; SwordPosition=value.SwordPosition; SwordDestination=value.SwordDestination; aim=value.Aim;
        }
    }
}
