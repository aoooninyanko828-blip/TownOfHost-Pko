using System.Collections.Generic;
using UnityEngine;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class Transparent
    {
        private static readonly int Id = 18900;
        private static Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Transparent);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "Ｔ");
        public static List<byte> playerIdList = new();

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Transparent, fromtext: UtilsOption.GetFrom(From.TownOfHost_K));
            AddOnsAssignData.Create(Id + 10, CustomRoles.Transparent, true, true, true, true);
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