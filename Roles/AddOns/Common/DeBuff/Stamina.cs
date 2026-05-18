using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUs.GameOptions;
using Hazel;
using HarmonyLib;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class Stamina
    {
        private static readonly int Id = 50900;
        private static Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Stamina);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "ST");
        public static List<byte> playerIdList = new();

        static OptionItem OptionMaxStamina; static float maxStamina;
        static OptionItem OptionDrainRate; static float drainRate;
        static OptionItem OptionRecoverRate; static float recoverRate;
        static OptionItem OptionMaxSpeed; static float maxSpeed;
        static OptionItem OptionSlowSpeed; static float slowSpeed;
        static OptionItem OptionDrainThreshold; static float drainThreshold;

        private const float StopDistancePerSec = 0.15f;

        private static readonly Dictionary<byte, float> CurrentStamina = new();
        private static readonly Dictionary<byte, bool> IsExhausted = new();
        private static readonly Dictionary<byte, Vector2> LastPosition = new();
        private static readonly Dictionary<byte, bool> Initialized = new();
        private static readonly Dictionary<byte, float> NotifyTimer = new();

        private static readonly Dictionary<byte, float> BaseSpeed = new();
        private static readonly Dictionary<byte, float> LastSetSpeed = new();

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Stamina, fromtext: UtilsOption.GetFrom(From.TownOfHost_Pko));
            AddOnsAssignData.Create(Id + 10, CustomRoles.Stamina, true, true, true, true);
            ObjectOptionitem.Create(Id + 20, "AddonOption", true, "", TabGroup.Addons)
                .SetOptionName(() => "Role Option").SetSubRoleOptionItem(CustomRoles.Stamina);

            OptionMaxStamina = FloatOptionItem.Create(Id + 21, "StaminaMax",
                new(5f, 60f, 1f), 20f, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Stamina)
                .SetValueFormat(OptionFormat.Seconds);

            OptionDrainRate = FloatOptionItem.Create(Id + 22, "StaminaDrainRate",
                new(0.1f, 5f, 0.1f), 1f, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Stamina);

            OptionRecoverRate = FloatOptionItem.Create(Id + 23, "StaminaRecoverRate",
                new(0.1f, 5f, 0.1f), 0.5f, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Stamina);

            OptionMaxSpeed = FloatOptionItem.Create(Id + 24, "StaminaMaxSpeed",
                new(0.25f, 3f, 0.05f), 1.0f, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Stamina)
                .SetValueFormat(OptionFormat.Multiplier);

            OptionSlowSpeed = FloatOptionItem.Create(Id + 25, "StaminaSlowSpeed",
                new(0.1f, 2f, 0.05f), 0.5f, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Stamina)
                .SetValueFormat(OptionFormat.Multiplier);

            OptionDrainThreshold = FloatOptionItem.Create(Id + 26, "StaminaDrainThreshold",
                new(0f, 1f, 0.05f), 0.5f, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Stamina)
                .SetValueFormat(OptionFormat.Percent);
        }

        public static void Init()
        {
            playerIdList = new();
            maxStamina = OptionMaxStamina.GetFloat();
            drainRate = OptionDrainRate.GetFloat();
            recoverRate = OptionRecoverRate.GetFloat();
            maxSpeed = OptionMaxSpeed.GetFloat();
            slowSpeed = OptionSlowSpeed.GetFloat();
            drainThreshold = OptionDrainThreshold.GetFloat();

            CurrentStamina.Clear();
            IsExhausted.Clear();
            LastPosition.Clear();
            Initialized.Clear();
            NotifyTimer.Clear();
            BaseSpeed.Clear();
            LastSetSpeed.Clear();
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            CurrentStamina[playerId] = maxStamina;
            IsExhausted[playerId] = false;
            Initialized[playerId] = false;
            NotifyTimer[playerId] = 0f;

            if (AmongUsClient.Instance.AmHost)
            {
                if (Main.AllPlayerSpeed.TryGetValue(playerId, out float spd))
                    BaseSpeed[playerId] = spd;
                else
                    BaseSpeed[playerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);

                SetSpeed(playerId, BaseSpeed[playerId] * maxSpeed);
            }
        }

        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (player == null || !player.IsAlive()) return;
            if (GameStates.IsMeeting) return;

            byte id = player.PlayerId;
            if (!playerIdList.Contains(id)) return;

            var currentPos = player.GetTruePosition();

            if (!Initialized.TryGetValue(id, out bool init) || !init)
            {
                LastPosition[id] = currentPos;
                Initialized[id] = true;

                if (Main.AllPlayerSpeed.TryGetValue(id, out float initialSpd))
                    BaseSpeed[id] = initialSpd;
                else
                    BaseSpeed[id] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);

                return;
            }

            if (Main.AllPlayerSpeed.TryGetValue(id, out float currentDictSpeed))
            {
                if (!Mathf.Approximately(currentDictSpeed, LastSetSpeed.GetValueOrDefault(id, -1f)))
                {
                    BaseSpeed[id] = currentDictSpeed;
                }
            }

            float moved = Vector2.Distance(currentPos, LastPosition[id]);
            LastPosition[id] = currentPos;
            float movedPerSec = moved / Time.fixedDeltaTime;
            bool isMoving = movedPerSec > StopDistancePerSec;

            if (isMoving)
            {
                CurrentStamina[id] -= drainRate * Time.fixedDeltaTime;
                CurrentStamina[id] = Mathf.Max(0f, CurrentStamina[id]);
            }
            else
            {
                CurrentStamina[id] += recoverRate * Time.fixedDeltaTime;
                CurrentStamina[id] = Mathf.Min(maxStamina, CurrentStamina[id]);
            }

            float ratio = CurrentStamina[id] / maxStamina;
            float speedMultiplier = Mathf.Lerp(slowSpeed, maxSpeed, ratio);
            float targetSpeed = BaseSpeed.GetValueOrDefault(id, 1f) * speedMultiplier;

            SetSpeed(id, targetSpeed);

            NotifyTimer[id] += Time.fixedDeltaTime;
            if (NotifyTimer[id] >= 0.2f)
            {
                NotifyTimer[id] = 0f;
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
            }
        }

        public static void OnStartMeeting()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            foreach (var id in playerIdList)
            {
                CurrentStamina[id] = maxStamina;
                IsExhausted[id] = false;
                Initialized[id] = false;
            }
        }

        public static void AfterMeetingTasks()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            foreach (var id in playerIdList)
            {
                var pc = PlayerCatch.GetPlayerById(id);
                if (pc == null || !pc.IsAlive()) continue;
                CurrentStamina[id] = maxStamina;
                IsExhausted[id] = false;
                Initialized[id] = false;

                if (Main.AllPlayerSpeed.TryGetValue(id, out float spd))
                    BaseSpeed[id] = spd;
                else
                    BaseSpeed[id] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);

                float targetSpeed = BaseSpeed[id] * maxSpeed;
                SetSpeed(id, targetSpeed);
            }
        }

        private static void SetSpeed(byte playerId, float speed)
        {
            if (!Main.AllPlayerSpeed.ContainsKey(playerId)) return;
            if (Mathf.Approximately(Main.AllPlayerSpeed[playerId], speed)) return;

            Main.AllPlayerSpeed[playerId] = speed;
            LastSetSpeed[playerId] = speed;
            PlayerCatch.GetPlayerById(playerId)?.MarkDirtySettings();
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class StaminaFixedUpdatePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            if (!GameStates.IsInTask) return;
            if (!__instance.Is(CustomRoles.Stamina)) return;
            Stamina.OnFixedUpdate(__instance);
        }
    }
}