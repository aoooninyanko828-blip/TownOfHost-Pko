using System.Collections.Generic;
using HarmonyLib;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost.Patches;

/// <summary>
/// 誰でもダミーをキルできるようにするPatch。
/// PlayerControl.CheckMurder をフックして、
/// ホスト側でキラーの近くにダミーがいれば通常キルをキャンセルしてダミーをキルする。
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
public static class DummyKillPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (__instance == null || !__instance.IsAlive()) return true;

        // ★ キラーの近くにキル可能ダミーがいるか確認
        var dummy = CustomNetObject.GetKillableTarget(__instance, 2.0f);
        if (dummy is IKillableDummy kd)
        {
            // ★ ダミーをキルして通常キルをキャンセル
            kd.OnKilled(__instance);

            return false; // 通常キルをキャンセル
        }

        return true; // ダミーがいなければ通常キル
    }
}