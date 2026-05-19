using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

public sealed class EvilMoving : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(EvilMoving),
            player => new EvilMoving(player),
            CustomRoles.EvilMoving,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            126500,
            SetupOptionItem,
            "emv",
            OptionSort: (2, 10),
            from: From.SuperNewRoles
        );

    public EvilMoving(PlayerControl player) : base(RoleInfo, player)
    {
        TeleportCooldown = OptionTeleportCooldown.GetFloat();
        markedPos = null;
        hasMarked = false;
        cooldownLeft = 0f;
        PetActionManager.Register(Player.PlayerId, OnPet);
    }

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
        OptionTeleportCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.EvilMovingTeleportCooldown,
            new(2.5f, 120f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
    }

    public float CalculateKillCooldown() => Main.AllPlayerKillCooldown.GetValueOrDefault(Player.PlayerId, Main.NormalOptions.KillCooldown);
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;
    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override void OnSpawn(bool initialState = false)
    {
        cooldownLeft = TeleportCooldown + 1.5f;
        Player.RpcResetAbilityCooldown(Sync: true);
    }

    public override void Add()
    {
        markedPos = null;
        hasMarked = false;
        cooldownLeft = TeleportCooldown;
        PetActionManager.Register(Player.PlayerId, OnPet);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = cooldownLeft > 0f ? cooldownLeft : 0.1f;
    }

    void OnPet()
    {
        if (!Player.IsAlive()) return;
        if (!AmongUsClient.Instance.AmHost) return;

        if (!hasMarked)
        {
            markedPos = Player.transform.position;
            hasMarked = true;
            cooldownLeft = TeleportCooldown;
            Player.RpcResetAbilityCooldown(Sync: true);
            SendRpc();
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
            Player.RpcResetAbilityCooldown(Sync: true);
            SendRpc();

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
}