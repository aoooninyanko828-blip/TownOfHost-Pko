using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class NiceTeleporter : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(NiceTeleporter),
            player => new NiceTeleporter(player),
            CustomRoles.NiceTeleporter,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            103500,
            SetupOptionItem,
            "mv",
            "#00FF00",
            (6, 8),
            from: From.SuperNewRoles
        );

    public NiceTeleporter(PlayerControl player) : base(RoleInfo, player)
    {
        TeleportCooldown = OptionTeleportCooldown.GetFloat();

        cooldownLeft = 0f;

        PetActionManager.Register(Player.PlayerId, OnPet);
    }

    static OptionItem OptionTeleportCooldown;
    static float TeleportCooldown;

    enum OptionName { NiceTeleporterTeleportCooldown }

    static void SetupOptionItem()
    {
        OptionTeleportCooldown = FloatOptionItem.Create(
            RoleInfo, 10, OptionName.NiceTeleporterTeleportCooldown,
            new(2.5f, 120f, 2.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    float cooldownLeft;

    void OnPet()
    {
        if (!Player.IsAlive()) return;
        if (!AmongUsClient.Instance.AmHost) return;

        if (cooldownLeft > 0f) return;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

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

    void SendRpc()
    {
        using var sender = CreateSender();
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        cooldownLeft = reader.ReadSingle();
    }
}