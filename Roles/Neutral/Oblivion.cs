using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Neutral;

public sealed class Oblivion : RoleBase, IKillFlashSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Oblivion),
            player => new Oblivion(player),
            CustomRoles.Oblivion,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            30400,
            SetUpOptionItem,
            "ob",
            "#808080",
            (7, 2),
            true,
            from: From.SuperNewRoles
        );

    public Oblivion(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.False)
    {
        hasTransformed = false;
        pendingRoleId = byte.MaxValue;
    }

    bool hasTransformed;
    byte pendingRoleId;
    readonly Dictionary<byte, Vector2> deadBodyPositions = new();

    static void SetUpOptionItem() { }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }

    public override void OnDestroy()
    {
        ClearAllBodyArrows();
    }

    public override void OnStartMeeting()
    {
        ClearAllBodyArrows();
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        ClearAllBodyArrows();

        if (reporter == null || reporter.PlayerId != Player.PlayerId) return;
        if (hasTransformed) return;
        if (target == null) return;
        if (target.Disconnected) return;

        var deadPlayer = GetPlayerById(target.PlayerId);
        if (deadPlayer == null) return;

        var newRole = deadPlayer.GetCustomRole();
        if (newRole is CustomRoles.GM or CustomRoles.NotAssigned or CustomRoles.Oblivion) return;

        pendingRoleId = target.PlayerId;
        SendRPC();
    }

    public override void OnLeftPlayer(PlayerControl player)
    {
        if (player == null) return;
        RemoveDeadBodyArrow(player.PlayerId);
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        if (pendingRoleId == byte.MaxValue) return;

        var deadPlayer = GetPlayerById(pendingRoleId);
        pendingRoleId = byte.MaxValue;

        if (deadPlayer == null) return;

        var newRole = deadPlayer.GetCustomRole();
        if (newRole is CustomRoles.GM or CustomRoles.NotAssigned or CustomRoles.Oblivion) return;

        hasTransformed = true;
        TransformWithResync(newRole, deadPlayer);

        SendRPC();
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true, OnlyMeName: true, SpecifySeer: Player);
    }

    public bool? CheckKillFlash(MurderInfo info)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        if (!Player.IsAlive()) return false;
        if (hasTransformed) return false;

        var dead = info.AppearanceTarget;
        if (dead == null) return false;

        AddDeadBodyArrow(dead.PlayerId, dead.GetTruePosition());
        return false;
    }

    void TransformWithResync(CustomRoles newRole, PlayerControl deadPlayer)
    {
        var playerId = Player.PlayerId;

        if (!Utils.RoleSendList.Contains(playerId))
            Utils.RoleSendList.Add(playerId);

        Player.RpcSetCustomRole(newRole, log: null);
        ForceRoleSync(playerId, newRole, applyStartGameTasks: true, applyTaskStateSync: true);

        UtilsGameLog.AddGameLog(
            "Oblivion",
            $"{UtilsName.GetPlayerColor(Player)} transformed into {UtilsRoleText.GetRoleName(newRole)} by reporting {UtilsName.GetPlayerColor(deadPlayer)}"
        );

        Utils.SendMessage(
            string.Format(GetString("OblivionTransformed"), UtilsRoleText.GetRoleName(newRole)),
            Player.PlayerId
        );

        QueueRoleResync(playerId, newRole, 0.25f, 1);
        QueueRoleResync(playerId, newRole, 0.70f, 2);
        QueueRoleResync(playerId, newRole, 1.50f, 3);
    }

    static void QueueRoleResync(byte playerId, CustomRoles role, float delay, int index)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        _ = new LateTask(() =>
        {
            if (!AmongUsClient.Instance.AmHost) return;
            ForceRoleSync(playerId, role, applyStartGameTasks: false, applyTaskStateSync: false);
        }, delay, $"Oblivion.RoleResync.{playerId}.{index}", true);
    }

    static void ForceRoleSync(byte playerId, CustomRoles role, bool applyStartGameTasks, bool applyTaskStateSync)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        var player = GetPlayerById(playerId);
        if (player == null || player.Data == null) return;

        if (!Utils.RoleSendList.Contains(playerId))
            Utils.RoleSendList.Add(playerId);

        if (player.GetCustomRole() != role)
            player.RpcSetCustomRole(role, log: null);

        var roleClass = player.GetRoleClass();
        if (applyStartGameTasks)
            roleClass?.StartGameTasks();

        if (applyTaskStateSync)
            RefreshTaskStateAfterTransform(player);

        player.MarkDirtySettings();
        player.SyncSettings();
        player.ResetKillCooldown();
        player.SetKillCooldown(delay: true, force: true);
        player.RpcResetAbilityCooldown(Sync: true);

        if (applyTaskStateSync)
        {
            GameData.Instance?.RecomputeTaskCounts();
            GameManager.Instance?.CheckTaskCompletion();
        }

        UtilsNotifyRoles.NotifyRoles(ForceLoop: true, OnlyMeName: true, SpecifySeer: player);
    }

    static void RefreshTaskStateAfterTransform(PlayerControl player, bool allowRetry = true)
    {
        if (player == null) return;

        var state = PlayerState.GetByPlayerId(player.PlayerId);
        if (state == null) return;

        var taskState = state.GetTaskState();
        if (taskState == null) return;

        taskState.hasTasks = false;
        taskState.AllTasksCount = 0;
        taskState.NeedTaskCount = -1;
        taskState.CompletedTasksCount = 0;

        var shouldHaveTasks = ShouldHaveTasksAfterTransform(player, state);
        if (player.Data?.Tasks == null)
        {
            if (allowRetry && shouldHaveTasks)
                QueueTaskStateRefreshRetry(player.PlayerId);
            return;
        }

        state.InitTask(player);
        taskState = state.GetTaskState();
        if (taskState == null) return;

        if (!taskState.hasTasks)
        {
            if (allowRetry && shouldHaveTasks)
                QueueTaskStateRefreshRetry(player.PlayerId);
            return;
        }

        var tasks = player.Data.Tasks.ToArray();
        var completed = 0;
        for (var i = 0; i < tasks.Length; i++)
        {
            var task = tasks[i];
            if (task != null && task.Complete) completed++;
        }

        taskState.CompletedTasksCount = completed > taskState.AllTasksCount
            ? taskState.AllTasksCount
            : completed;
    }

    static bool ShouldHaveTasksAfterTransform(PlayerControl player, PlayerState state)
    {
        if (player == null || state == null) return false;

        var hasTasks = !state.MainRole.IsImpostor();
        var roleClass = player.GetRoleClass();
        if (roleClass != null)
        {
            hasTasks = roleClass.HasTasks switch
            {
                HasTask.True => true,
                HasTask.False => false,
                HasTask.ForRecompute => true,
                _ => hasTasks
            };
        }

        if (state.MainRole is CustomRoles.GM or CustomRoles.SKMadmate or CustomRoles.Jackaldoll)
            hasTasks = false;
        if (state.GhostRole is CustomRoles.AsistingAngel)
            hasTasks = false;

        return hasTasks;
    }

    static void QueueTaskStateRefreshRetry(byte playerId)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        _ = new LateTask(() =>
        {
            var player = GetPlayerById(playerId);
            if (player == null || player.Data == null) return;

            RefreshTaskStateAfterTransform(player, allowRetry: false);
            player.MarkDirtySettings();
            player.SyncSettings();
            GameData.Instance?.RecomputeTaskCounts();
            GameManager.Instance?.CheckTaskCompletion();
        }, 0.35f, $"Oblivion.TaskStateRetry.{playerId}", true);
    }
    void AddDeadBodyArrow(byte playerId, Vector2 pos)
    {
        if (deadBodyPositions.TryGetValue(playerId, out var oldPos))
            GetArrow.Remove(Player.PlayerId, oldPos);

        deadBodyPositions[playerId] = pos;
        GetArrow.Add(Player.PlayerId, pos);
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    void RemoveDeadBodyArrow(byte playerId)
    {
        if (!deadBodyPositions.TryGetValue(playerId, out var pos)) return;

        GetArrow.Remove(Player.PlayerId, pos);
        deadBodyPositions.Remove(playerId);
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    void ClearAllBodyArrows()
    {
        foreach (var pos in deadBodyPositions.Values)
            GetArrow.Remove(Player.PlayerId, pos);

        deadBodyPositions.Clear();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (isForMeeting) return "";
        if (!Player.IsAlive()) return "";
        if (!Is(seer) || !Is(seen)) return "";
        if (hasTransformed) return "";
        if (deadBodyPositions.Count == 0) return "";

        var arrows = "";
        foreach (var pos in deadBodyPositions.Values)
            arrows += GetArrow.GetArrows(seer, pos);

        return arrows == "" ? "" : $"<color=#808080>{arrows}</color>";
    }

    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(hasTransformed);
        sender.Writer.Write(pendingRoleId);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        hasTransformed = reader.ReadBoolean();
        pendingRoleId = reader.ReadByte();
    }

    public override string GetProgressText(bool comms = false, bool gameLog = false)
    {
        if (hasTransformed) return "";
        return "<color=#b0b0d0>(未変化)</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        if (hasTransformed) return "";

        if (pendingRoleId != byte.MaxValue)
            return $"{(isForHud ? "" : "<size=60%>")}<color=#808080>会議後に役職が変化する...</color>";
        return $"{(isForHud ? "" : "<size=60%>")}<color=#808080>死体をレポートすると役職が変化する</color>";
    }
}