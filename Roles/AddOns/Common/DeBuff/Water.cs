using System.Collections.Generic;
using UnityEngine;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class Water
    {
        private static readonly int Id = 19000;
        private static Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Water);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "Ｗ");
        public static List<byte> playerIdList = new();

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Water, fromtext: UtilsOption.GetFrom(From.TownOfHost_K));
            AddOnsAssignData.Create(Id + 10, CustomRoles.Water, true, true, true, true);
        }

        public static void Init()
        {
            playerIdList = new();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }
    }
}