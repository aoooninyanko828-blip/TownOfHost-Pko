using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;
public sealed class Sage : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Sage),
            player => new Sage(player),
            CustomRoles.Sage,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            60100,
            SetupOptionItem,
            "sg",
            "#aaddff",
            (5, 0),
            from: From.SuperNewRoles
        );

    public Sage(PlayerControl player)
        : base(RoleInfo, player)
    {
        BarrierDuration = OptionBarrierDuration.GetFloat();
        BarrierCooldown = OptionBarrierCooldown.GetFloat();
        isBarrierActive = false;
        barrierTimer = 0f;
        savedPosition = Vector2.zero;
    }

    static OptionItem OptionBarrierDuration;
    static float BarrierDuration;
    static OptionItem OptionBarrierCooldown;
    static float BarrierCooldown;

    bool isBarrierActive;
    float barrierTimer;
    Vector2 savedPosition; // バリア中の固定位置

    enum OptionName
    {
        SageBarrierDuration,
        SageBarrierCooldown,
    }

    static void SetupOptionItem()
    {
        OptionBarrierDuration = FloatOptionItem.Create(RoleInfo, 10, OptionName.SageBarrierDuration,
            new(1f, 15f, 0.5f), 5f, false).SetValueFormat(OptionFormat.Seconds);
        OptionBarrierCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.SageBarrierCooldown,
            new(2.5f, 60f, 2.5f), 20f, false).SetValueFormat(OptionFormat.Seconds);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = BarrierCooldown;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override bool CanClickUseVentButton => false;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;

    public void ActivateBarrier()
    {
        if (!Player.IsAlive()) return;
        if (isBarrierActive) return;

        isBarrierActive = true;
        barrierTimer = 0f;
        savedPosition = Player.GetTruePosition();

        Main.AllPlayerSpeed[Player.PlayerId] = 0f;
        Player.MarkDirtySettings();
        Player.SyncSettings();

        Utils.SendMessage(
            $"<color=#aaddff>【聖なるバリア】発動！\n{BarrierDuration}秒間キルを反射します。</color>",
            Player.PlayerId);

        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);

        Logger.Info($"{Player.Data.GetLogPlayerName()} がバリアを発動", "Sage");
    }

    // ★ バリア解除
    private void DeactivateBarrier(bool resetCooldown = true)
    {
        if (!isBarrierActive) return;
        isBarrierActive = false;
        barrierTimer = 0f;

        // ★ 速度を元に戻す
        Main.AllPlayerSpeed[Player.PlayerId] = Main.RealOptionsData?.GetFloat(FloatOptionNames.PlayerSpeedMod) ?? 1f;
        Player.MarkDirtySettings();
        Player.SyncSettings();

        if (resetCooldown)
        {
            // ★ CDリセット（ベントCD = バリアCD）
            AURoleOptions.EngineerCooldown = BarrierCooldown;
            Player.RpcResetAbilityCooldown();
        }

        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!isBarrierActive) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) { DeactivateBarrier(false); return; }

        barrierTimer += Time.fixedDeltaTime;

        // ★ バリア中は位置を固定
        var currentPos = Player.GetTruePosition();
        if (Vector2.Distance(currentPos, savedPosition) > 0.05f)
        {
            Player.NetTransform.SnapTo(savedPosition);
            ushort sid = (ushort)(Player.NetTransform.lastSequenceId + 2U);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                Player.NetTransform.NetId, (byte)RpcCalls.SnapTo, Hazel.SendOption.Reliable);
            NetHelpers.WriteVector2(savedPosition, writer);
            writer.Write(sid);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        // ★ 時間切れで解除
        if (barrierTimer >= BarrierDuration)
        {
            Utils.SendMessage(
                $"<color=#aaddff>【聖なるバリア】解除。</color>",
                Player.PlayerId);
            DeactivateBarrier();
        }
    }

    // ★ キルされそうになったとき → バリア中なら反射
    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        if (!isBarrierActive) return true; // バリアなし → 通常キル

        var killer = info.AttemptKiller;
        if (killer == null) return true;

        // ★ キルを防ぐ
        info.DoKill = false;

        Logger.Info($"{Player.Data.GetLogPlayerName()} がバリアでキルを反射！ → {killer.Data.GetLogPlayerName()}", "Sage");

        // ★ 攻撃者をキル
        _ = new LateTask(() =>
        {
            if (!killer.IsAlive()) return;
            PlayerState.GetByPlayerId(killer.PlayerId).DeathReason = CustomDeathReason.Counter;
            killer.RpcMurderPlayerV2(killer);
            UtilsGameLog.AddGameLog("Sage",
                $"{UtilsName.GetPlayerColor(Player)}のバリアが{UtilsName.GetPlayerColor(killer)}のキルを反射した");
        }, 0.1f, "Sage.ReflectKill", true);

        // ★ バリアは1回反射したら解除
        Utils.SendMessage(
            $"<color=#aaddff>【聖なるバリア】キルを反射しました！</color>",
            Player.PlayerId);
        DeactivateBarrier();

        return false; // キルをキャンセル
    }

    public override void OnStartMeeting()
    {
        if (isBarrierActive)
            DeactivateBarrier(false);
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        AURoleOptions.EngineerCooldown = BarrierCooldown;
        Player.RpcResetAbilityCooldown();
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        if (isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (isBarrierActive)
        {
            float remaining = Mathf.Max(0f, BarrierDuration - barrierTimer);
            return $"{size}<color={color}>【バリア発動中】{remaining:F1}s | 動けません</color>";
        }
        return $"{size}<color={color}>ペットなで → 聖なるバリア発動</color>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        if (isBarrierActive)
        {
            float remaining = Mathf.Max(0f, BarrierDuration - barrierTimer);
            return $"<color={RoleInfo.RoleColorCode}>({remaining:F1}s)</color>";
        }
        return "";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(isBarrierActive);
        sender.Writer.Write(barrierTimer);
        // ★ X座標とY座標を別々にFloatとして書き込む
        sender.Writer.Write(savedPosition.x);
        sender.Writer.Write(savedPosition.y);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        isBarrierActive = reader.ReadBoolean();
        barrierTimer = reader.ReadSingle();
        // ★ X座標とY座標を別々にFloatとして読み込み、Vector2を再構築する
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        savedPosition = new Vector2(x, y);
    }

    // ★ PetActionManagerへの登録はAdd()で行う
    public override void Add()
    {
        PetActionManager.Register(Player.PlayerId, ActivateBarrier);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }
}