using UnityEngine;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using System.Collections.Generic;
using TownOfHost.Modules;
using TownOfHost.Patches;

namespace TownOfHost.Roles.Crewmate;

public sealed class Hitchhiker : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Hitchhiker),
            player => new Hitchhiker(player),
            CustomRoles.Hitchhiker,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            90100,
            SetupOptionItem,
            "hi",
            "#A0522D",
            (7, 1),
            from: From.TownOfHost_Pko
        );

    public Hitchhiker(PlayerControl player)
        : base(RoleInfo, player)
    {
        SpeedMultiplier = OptionSpeed.GetFloat();
        CooldownTimerLimit = OptionCooldown.GetFloat();
        DurationLimit = OptionDuration.GetFloat();
        targetSpeedUp = OptionTargetSpeedUp.GetBool();
        targetSpeedMultiplier = OptionTargetSpeedMultiplier.GetFloat();
        CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
    }

    public override void OnDestroy()
    {
        RestoreMySpeed();
        ReleaseTarget();
        pendingTarget = null;
        PetActionManager.Unregister(Player.PlayerId);
        CustomRoleManager.LowerOthers.Remove(GetLowerTextOthers);
    }

    static OptionItem OptionSpeed;
    static OptionItem OptionCooldown;
    static OptionItem OptionDuration;
    static OptionItem OptionTargetSpeedUp;
    static OptionItem OptionTargetSpeedMultiplier;

    enum OptionName
    {
        HitchhikerSpeed,
        HitchhikerCooldown,
        HitchhikerDuration,
        HitchhikerTargetSpeedUp,
        HitchhikerTargetSpeedMultiplier,
    }

    private PlayerControl TargetPlayer;
    private PlayerControl pendingTarget;
    private float abilityTimer;
    private float currentCooldown;
    private float petAttachTimer;
    private float SpeedMultiplier;
    private float CooldownTimerLimit;
    private float DurationLimit;
    private bool targetSpeedUp;
    private float targetSpeedMultiplier;
    private int snapFrame = 0;
    private bool speedApplied = false;

    public static void SetupOptionItem()
    {
        OptionSpeed = FloatOptionItem.Create(RoleInfo, 10, OptionName.HitchhikerSpeed, new(0.1f, 0.9f, 0.1f), 0.5f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.HitchhikerCooldown, new(0f, 60f, 1f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDuration = FloatOptionItem.Create(RoleInfo, 12, OptionName.HitchhikerDuration, new(0f, 120f, 1f), 0f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionTargetSpeedUp = BooleanOptionItem.Create(RoleInfo, 13, OptionName.HitchhikerTargetSpeedUp, false, false);
        OptionTargetSpeedMultiplier = FloatOptionItem.Create(RoleInfo, 14, OptionName.HitchhikerTargetSpeedMultiplier, new(1.1f, 3f, 0.1f), 1.5f, false, OptionTargetSpeedUp)
            .SetValueFormat(OptionFormat.Multiplier);
    }

    public override void Add()
    {
        currentCooldown = CooldownTimerLimit;
        TargetPlayer = null;
        pendingTarget = null;
        abilityTimer = 0f;
        petAttachTimer = 0f;
        speedApplied = false;
        Main.AllPlayerSpeed[Player.PlayerId] *= SpeedMultiplier;
        speedApplied = true;

        PetActionManager.Register(Player.PlayerId, OnPetAction);
    }

    void RestoreMySpeed()
    {
        if (!speedApplied) return;
        Main.AllPlayerSpeed[Player.PlayerId] /= SpeedMultiplier;
        speedApplied = false;
        Player.MarkDirtySettings();
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = currentCooldown > 0f ? currentCooldown : CooldownTimerLimit;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override bool CanClickUseVentButton => false;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;

    private void OnPetAction()
    {
        if (!Player.IsAlive() || currentCooldown > 0f) return;

        if (TargetPlayer != null)
        {
            // ★ ターゲットが移動制限中に解除しようとしたら自殺
            if (IsTargetOnRestrictedMove(TargetPlayer))
            {
                PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = CustomDeathReason.Suicide;
                Player.SetRealKiller(Player);
                Player.RpcExileV3();
                PlayerState.GetByPlayerId(Player.PlayerId).SetDead();
                ReleaseTarget();
                SendRpc();
                UtilsGameLog.AddGameLog("Hitchhiker",
                    $"{UtilsName.GetPlayerColor(Player)} が移動制限中に離脱しようとして自滅した");
                return;
            }

            ReleaseTarget();
            currentCooldown = CooldownTimerLimit;
            Player.MarkDirtySettings();
            Player.RpcResetAbilityCooldown(Sync: true);
            SendRpc();
            return;
        }

        float nearestDistance = Main.NormalOptions.KillDistance switch
        {
            0 => 1.0f,
            2 => 2.5f,
            _ => 1.8f,
        };

        PlayerControl closest = null;
        float minDist = float.MaxValue;

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            if (pc.PlayerId == Player.PlayerId) continue;
            float dist = Vector2.Distance(Player.GetTruePosition(), pc.GetTruePosition());
            if (dist <= nearestDistance && dist < minDist)
            {
                closest = pc;
                minDist = dist;
            }
        }

        if (closest != null)
        {
            pendingTarget = closest;
            petAttachTimer = 0.5f;
            UtilsNotifyRoles.NotifyRoles();
        }
    }

    void AttachToPlayer(PlayerControl target)
    {
        TargetPlayer = target;
        abilityTimer = DurationLimit;
        Player.petting = false;

        PlayerState.GetByPlayerId(Player.PlayerId).CanMove = false;
        Player.MarkDirtySettings();

        if (targetSpeedUp)
            Main.AllPlayerSpeed[target.PlayerId] *= targetSpeedMultiplier;

        UtilsGameLog.AddGameLog("Hitchhiker",
            $"{UtilsName.GetPlayerColor(Player)} が {UtilsName.GetPlayerColor(target)} にくっついた");
        UtilsNotifyRoles.NotifyRoles();
    }

    void ReleaseTarget()
    {
        if (TargetPlayer == null) return;

        PlayerState.GetByPlayerId(Player.PlayerId).CanMove = true;
        Player.MarkDirtySettings();

        if (targetSpeedUp)
            Main.AllPlayerSpeed[TargetPlayer.PlayerId] /= targetSpeedMultiplier;

        UtilsGameLog.AddGameLog("Hitchhiker",
            $"{UtilsName.GetPlayerColor(Player)} が {UtilsName.GetPlayerColor(TargetPlayer)} から離れた");
        TargetPlayer = null;
        pendingTarget = null;
        UtilsNotifyRoles.NotifyRoles();
    }

    static bool IsTargetOnRestrictedMove(PlayerControl target)
    {
        if (target == null) return false;
        if (target.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) return true;
        if (target.onLadder) return true;
        if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship &&
            Vector2.Distance(target.GetTruePosition(), new Vector2(7.76f, 8.56f)) <= 1.9f) return true;
        if (target.inMovingPlat) return true;
        return false;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInTask) return;

        if (currentCooldown > 0f)
        {
            float prev = currentCooldown;
            currentCooldown -= Time.fixedDeltaTime;
            if (currentCooldown <= 0f) currentCooldown = 0f;

            if (Mathf.FloorToInt(prev) != Mathf.FloorToInt(currentCooldown))
            {
                Player.MarkDirtySettings();
                SendRpc();
            }
        }

        if (pendingTarget != null)
        {
            if (!Player.IsAlive() || !pendingTarget.IsAlive())
            {
                pendingTarget = null;
                UtilsNotifyRoles.NotifyRoles();
            }
            else
            {
                petAttachTimer -= Time.fixedDeltaTime;
                if (petAttachTimer <= 0f)
                {
                    AttachToPlayer(pendingTarget);
                    pendingTarget = null;
                    currentCooldown = 1f;
                    Player.MarkDirtySettings();
                    Player.RpcResetAbilityCooldown(Sync: true);
                    SendRpc();
                }
            }
        }

        if (TargetPlayer != null)
        {
            if (!TargetPlayer.IsAlive() || !Player.IsAlive())
            {
                ReleaseTarget();
                currentCooldown = CooldownTimerLimit;
                Player.MarkDirtySettings();
                Player.RpcResetAbilityCooldown(Sync: true);
                SendRpc();
                return;
            }

            if (DurationLimit > 0f)
            {
                // ★ ターゲットが梯子/ジップライン/ヌーン使用中はタイマーを止める
                if (!IsTargetOnRestrictedMove(TargetPlayer))
                {
                    abilityTimer -= Time.fixedDeltaTime;
                }

                if (abilityTimer <= 0f)
                {
                    ReleaseTarget();
                    currentCooldown = CooldownTimerLimit;
                    Player.MarkDirtySettings();
                    Player.RpcResetAbilityCooldown(Sync: true);
                    SendRpc();
                    return;
                }
            }

            snapFrame++;
            if (snapFrame % 3 == 0)
            {
                var targetPos = TargetPlayer.transform.position;
                Player.NetTransform.SnapTo(targetPos);

                ushort sid = (ushort)(Player.NetTransform.lastSequenceId + 2U);
                var writer = AmongUsClient.Instance.StartRpcImmediately(
                    Player.NetTransform.NetId, (byte)RpcCalls.SnapTo, SendOption.None);
                NetHelpers.WriteVector2(targetPos, writer);
                writer.Write(sid);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
        }
    }

    // ★ 死亡時に速度を戻す
    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        RestoreMySpeed();
        ReleaseTarget();
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        ReleaseTarget();
        pendingTarget = null;
    }

    public override void OnStartMeeting()
    {
        ReleaseTarget();
        pendingTarget = null;
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        currentCooldown = CooldownTimerLimit;
        pendingTarget = null;
        Player.MarkDirtySettings();
        Player.RpcResetAbilityCooldown(Sync: true);
        UtilsNotifyRoles.NotifyRoles();
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive() || isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (TargetPlayer != null)
            return $"{size}<color={color}>ヒッチハイク中...</color>";

        if (pendingTarget != null)
            return $"{size}<color={color}>乗車準備中...</color>";

        if (currentCooldown > 0f)
            return $"{size}<color=#888888>クールダウン中</color>";

        return $"{size}<color={color}>ペットを撫でる → 近くのプレイヤーに乗る</color>";
    }

    public string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting || !Player.IsAlive()) return "";

        if (TargetPlayer != null && seer.PlayerId == TargetPlayer.PlayerId && seen.PlayerId == TargetPlayer.PlayerId)
        {
            string size = isForHud ? "" : "<size=60%>";
            string color = RoleInfo.RoleColorCode;
            return $"\n{size}<color={color}>ヒッチハイカーが乗車中...</color>";
        }

        return "";
    }

    public override bool CanUseAbilityButton() => true;

    public override string GetAbilityButtonText()
    {
        if (currentCooldown > 0f) return Mathf.CeilToInt(currentCooldown).ToString();
        if (pendingTarget != null) return Mathf.CeilToInt(petAttachTimer).ToString();
        return TargetPlayer != null ? "降車" : "乗車";
    }

    public override bool OverrideAbilityButton(out string text)
    {
        text = "Vent";
        return true;
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(TargetPlayer?.PlayerId ?? byte.MaxValue);
        sender.Writer.Write(abilityTimer);
        sender.Writer.Write(currentCooldown);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        byte targetId = reader.ReadByte();
        abilityTimer = reader.ReadSingle();
        currentCooldown = reader.ReadSingle();

        if (targetId == byte.MaxValue)
            TargetPlayer = null;
        else
        {
            TargetPlayer = PlayerCatch.GetPlayerById(targetId);
            Player.petting = false;
        }
    }
}