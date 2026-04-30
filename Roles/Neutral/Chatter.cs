using AmongUs.GameOptions;
using HarmonyLib;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class Chatter : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Chatter),
            player => new Chatter(player),
            CustomRoles.Chatter,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            80500,
            SetupOptionItem,
            "ct",
            "#FF66B2",
            (5, 0),
            from: From.SuperNewRoles
        );

    public Chatter(PlayerControl player) : base(RoleInfo, player, () => HasTask.False)
    {
        ChatTimeLimit = OptionChatTimeLimit.GetFloat();
        timeSinceLastChat = 0f;
        meetingActiveTimer = 0f;
    }

    static OptionItem OptionChatTimeLimit;
    static float ChatTimeLimit;

    public float timeSinceLastChat;
    public float meetingActiveTimer;

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 15);

        OptionChatTimeLimit = FloatOptionItem.Create(RoleInfo, 10, "ChatterTimeLimit", new(10f, 120f, 5f), 30f, false)
            .SetOptionName(() => "チャット制限時間")
            .SetValueFormat(OptionFormat.Seconds);
    }

    public void ResetTimer()
    {
        timeSinceLastChat = 0f;
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
    }

    public override void OnStartMeeting()
    {
        timeSinceLastChat = 0f;
        meetingActiveTimer = 0f;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        if (MeetingHud.Instance == null) return;

        if ((int)MeetingHud.Instance.state >= 3) return;

        meetingActiveTimer += Time.fixedDeltaTime;

        if (meetingActiveTimer <= 10f) return;

        timeSinceLastChat += Time.fixedDeltaTime;

        if (timeSinceLastChat >= ChatTimeLimit)
        {
            PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = CustomDeathReason.Suicide;
            Player.RpcMurderPlayerV2(Player);
            UtilsGameLog.AddGameLog("Chatter", $"{UtilsName.GetPlayerColor(Player)} は無言に耐えきれず息絶えた");
        }
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";

        string size = isForHud ? "" : "<size=60%>";

        if (MeetingHud.Instance == null)
            return $"{size}<color={RoleInfo.RoleColorCode}>【チャッター】会議中のみカウントされます</color>";

        if ((int)MeetingHud.Instance.state >= 3)
            return $"{size}<color={RoleInfo.RoleColorCode}>【チャッター】タイマー停止中</color>";

        if (meetingActiveTimer <= 10f)
        {
            float waitTime = 10f - meetingActiveTimer;
            return $"{size}<color={RoleInfo.RoleColorCode}>カウント開始まで: {waitTime:F1}s</color>";
        }

        float remaining = Mathf.Max(0f, ChatTimeLimit - timeSinceLastChat);
        if (remaining <= 5f)
            return $"{size}<color=#FF0000>沈黙死まで: {remaining:F1}s</color>";

        return $"{size}<color={RoleInfo.RoleColorCode}>沈黙まで: {remaining:F1}s</color>";
    }

    public static bool CheckWin(ref GameOverReason reason)
    {
        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            if (pc.GetRoleClass() is not Chatter) continue;
            if (!pc.IsAlive()) continue;

            if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Chatter, pc.PlayerId))
            {
                CustomWinnerHolder.NeutralWinnerIds.Add(pc.PlayerId);
                reason = GameOverReason.ImpostorsByKill;
                return true;
            }
        }
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
public static class Chatter_RpcSendChat_Patch
{
    public static void Postfix(PlayerControl __instance, string chatText)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (__instance.GetRoleClass() is Chatter chatter && __instance.IsAlive())
        {
            chatter.ResetTimer();
        }
    }
}