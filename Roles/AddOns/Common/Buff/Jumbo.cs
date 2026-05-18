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
    public static class Jumbo
    {
        private static readonly int Id = 51000;
        private static Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Jumbo);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "J");
        public static List<byte> playerIdList = new();

        private static OptionItem OptionTimeToMax;
        private static OptionItem OptionResetOnMeeting;
        private static OptionItem OptionMaxSize;

        private static float TimeToMax => OptionTimeToMax.GetFloat();
        private static bool ResetOnMeeting => OptionResetOnMeeting.GetBool();
        private static int MaxSize => OptionMaxSize.GetInt();

        private static readonly Dictionary<byte, float> Progress = new();
        private static readonly Dictionary<byte, float> DelayTimer = new();
        private const float SpawnDelay = 3f;

        public static int GetCurrentSize(byte playerId)
        {
            if (!Progress.TryGetValue(playerId, out var p)) return 100;
            return Mathf.Max(100, Mathf.RoundToInt(100 + p * (MaxSize - 100)));
        }

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Jumbo, fromtext: UtilsOption.GetFrom(From.TownOfHost_Pko));
            AddOnsAssignData.Create(Id + 10, CustomRoles.Jumbo, true, true, true, true);
            ObjectOptionitem.Create(Id + 20, "AddonOption", true, "", TabGroup.Addons)
                .SetOptionName(() => "Role Option").SetSubRoleOptionItem(CustomRoles.Jumbo);

            OptionTimeToMax = FloatOptionItem.Create(Id + 21, "JumboTimeToMax",
                new(10f, 300f, 5f), 60f, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Jumbo)
                .SetValueFormat(OptionFormat.Seconds);

            OptionMaxSize = IntegerOptionItem.Create(Id + 22, "JumboMaxSize",
                new(150, 10000, 50), 500, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Jumbo)
                .SetValueFormat(OptionFormat.Percent);

            OptionResetOnMeeting = BooleanOptionItem.Create(Id + 23, "JumboResetOnMeeting",
                true, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Jumbo);
        }

        public static void Init()
        {
            playerIdList.Clear();
            Progress.Clear();
            DelayTimer.Clear();
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            Progress[playerId] = 0f;
            DelayTimer[playerId] = 0f;
        }

        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!GameStates.IsInTask || GameStates.IsMeeting) return;

            byte id = player.PlayerId;
            if (!playerIdList.Contains(id)) return;

            if (!player.IsAlive())
            {
                if (Progress.TryGetValue(id, out var currentP) && currentP > 0f)
                {
                    Progress[id] = 0f;
                    UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
                }
                return;
            }

            if (!DelayTimer.ContainsKey(id)) DelayTimer[id] = 0f;
            if (DelayTimer[id] < SpawnDelay)
            {
                DelayTimer[id] += Time.fixedDeltaTime;
                return;
            }

            if (!Progress.TryGetValue(id, out var p)) return;

            if (p >= 1f) return;

            float delta = Time.fixedDeltaTime / TimeToMax;
            Progress[id] = Mathf.Min(1f, p + delta);

            if (GetCurrentSize(id) != Mathf.RoundToInt(100 + (p + delta) * (MaxSize - 100)))
            {
                UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
            }
        }

        public static void OnStartMeeting()
        {
            if (!AmongUsClient.Instance.AmHost) return;

            foreach (var key in DelayTimer.Keys.ToArray())
            {
                DelayTimer[key] = 0f;
            }

            if (ResetOnMeeting)
            {
                foreach (var key in Progress.Keys.ToArray())
                {
                    Progress[key] = 0f;
                }

                UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
            }
        }

        public static string GetMark(PlayerControl seer, PlayerControl seen)
        {
            if (seen == null || !seen.IsAlive()) return "";
            if (!playerIdList.Contains(seen.PlayerId)) return "";
            if (!playerIdList.Contains(seer.PlayerId) && seer.PlayerId != seen.PlayerId) return "";

            int size = GetCurrentSize(seen.PlayerId);
            return $"<size=60%><color=#ffcc00>({size}%)</color></size>";
        }

        public static string GetNameSizePrefix(byte playerId)
        {
            var pc = PlayerCatch.GetPlayerById(playerId);
            if (pc == null || !pc.IsAlive()) return "";

            if (!playerIdList.Contains(playerId)) return "";
            if (!Progress.TryGetValue(playerId, out var p)) return "";

            int size = GetCurrentSize(playerId);
            if (size <= 100) return "";
            return $"<size={size}%>";
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class JumboFixedUpdatePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            if (!GameStates.IsInTask) return;
            Jumbo.OnFixedUpdate(__instance);
        }
    }
}