using AmongUs.GameOptions;
using HarmonyLib;
using TownOfHost.Modules;
using TownOfHost.Modules.ChatManager;
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
            from: From.TownOfHost_Pko
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

        OptionChatTimeLimit = FloatOptionItem.Create(RoleInfo, 10, "ChatterTimeLimit", new(5f, 120f, 2.5f), 20f, false)
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
    }

    public void UpdateMeetingTimer()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInGame || !GameStates.IsMeeting) return;
        if (Player == null || !Player || Player.Data == null || Player.Data.Disconnected) return;
        if (!Player.IsAlive()) return;
        if (MeetingHud.Instance == null || (int)MeetingHud.Instance.state >= 3) return;

        meetingActiveTimer += Time.deltaTime;
        if (meetingActiveTimer <= 10f) return;

        timeSinceLastChat += Time.deltaTime;
        if (timeSinceLastChat < ChatTimeLimit) return;

        var chatterId = Player.PlayerId;
        timeSinceLastChat = -9999f;

        Utils.SendMessage(
            $"<color=#FFCC00><b>【=== おい!!見ろ!!あいつが!! ===】</b></color>\n{UtilsName.GetPlayerColor(Player)} が急に意識を失った。\nどうしたんだろうな。",
            title: GetString("MSKillTitle"));

        _ = new LateTask(() =>
        {
            if (!GameStates.IsInGame || !GameStates.IsMeeting || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
                return;

            var chatter = PlayerCatch.GetPlayerById(chatterId);
            if (chatter == null || !chatter || chatter.Data == null || chatter.Data.Disconnected || !chatter.IsAlive())
                return;

            var playerState = PlayerState.GetByPlayerId(chatterId);
            if (playerState == null || playerState.IsDead) return;

            playerState.DeathReason = CustomDeathReason.Suicide;
            chatter.SetRealKiller(chatter);
            MeetingVoteManager.ResetVoteManager(chatterId);

            if (!chatter.IsModClient() && !chatter.AmOwner) chatter.RpcMeetingKill(chatter);
            CustomRoleManager.OnMurderPlayer(chatter, chatter);

            _ = new LateTask(() => ChatManager.OnDisconnectOrDeadPlayer(chatterId), 0.1f, "チャッター死亡チャット同期");
        }, Main.LagTime, "ChatterKill");

        UtilsGameLog.AddGameLog("Chatter", $"{UtilsName.GetPlayerColor(Player)} 沈黙に耐えられず死んじゃった！");
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

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
public static class Chatter_AddChat_Patch
{
    public static void Postfix(PlayerControl sourcePlayer, string chatText)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInGame || !GameStates.IsMeeting) return;
        if (sourcePlayer == null || !sourcePlayer) return;
        if (!sourcePlayer.IsAlive()) return;
        if (sourcePlayer.GetRoleClass() is not Chatter chatter) return;

        // ★ /cmd を含むメッセージはコマンドなのでリセットしない
        if (chatText != null && chatText.TrimStart().StartsWith("/cmd"))
            return;

        chatter.ResetTimer();
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
public static class Chatter_MeetingHud_Update_Patch
{
    public static void Postfix()
    {
        if (!GameStates.IsInGame || !GameStates.IsMeeting) return;
        if (PlayerControl.AllPlayerControls == null) return;

        foreach (var pc in PlayerControl.AllPlayerControls)
        {
            if (pc == null || !pc) continue;
            if (pc.GetRoleClass() is Chatter chatter)
            {
                chatter.UpdateMeetingTimer();
            }
        }
    }
}