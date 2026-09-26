using DCG.Core;
using DCG.Gameplay;
using DCG.Gameplay.Fighting;
using DCG.Classes.Graves;
using DCG.Classes.Vendetta;
using DCG.Classes.Rifle;
using DCG.Classes.Sniper;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCG.Bootstrap.Hub
{
    public sealed partial class ControlHubController
    {
        public bool ArenaMode;
        public bool EnemyAI = true;
        bool arenaMenu;
        GUIStyle healthLabel;
        InputActionMap arenaActions;
        int pendingCharacter = -1;
        bool pendingMenu, pendingReset;
        int resetFrame = -1;
        float enemyGraceUntil;
        static readonly ClassId[] roster = { ClassId.Graves, ClassId.Vendetta, ClassId.Rifle, ClassId.Sniper, ClassId.Paul };
        static readonly string[] names = { "GRAVES", "VENDETTA", "M416", "TRG", "PAUL" };
        // Plaza bounds: X -30..18, Z -16..16. Leave three metres inside each wall.
        static readonly Vector3[] enemyCorners = {
            new Vector3(-27, 0, -13), new Vector3(15, 0, -13),
            new Vector3(-27, 0, 13), new Vector3(15, 0, 13)
        };

        public void StartArena(ClassId selected)
        {
            foreach (var module in modules) module.Deactivate();
            ActiveModule = null;
            OpponentModule = null;
            ArenaMode = true;
            InitializeArenaInput();
            int enemyIndex = 0;
            for (int i = 0; i < roster.Length; i++)
            {
                var module = Find(roster[i]);
                module.Activate(context, HubRole.Opponent);
                var actor = module.PrimaryActor;
                var body = actor.GetComponent<CharacterController>();
                body.enabled = false;
                actor.transform.position = map.plazaCenter.position +
                    (roster[i] == selected ? Vector3.zero : enemyCorners[enemyIndex++]);
                body.enabled = true;
                actor.name = names[i];
            }
            Physics.SyncTransforms();
            enemyGraceUntil = Time.time + 2;
            SwitchCharacter(selected);
        }

        public bool SwitchCharacter(ClassId id)
        {
            var next = Find(id);
            if (!ArenaMode || next == null || !next.Active || !next.PrimaryActor.Health.IsAlive) return false;
            var previous = ActiveModule as ControlModuleBase;
            if (previous != next)
            {
                if (previous != null) { previous.SetControlled(false); ClearOwnership(previous.PrimaryActor); }
                ClearOwnership(next.PrimaryActor);
                hubCamera.fieldOfView = defaultFov;
                hubCamera.nearClipPlane = defaultNear;
                hubCamera.farClipPlane = defaultFar;
                ActiveModule = next;
                foreach (var module in modules) module.PrimaryActor.team = module == next ? 0 : 1;
                next.SetControlled(true);
            }
            arenaMenu = false;
            next.SetInputEnabled(true);
            ApplyCursor(next.RequiredCursor);
            UpdatePaulTarget();
            return true;
        }

        void ClearOwnership(ActorSimulation actor)
        {
            world.Session.TransferControl(actor.Id);
            // Clear held input only; do not reset cooldowns, reloads, health or active moves.
            actor.Receive(new PlayerCommand {
                Envelope = new CommandEnvelope { ActorId = actor.Id, CommandType =
                    actor.classId == ClassId.Paul ? CommandType.FightInput : CommandType.Stop },
                Fight = new FightInputFrame { Direction = 5 }
            });
        }

        void ResetArena()
        {
            if (resetFrame == Time.frameCount) return;
            resetFrame = Time.frameCount;
            StartArena(ActiveModule != null ? ActiveModule.Id : ClassId.Graves);
        }

        void InitializeArenaInput()
        {
            if (arenaActions != null) return;
            arenaActions = new InputActionMap("Arena");
            for (int i = 0; i < roster.Length; i++)
            {
                int index = i;
                arenaActions.AddAction("Select" + i, InputActionType.Button, "<Keyboard>/f" + (8 + i))
                    .performed += _ => pendingCharacter = index;
            }
            arenaActions.AddAction("Menu", InputActionType.Button, "<Keyboard>/escape")
                .performed += _ => pendingMenu = true;
            arenaActions.AddAction("Restart", InputActionType.Button, "<Keyboard>/f5")
                .performed += _ => pendingReset = true;
            arenaActions.Enable();
        }

        void ReadArenaKeys()
        {
            // Actions latch edges so extra manual input updates cannot consume a switch key.
            bool reset = pendingReset, menu = pendingMenu;
            int character = pendingCharacter;
            pendingReset = pendingMenu = false;
            pendingCharacter = -1;
            if (reset) ResetActive();
            if (character >= 0) SwitchCharacter(roster[character]);
            else if (menu) ShowSelect();
        }

        public bool BlocksArenaPointer(Vector2 point)
        {
            point.y = Screen.height - point.y;
            return ArenaBar().Contains(point) || new Rect(20, 20, 380, 115).Contains(point);
        }

        Rect ArenaBar()
        {
            float width = Mathf.Min(650, Screen.width - 20);
            return new Rect((Screen.width - width) / 2, arenaMenu ? Screen.height * .32f : Screen.height - 210, width, 76);
        }

        void UpdatePaulTarget()
        {
            var paul = Find(ClassId.Paul) as PaulModule;
            if (paul == null || !paul.Active) return;
            ControlModuleBase target = ActiveModule as ControlModuleBase;
            if (target == paul)
            {
                target = null;
                float nearest = float.PositiveInfinity;
                foreach (var module in modules)
                {
                    if (module == paul || !module.PrimaryActor.Health.IsAlive) continue;
                    float distance = (module.PrimaryActor.transform.position - paul.PrimaryActor.transform.position).sqrMagnitude;
                    if (distance < nearest) { nearest = distance; target = module; }
                }
            }
            if (target != null) paul.SetOpponent(target);
        }

        public FightInputFrame EnemyFightInput(PaulModule paul)
        {
            var target = ActiveModule?.PrimaryActor;
            if (!EnemyAI || Time.time < enemyGraceUntil || target == null || !target.Health.IsAlive)
                return new FightInputFrame { Direction = 5 };
            float distance = Vector3.Distance(paul.PrimaryActor.transform.position, target.transform.position);
            return new FightInputFrame {
                Direction = FightDirections.Relative(distance > 1.3f ? 6 : 5, paul.Fighter.Side),
                Pressed = distance < 2 && paul.Match.Frame % 45 == 0 ? FightButtons.LP : FightButtons.None
            };
        }

        void UpdateArenaEnemies()
        {
            UpdatePaulTarget();
            var target = ActiveModule.PrimaryActor;
            foreach (var module in modules)
            {
                if (module == ActiveModule || module.Id == ClassId.Paul) continue;
                var actor = module.PrimaryActor;
                if (!actor.Health.IsAlive) continue;
                if (!EnemyAI || !target.Health.IsAlive || Time.time < enemyGraceUntil)
                {
                    SendEnemy(actor, new PlayerCommand { Envelope = new CommandEnvelope { CommandType = CommandType.Stop } });
                    continue;
                }
                if (module.Id == ClassId.Graves)
                {
                    var orders = actor.GetComponent<GravesOrderController>();
                    if (orders.TargetId != target.Id)
                        SendEnemy(actor, new PlayerCommand {
                            Envelope = new CommandEnvelope { CommandType = CommandType.Target },
                            Target = new TargetCommand { TargetActorId = target.Id, OrderType = OrderType.AttackTarget }
                        });
                    continue;
                }
                Vector3 origin = actor.AimPoint;
                if (module.Id == ClassId.Rifle) origin = actor.GetComponent<RifleController>().Eye;
                if (module.Id == ClassId.Sniper) origin = actor.GetComponent<SniperController>().Eye;
                var direction = target.AimPoint - origin;
                float distance = direction.magnitude;
                bool knife = module.Id == ClassId.Sniper && actor.GetComponent<SniperController>().Weapon == SniperWeapon.Knife;
                bool visible = !Physics.Linecast(origin, target.AimPoint, LayerMask.GetMask("World", "NavigationSurface"));
                float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
                bool melee = module.Id == ClassId.Vendetta;
                bool fire = visible && !melee && world.Tick % 90 < 12;
                var frame = new DirectControlFrame {
                    MoveAxes = distance > (melee || knife ? 1.5f : 10) || !visible ? Vector2.up : Vector2.zero,
                    AimYawPitch = new Vector2(yaw, Mathf.Clamp(pitch, -80, 80)),
                    HeldButtons = fire ? ControlButtons.Fire : ControlButtons.None,
                    PressedButtons = fire && world.Tick % 90 == 0 ? ControlButtons.Fire : ControlButtons.None
                };
                var ammo = actor.Movement as IActorAmmoSource;
                if (ammo != null && ammo.Ammo == 0) frame.PressedButtons |= ControlButtons.Reload;
                SendEnemy(actor, new PlayerCommand { Envelope = new CommandEnvelope { CommandType = CommandType.DirectControl }, Direct = frame });
                // Apply aim before asking Vendetta to dash along it.
                if (melee && visible && distance < 7 && world.Tick % 120 == 0)
                    SendEnemy(actor, new PlayerCommand {
                        Envelope = new CommandEnvelope { CommandType = CommandType.Action },
                        Action = new ActionCommand { ActionId = VendettaController.ShiftAction, TargetPoint = target.AimPoint }
                    }, 1);
            }
        }

        void SendEnemy(ActorSimulation actor, PlayerCommand command, uint offset = 0)
        {
            command.Envelope.ActorId = actor.Id;
            command.Envelope.ClientTick = world.Tick;
            command.Envelope.Sequence = actor.LastSequence + 1 + offset;
            world.Session.Submit(actor.Id, command);
        }

        void DrawHealthBar(Rect rect, ActorSimulation actor, bool controlled)
        {
            if (healthLabel == null)
            {
                healthLabel = new GUIStyle(GUI.skin.label) {
                    alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold
                };
                healthLabel.normal.textColor = Color.white;
            }
            Color previous = GUI.color;
            GUI.color = new Color(.04f, .05f, .07f, .95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            float ratio = Mathf.Clamp01(actor.Health.Current / actor.Health.Maximum);
            GUI.color = !actor.Health.IsAlive ? Color.gray : controlled
                ? new Color(.1f, .62f, .36f) : new Color(.75f, .17f, .12f);
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * ratio, rect.height - 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, actor.Health.Current.ToString("0") + " / " + actor.Health.Maximum.ToString("0"), healthLabel);
            GUI.color = previous;
        }

        void DrawArena()
        {
            if (ActiveModule == null) return;
            if (!arenaMenu) ActiveModule.DrawHud();
            var area = ArenaBar();
            float width = area.width;
            GUI.Box(area, "F8-F12  Switch character  |  Esc  Selection  |  F5  Restart arena");
            for (int i = 0; i < roster.Length; i++)
            {
                var module = Find(roster[i]);
                var actor = module.PrimaryActor;
                GUI.enabled = actor.Health.IsAlive;
                string label = "F" + (8 + i) + " " + names[i] + (module == ActiveModule ? " [YOU]" : "") + " " + actor.Health.Current.ToString("0");
                float cellX = area.x + 5 + i * (width - 10) / 5;
                float cellWidth = (width - 10) / 5 - 3;
                if (GUI.Button(new Rect(cellX, area.y + 24, cellWidth, 24), label))
                    SwitchCharacter(roster[i]);
                GUI.enabled = true;
                DrawHealthBar(new Rect(cellX, area.y + 52, cellWidth, 18), actor, module == ActiveModule);
            }
            GUI.enabled = true;
            int living = 0;
            foreach (var module in modules)
            {
                var actor = module.PrimaryActor;
                bool controlled = module == ActiveModule;
                if (!controlled && actor.Health.IsAlive) living++;
                Vector3 point = hubCamera.WorldToScreenPoint(actor.AimPoint + Vector3.up * .8f);
                if (point.z <= hubCamera.nearClipPlane || point.x < 0 || point.x > Screen.width ||
                    point.y < 0 || point.y > Screen.height) continue;
                float x = Mathf.Clamp(point.x - 65, 0, Mathf.Max(0, Screen.width - 130));
                float y = Mathf.Clamp(Screen.height - point.y, 22, Mathf.Max(22, Screen.height - 20));
                GUI.color = Color.white;
                GUI.Box(new Rect(x, y - 22, 130, 22), actor.name + (controlled ? " [YOU]" : ""));
                DrawHealthBar(new Rect(x, y, 130, 18), actor, controlled);
            }
            GUI.color = Color.white;
            if (!ActiveModule.PrimaryActor.Health.IsAlive || living == 0)
                GUI.Box(new Rect(Screen.width / 2 - 220, 200, 440, 48), living == 0
                    ? "VICTORY - F5 to restart" : "DEFEATED - F8-F12 to take over a survivor / F5 restart");
        }
    }
}
