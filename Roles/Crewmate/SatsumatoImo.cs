using AmongUs.GameOptions;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Madmate;

public sealed class SatsumatoImo : RoleBase
{
    internal static bool IsSpecialMeetingNoSwap()
    {
        if (Roles.Crewmate.Balancer.Id != byte.MaxValue
            || (Roles.Crewmate.Balancer.target1 != byte.MaxValue
                && Roles.Crewmate.Balancer.target2 != byte.MaxValue))
        {
            return true;
        }

        if (Roles.Crewmate.Nimrod.IsExecutionMeeting())
        {
            return true;
        }

        var assassinState = Roles.Impostor.Assassin.assassin?.NowState;
        if (assassinState is Roles.Impostor.Assassin.AssassinMeeting.Guessing
            or Roles.Impostor.Assassin.AssassinMeeting.Collected
            or Roles.Impostor.Assassin.AssassinMeeting.DieWait)
        {
            return true;
        }

        return false;
    }

    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(SatsumatoImo),
            player => new SatsumatoImo(player),
            CustomRoles.SatsumatoImo,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            70640,
            SetupOptionItem,
            "si",
            "#990044",
            (0, 0),
            introSound: () => GetIntroSound(RoleTypes.Impostor)
        );

    public SatsumatoImo(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
    }

    static void SetupOptionItem()
    {
        HideRoleOptions(CustomRoles.SatsumatoImoC);
        HideRoleOptions(CustomRoles.SatsumatoImoM);
    }

    internal static void HideRoleOptions(CustomRoles role)
    {
        if (Options.CustomRoleSpawnChances != null &&
            Options.CustomRoleSpawnChances.TryGetValue(role, out var spawnOption))
        {
            spawnOption.SetHidden(true);
        }

        if (Options.CustomRoleCounts != null &&
            Options.CustomRoleCounts.TryGetValue(role, out var countOption))
        {
            countOption.SetHidden(true);
        }
    }
}

public sealed class SatsumatoImoC : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(SatsumatoImoC),
            player => new SatsumatoImoC(player),
            CustomRoles.SatsumatoImoC,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            70650,
            SetupOptionItem,
            "si",
            "#990044",
            (8, 1),
            assignInfo: new RoleAssignInfo(CustomRoles.SatsumatoImoC, CustomRoleTypes.Crewmate)
            {
                IsInitiallyAssignableCallBack = () => false,
                AssignCountRule = new(0, 0, 1)
            }
        );

    public SatsumatoImoC(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
    }
    bool skipSwapForThisMeeting;

    static void SetupOptionItem()
    {
        SatsumatoImo.HideRoleOptions(CustomRoles.SatsumatoImoC);
    }

    public override void OnStartMeeting()
    {
        skipSwapForThisMeeting = SatsumatoImo.IsSpecialMeetingNoSwap();
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (skipSwapForThisMeeting)
        {
            skipSwapForThisMeeting = false;
            return;
        }
        skipSwapForThisMeeting = false;
        Player.RpcSetCustomRole(CustomRoles.SatsumatoImoM, log: null);
    }
}

public sealed class SatsumatoImoM : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(SatsumatoImoM),
            player => new SatsumatoImoM(player),
            CustomRoles.SatsumatoImoM,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            70660,
            SetupOptionItem,
            "si",
            "#990044",
            (8, 2),
            assignInfo: new RoleAssignInfo(CustomRoles.SatsumatoImoM, CustomRoleTypes.Madmate)
            {
                IsInitiallyAssignableCallBack = () => false,
                AssignCountRule = new(0, 0, 1)
            },
            introSound: () => GetIntroSound(RoleTypes.Impostor)
        );

    public SatsumatoImoM(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
    }
    bool skipSwapForThisMeeting;

    static void SetupOptionItem()
    {
        SatsumatoImo.HideRoleOptions(CustomRoles.SatsumatoImoM);
    }

    public override void OnStartMeeting()
    {
        skipSwapForThisMeeting = SatsumatoImo.IsSpecialMeetingNoSwap();
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (skipSwapForThisMeeting)
        {
            skipSwapForThisMeeting = false;
            return;
        }
        skipSwapForThisMeeting = false;
        Player.RpcSetCustomRole(CustomRoles.SatsumatoImoC, log: null);
    }
}