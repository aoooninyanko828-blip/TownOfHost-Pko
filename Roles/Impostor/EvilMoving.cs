/*using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

public sealed class EvilMoving : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(EvilMoving),
            player => new EvilMoving(player),
            CustomRoles.EvilMoving,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            126500,
            SetupOptionItem,
            "emv",
            OptionSort: (3, 16),
            from: From.SuperNewRoles
        );

    public EvilMoving(PlayerControl player) : base(RoleInfo, player)
    {
        KillCooldown = OptionKillCooldown.GetFloat();
        TeleportCooldown = OptionTeleportCooldown.GetFloat();

        markedPos = null;
        hasMarked = false;
        cooldownLeft = 0f;

        PetActionManager.Register(Player.PlayerId, OnPet);
    }

    static OptionItem OptionKillCooldown;
    static float KillCooldown;
    static OptionItem OptionTeleportCooldown;
    static float TeleportCooldown;

    enum OptionName
    {
        EvilMovingTeleportCooldown,
    }

    Vector2? markedPos;
    bool hasMarked;
    float cooldownLeft;

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown,
            new(0f, 180f, 0.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionTeleportCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.EvilMovingTeleportCooldown,
            new(2.5f, 120f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
    }

    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;

    void OnPet()
    {
        if (!Player.IsAlive()) return;
        if (!AmongUsClient.Instance.AmHost) return;

        if (!hasMarked)
        {
            markedPos = Player.transform.position;
            hasMarked = true;
            cooldownLeft = TeleportCooldown;
            SendRpc();
            Player.MarkDirtySettings();
            Player.RpcResetAbilityCooldown(Sync: true);
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
            Utils.SendMessage(
                $"<color=#ff4444>ワープ先を設定しました！</color>",
                Player.PlayerId);
            return;
        }

        if (cooldownLeft > 0f) return;

        if (markedPos.HasValue)
        {
            var capturedPos = markedPos.Value;
            cooldownLeft = TeleportCooldown;
            SendRpc();
            Player.MarkDirtySettings();
            Player.RpcResetAbilityCooldown(Sync: true);

            _ = new LateTask(() =>
            {
                if (!Player.IsAlive()) return;
                Player.RpcSnapToForced(capturedPos);
                UtilsGameLog.AddGameLog("EvilMoving",
                    $"{UtilsName.GetPlayerColor(Player)} がワープした → {capturedPos}");
            }, 0.5f, $"EvilMoving.Warp.{Player.PlayerId}", true);
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        if (!hasMarked || cooldownLeft <= 0f) return;

        float prev = cooldownLeft;
        cooldownLeft -= Time.fixedDeltaTime;
        if (cooldownLeft < 0f) cooldownLeft = 0f;

        if (Mathf.FloorToInt(prev) != Mathf.FloorToInt(cooldownLeft))
        {
            Player.MarkDirtySettings();
            SendRpc();
        }
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = cooldownLeft > 0f ? cooldownLeft : TeleportCooldown;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        if (isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (!hasMarked)
            return $"{size}<color={color}>ペットを撫でてワープ先を設定</color>";
        if (cooldownLeft > 0f)
            return $"{size}<color=#888888>ワープCD: {Mathf.CeilToInt(cooldownLeft)}s</color>";
        return $"{size}<color={color}>ペットを撫でてワープ！</color>";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(hasMarked);
        sender.Writer.Write(cooldownLeft);
        sender.Writer.Write(markedPos.HasValue);
        if (markedPos.HasValue)
        {
            sender.Writer.Write(markedPos.Value.x);
            sender.Writer.Write(markedPos.Value.y);
        }
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        hasMarked = reader.ReadBoolean();
        cooldownLeft = reader.ReadSingle();
        bool hasPos = reader.ReadBoolean();
        markedPos = hasPos
            ? new Vector2(reader.ReadSingle(), reader.ReadSingle())
            : null;
    }
}*/