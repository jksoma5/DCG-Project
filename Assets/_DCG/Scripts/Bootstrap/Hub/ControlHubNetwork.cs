using DCG.Core;
using DCG.Gameplay;
using DCG.Gameplay.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCG.Bootstrap.Hub
{
    public sealed partial class ControlHubController
    {
        public ControlModuleBase[] NetworkPlayers { get; private set; }
        public int NetworkLocalSlot { get; private set; }

        public void CreateNetworkDuel(ClassId first, ClassId second, int localSlot, GridGraph grid)
        {
            ClearNetworkDuel();
            nextActorId = 1;
            world.ResetSession();
            NetworkLocalSlot = localSlot;
            NetworkPlayers = new ControlModuleBase[2];
            var ids = new[] { first, second };
            for (int i = 0; i < 2; i++)
            {
                var source = Find(ids[i]);
                if (source == null) throw new System.InvalidOperationException("Missing class module: " + ids[i]);
                // Clone configuration, not a live actor: mirror matches need independent policies and ammo.
                var copy = Instantiate(source.gameObject, transform).GetComponent<ControlModuleBase>();
                copy.name = "Network player " + i;
                if (copy is GravesModule graves) graves.grid = grid;
                NetworkPlayers[i] = copy;
                copy.Activate(context, HubRole.Opponent);
                var actor = copy.PrimaryActor;
                actor.team = i;
                actor.Motor.ApplySnapshot(new Vector3(i == 0 ? -8 : 8, .1f, 0), i == 0 ? 90 : 270, Vector3.zero);
                if (ids[i] == ClassId.Rifle || ids[i] == ClassId.Sniper || ids[i] == ClassId.Vendetta)
                    actor.Receive(new PlayerCommand {
                        Envelope = new CommandEnvelope { ActorId = actor.Id, CommandType = CommandType.DirectControl },
                        Direct = new DirectControlFrame { AimYawPitch = new Vector2(i == 0 ? 90 : 270, 0) }
                    });
            }
            NetworkPlayers[0].SetOpponent(NetworkPlayers[1]);
            NetworkPlayers[1].SetOpponent(NetworkPlayers[0]);
            ActiveModule = NetworkPlayers[localSlot];
            OpponentModule = NetworkPlayers[1 - localSlot];
            NetworkPlayers[localSlot].SetControlled(true);
            NetworkPlayers[localSlot].SetInputEnabled(false);
            ApplyCursor(HubCursorMode.Free);
            Physics.SyncTransforms();
        }

        public void SetNetworkInput(bool enabled)
        {
            if (NetworkPlayers == null) { ApplyCursor(HubCursorMode.Free); return; }
            NetworkPlayers[NetworkLocalSlot].SetInputEnabled(enabled);
            ApplyCursor(enabled ? ActiveModule.RequiredCursor : HubCursorMode.Free);
        }

        public void SampleNetworkInput()
        {
            InputSystem.Update();
            if (ActiveModule is PaulModule paul) paul.SampleNetworkInput();
        }

        public void StepNetworkWorld()
        {
            world.Step(1f / 60);
            if (NetworkPlayers == null) return;
            // Paul vs Paul is a single match: advancing both module matches would step both fighters twice.
            if (NetworkPlayers[0] is PaulModule first) first.Match.StepFrame();
            else if (NetworkPlayers[1] is PaulModule second) second.Match.StepFrame();
            Physics.SyncTransforms();
        }
        public void StepPredictedWorld()
        {
            if (ActiveModule == null) return;
            world.StepPredicted(ActiveModule.PrimaryActor, 1f / 60);
            if (ActiveModule is PaulModule paul)
            {
                paul.Match.UpdatePlane();
                if (paul.Fighter.Actor.Health.IsAlive) paul.Fighter.State.Advance();
                paul.Fighter.Prepare((int)world.Tick);
            }
            Physics.SyncTransforms();
        }

        public void UpdateNetworkViews(bool late)
        {
            if (NetworkPlayers == null) return;
            foreach (var player in NetworkPlayers)
                if (late) player.LateUpdateView(Time.deltaTime); else player.UpdateView(Time.deltaTime);
        }

        public void ClearNetworkDuel()
        {
            if (NetworkPlayers != null)
                foreach (var player in NetworkPlayers)
                    if (player != null) { player.Deactivate(); Destroy(player.gameObject); }
            NetworkPlayers = null; ActiveModule = null; OpponentModule = null;
            if (hubCamera != null)
            {
                hubCamera.fieldOfView = defaultFov;
                hubCamera.nearClipPlane = defaultNear;
                hubCamera.farClipPlane = defaultFar;
            }
            ApplyCursor(HubCursorMode.Free);
        }
    }
}
