using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

public sealed class Samurai : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Samurai),
            player => new Samurai(player),
            CustomRoles.Samurai,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            87100,
            SetupOptionItem,
            "sam",
            "#ff1919",
            (3, 16),
            introSound: () => GetIntroSound(RoleTypes.Shapeshifter),
            from: From.SuperNewRoles
        );

    static OptionItem OptionKillCooldown;
    static OptionItem OptionAbilityCooldown;
    static OptionItem OptionCanVent;
    static OptionItem OptionCanSabotage;
    static OptionItem OptionSlashRange;

    static float KillCooldown => OptionKillCooldown?.GetFloat() ?? 30f;
    static float AbilityCooldown => OptionAbilityCooldown?.GetFloat() ?? 30f;
    static bool CanVent => OptionCanVent?.GetBool() ?? true;
    static bool CanSabotage => OptionCanSabotage?.GetBool() ?? true;
    static float SlashRange => OptionSlashRange?.GetFloat() ?? 2f;

    enum OptionName
    {
        SamuraiSlashRange
    }

    public Samurai(PlayerControl player)
        : base(RoleInfo, player)
    {
    }

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionAbilityCooldown = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.Cooldown, OptionBaseCoolTime, 25f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.CanVent, true, false);
        OptionCanSabotage = BooleanOptionItem.Create(RoleInfo, 13, GeneralOption.CanUseSabotage, true, false);
        OptionSlashRange = FloatOptionItem.Create(RoleInfo, 14, OptionName.SamuraiSlashRange, new(0.5f, 6f, 0.25f), 2f, false)
            .SetValueFormat(OptionFormat.Multiplier);
    }

    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => CanSabotage;
    public bool CanUseImpostorVentButton() => CanVent;

    public override bool CanClickUseVentButton => CanVent;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => CanVent;
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => CanVent;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = AbilityCooldown;
    }

    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        var killedCount = KillTargetsInFront();
        if (killedCount <= 0) return;

        ResetCooldown = true;
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    int KillTargetsInFront()
    {
        var origin = Player.GetTruePosition();
        var forward = Player.cosmetics.FlipX ? Vector2.left : Vector2.right;

        List<PlayerControl> targets =
            PlayerCatch.AllAlivePlayerControls
                .Where(target => target != null && target.PlayerId != Player.PlayerId)
                .Where(target => IsTargetInSlashArea(target, origin, forward))
                .OrderBy(target => Vector2.Distance(origin, target.GetTruePosition()))
                .ToList();

        if (targets.Count == 0) return 0;

        var killCount = 0;
        foreach (var target in targets)
        {
            if (!target.IsAlive()) continue;
            if (target.GetCustomRole().IsImpostor() && !SuddenDeathMode.NowSuddenDeathMode) continue;
            if (SuddenDeathMode.NowSuddenDeathTemeMode && SuddenDeathMode.IsSameteam(target.PlayerId, Player.PlayerId)) continue;

            if (CustomRoleManager.OnCheckMurder(Player, target, Player, target, true, Killpower: 1, deathReason: CustomDeathReason.Kill))
            {
                killCount++;
            }
        }

        return killCount;
    }

    bool IsTargetInSlashArea(PlayerControl target, Vector2 origin, Vector2 forward)
    {
        var offset = target.GetTruePosition() - origin;
        var distance = offset.magnitude;
        if (distance <= 0.01f || distance > SlashRange) return false;

        var forwardDot = Vector2.Dot(offset.normalized, forward);
        return forwardDot >= 0.35f;
    }

    public override string GetAbilityButtonText() => GetString("SamuraiAbilityButtonText");

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting) return "";
        if (seer.PlayerId != Player.PlayerId || seen.PlayerId != Player.PlayerId || !Player.IsAlive()) return "";

        if (isForHud) return GetString("SamuraiLowerText");
        return $"<size=50%>{GetString("SamuraiLowerText")}</size>";
    }
}