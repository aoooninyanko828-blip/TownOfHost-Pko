using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Impostor;

public sealed class Slugger : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Slugger),
            player => new Slugger(player),
            CustomRoles.Slugger,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            76350,
            SetUpOptionItem,
            "slg",
            OptionSort: (3, 13),
            from: From.SuperNewRoles
            
        );

    public Slugger(PlayerControl player)
        : base(RoleInfo, player)
    {
        KillCooldown = OptionKillCooldown.GetFloat();
        Cooldown = OptionCooldown.GetFloat();
        ChargeTime = OptionChargeTime.GetFloat();
        SwingTime = OptionSwingTime.GetFloat();
        KillRange = OptionKillRange.GetFloat();
        MultiKill = OptionMultiKill.GetBool();
        FlyDistance = OptionFlyDistance.GetFloat();

        IsCharging = false;
        IsSwinging = false;
        chargeTimer = 0f;
        swingTimer = 0f;
        IsFiring = false;
        SwingFacingLeft = false;
    }

    // ★ 状態
    public bool IsCharging;
    public bool IsSwinging;
    public bool SwingFacingLeft;
    private float chargeTimer;
    private float swingTimer;
    private bool IsFiring;
    private float PlayerSpeed;
    private bool spawnCooldownStarted = false;

    // ★ オプション
    private static OptionItem OptionKillCooldown;
    private static float KillCooldown;
    private static OptionItem OptionCooldown;
    private static float Cooldown;
    private static OptionItem OptionChargeTime;
    private static float ChargeTime;
    private static OptionItem OptionSwingTime;
    private static float SwingTime;
    private static OptionItem OptionKillRange;
    private static float KillRange;
    private static OptionItem OptionMultiKill;
    private static bool MultiKill;
    private static OptionItem OptionFlyDistance;
    private static float FlyDistance;

    private enum OptionName
    {
        SluggerChargeTime,
        SluggerSwingTime,
        SluggerKillRange,
        SluggerMultiKill,
        SluggerFlyDistance,
    }

    private static void SetUpOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown,
            new(0f, 180f, 0.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.Cooldown,
            OptionBaseCoolTime, 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionChargeTime = FloatOptionItem.Create(RoleInfo, 12, OptionName.SluggerChargeTime,
            new(0.5f, 5f, 0.5f), 1.5f, false).SetValueFormat(OptionFormat.Seconds);
        OptionSwingTime = FloatOptionItem.Create(RoleInfo, 13, OptionName.SluggerSwingTime,
            new(0.1f, 2f, 0.1f), 0.5f, false).SetValueFormat(OptionFormat.Seconds);
        OptionKillRange = FloatOptionItem.Create(RoleInfo, 14, OptionName.SluggerKillRange,
            new(0.5f, 5f, 0.25f), 2f, false);
        OptionMultiKill = BooleanOptionItem.Create(RoleInfo, 15, OptionName.SluggerMultiKill,
            false, false);
        OptionFlyDistance = FloatOptionItem.Create(RoleInfo, 16, OptionName.SluggerFlyDistance,
            new(1f, 20f, 1f), 10f, false);
    }

    // ======== 基本設定 ========

    public float CalculateKillCooldown() => KillCooldown;

    public override void Add()
    {
        PlayerSpeed = Main.AllPlayerSpeed[Player.PlayerId];
        spawnCooldownStarted = false;
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = Cooldown;
    }

    public override bool CanUseAbilityButton() => true;
    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => true;

    // ======== ファントムボタン押下 ========

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        if (IsFiring || !Player.IsAlive()) return;

        IsFiring = true;
        IsCharging = true;
        chargeTimer = 0f;

        // チャージ中は動けない
        Main.AllPlayerSpeed[Player.PlayerId] = Main.MinSpeed;
        Player.MarkDirtySettings();

        // キルクールを長くして誤発防止
        Main.AllPlayerKillCooldown[Player.PlayerId] = 60f;
        Player.SetKillCooldown(60f);
        _ = new LateTask(() => Player.SyncSettings(), 0.1f, "SluggerKillTimer", true);
        Player.SyncSettings();

        // 全員にキルフラッシュ（音代わり）
        Utils.AllPlayerKillFlash();

        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRpc();
    }

    // ======== 毎フレーム処理 ========

    public override void OnFixedUpdate(PlayerControl player)
    {
        // スポーン直後のクールダウン初期化
        if (!spawnCooldownStarted && Player.IsAlive()
            && !GameStates.Intro && GameStates.IsInTask && !GameStates.IsMeeting)
        {
            spawnCooldownStarted = true;
            Player.RpcResetAbilityCooldown(Sync: true);
        }

        // 会議中はリセット
        if (MeetingHud.Instance != null)
        {
            if (IsCharging || IsSwinging) ResetState();
            return;
        }

        // 死亡時はリセット
        if (!Player.IsAlive() && (IsCharging || IsSwinging))
        {
            ResetState();
            return;
        }

        if (!AmongUsClient.Instance.AmHost) return;

        // ★ チャージ中
        if (IsCharging)
        {
            chargeTimer += Time.fixedDeltaTime;
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);

            if (chargeTimer >= ChargeTime)
            {
                // チャージ完了 → 振り抜き開始
                IsCharging = false;
                IsSwinging = true;
                swingTimer = 0f;
                SwingFacingLeft = Player.cosmetics.FlipX;

                Utils.AllPlayerKillFlash();
                UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
                SendRpc();
            }
        }

        // ★ 振り抜き中
        if (IsSwinging)
        {
            swingTimer += Time.fixedDeltaTime;
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);

            if (swingTimer >= SwingTime)
            {
                // 振り抜き完了 → 当たり判定
                ApplySwingHit();
                ResetState();
            }
        }
    }

    // ======== 当たり判定 ========

    private void ApplySwingHit()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;

        var myPos = (Vector2)Player.GetTruePosition();
        Vector2 swingDir = SwingFacingLeft ? Vector2.left : Vector2.right;
        bool hitAny = false;

        foreach (var target in PlayerCatch.AllAlivePlayerControls)
        {
            if (target.PlayerId == Player.PlayerId) continue;

            var toTarget = (Vector2)target.GetTruePosition() - myPos;

            // ★ 前方判定（向いている方向の前方90度以内）
            if (Vector2.Dot(toTarget.normalized, swingDir) < 0.3f) continue;

            // ★ 距離判定
            if (toTarget.magnitude > KillRange) continue;

            // ★ 吹き飛ばし位置を計算（壁考慮）
            var flyPos = CalcFlyPosition(target.GetTruePosition(), swingDir);

            // ★ ワープで吹き飛ばし
            target.NetTransform.SnapTo(flyPos);

            // ★ 少し待ってからキル（スプライトが飛んでいく演出）
            var t = target;
            _ = new LateTask(() =>
            {
                if (PlayerState.GetByPlayerId(t.PlayerId).IsDead) return;
                PlayerState.GetByPlayerId(t.PlayerId).DeathReason = CustomDeathReason.Hit;
                t.RpcExileV3();
                PlayerState.GetByPlayerId(t.PlayerId).SetDead();
                t.SetRealKiller(Player, true);
                UtilsGameLog.AddGameLog("Slugger",
                    $"<color=#ff6600>【スラッガー】</color> {UtilsName.GetPlayerColor(Player, true)} ═> {UtilsName.GetPlayerColor(t, true)}");
            }, 0.25f, "SluggerKill_" + target.PlayerId, true);

            hitAny = true;
            if (!MultiKill) break;
        }
    }

    // ★ 壁を考慮した吹き飛ばし位置
    private Vector2 CalcFlyPosition(Vector2 startPos, Vector2 dir)
    {
        // 【変更前】 Constants.ShadowAndShipLayerMask
        // 【変更後】 Constants.ShipOnlyMask に書き換え
        var hit = Physics2D.Raycast(startPos + dir * 0.3f, dir, FlyDistance,
            Constants.ShipOnlyMask);

        if (hit.collider != null)
            return hit.point - dir * 0.3f;
        return startPos + dir * FlyDistance;
    }

    // ======== 状態リセット ========

    private void ResetState()
    {
        IsCharging = false;
        IsSwinging = false;
        IsFiring = false;
        chargeTimer = 0f;
        swingTimer = 0f;

        Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
        Player.MarkDirtySettings();
        Player.SyncSettings();

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            Main.AllPlayerKillCooldown[Player.PlayerId] = KillCooldown;
            Player.SetKillCooldown(KillCooldown);
            AURoleOptions.PhantomCooldown = Cooldown;
            Player.RpcResetAbilityCooldown(Sync: true);
        }, 0.2f, "SluggerReset", true);

        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRpc();
    }

    // ======== イベント ========

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (IsCharging || IsSwinging) ResetState();
    }

    public override void OnStartMeeting()
    {
        if (IsCharging || IsSwinging) ResetState();
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;
        AURoleOptions.PhantomCooldown = Cooldown;
        Player.RpcResetAbilityCooldown();
    }

    // ======== 名前表示（第三者にも見える） ========

    public override bool GetTemporaryName(ref string name, ref bool NoMarker,
        bool isForMeeting, PlayerControl seer, PlayerControl seen = null)
    {
        seen ??= seer;
        if (!Player.IsAlive() || isForMeeting) return false;
        if (seen.PlayerId != Player.PlayerId) return false; // スラッガー自身の名前を変更
        if (!IsCharging && !IsSwinging) return false;

        // スラッガーの生名前（タグなし）
        string rawName = Main.AllPlayerNames.TryGetValue(Player.PlayerId, out var n)
            ? n.RemoveHtmlTags() : Player.Data.PlayerName.RemoveHtmlTags();

        bool facingLeft = (seer.PlayerId == Player.PlayerId)
            ? Player.cosmetics.FlipX
            : SwingFacingLeft;

        if (IsCharging)
        {
            // ★ チャージ中：名前をゆっくり持ち上げる（rotate 0→80）
            float progress = Mathf.Clamp01(chargeTimer / ChargeTime);
            int angle = (int)Mathf.Lerp(0f, 80f, progress);
            // 左向きなら反転
            if (facingLeft) angle = -angle;

            name = $"<line-height=600%>\n<size=400%><rotate={angle}>" +
                   $"<color=#ff6600>{rawName}</color></rotate></size>";
            NoMarker = true;
            return true;
        }

        if (IsSwinging)
        {
            // ★ 振り抜き中：大きく回転（80→-90）
            float progress = Mathf.Clamp01(swingTimer / SwingTime);
            int angle = (int)Mathf.Lerp(80f, -90f, progress);
            if (facingLeft) angle = -angle;

            // 振り抜き時は大きく・赤く
            name = $"<line-height=900%>\n<size=700%><rotate={angle}>" +
                   $"<color=#ff2200><b>{rawName}</b></color></rotate></size>";
            NoMarker = true;
            return true;
        }

        return false;
    }

    // ======== 下部テキスト ========

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || isForMeeting || !Player.IsAlive()) return "";

        string size = isForHud ? "" : "<size=60%>";
        if (!IsCharging && !IsSwinging)
            return $"{size}<color=#ff6600>ファントムボタン → ハリセンチャージ開始</color>";
        if (IsCharging)
        {
            float rem = Mathf.Max(0f, ChargeTime - chargeTimer);
            return $"{size}<color=#ff6600>チャージ中... {rem:F1}s ／ 構えを維持！</color>";
        }
        return $"{size}<color=#ff2200><b>振り抜き！！</b></color>";
    }

    public override string GetAbilityButtonText() => GetString("SluggerAbilityText");

    // ======== RPC ========

    public void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(IsCharging);
        sender.Writer.Write(IsSwinging);
        sender.Writer.Write(SwingFacingLeft);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        IsCharging = reader.ReadBoolean();
        IsSwinging = reader.ReadBoolean();
        SwingFacingLeft = reader.ReadBoolean();
    }
}