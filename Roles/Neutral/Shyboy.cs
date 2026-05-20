using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using UnityEngine;
using TownOfHost.Roles.AddOns.Common;

namespace TownOfHost.Roles.Crewmate;

public sealed class Shyboy : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Shyboy),
            player => new Shyboy(player),
            CustomRoles.Shyboy,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Neutral,
            11900,
            SetupOptionItem,
            "Sy",
            "#00fa9a",
            (7, 0)
        );

    public Shyboy(PlayerControl player)
    : base(RoleInfo, player)
    {
        Notify = true;
        Shytime = OptionShytime.GetFloat();
        Notshy = OptionNotShy.GetFloat();
        ShowArrow = OptionShowArrow.GetBool();
        ArrowMatchColor = OptionArrowMatchColor.GetBool();
        ShowNearby = OptionShowNearby.GetBool();
        ShowNearbyRange = OptionShowNearbyRange.GetFloat();

        Shydeath = 0;
        AfterMeeting = 0;
    }

    private static float Shytime;
    private static OptionItem OptionShytime;
    private static float Notshy;
    private static OptionItem OptionNotShy;
    public static OptionItem OptionShyDieBom;

    public static OptionItem OptionShowArrow;
    public static OptionItem OptionArrowMatchColor;
    public static OptionItem OptionShowNearby;
    public static OptionItem OptionShowNearbyRange;

    float Shydeath;
    float Cool;
    float AfterMeeting;
    bool Notify;
    float Last;
    float Shydeathdi;

    bool ShowArrow;
    bool ArrowMatchColor;
    bool ShowNearby;
    float ShowNearbyRange;

    readonly HashSet<byte> registeredArrows = new();

    enum OptionName
    {
        ShyboyShytime,
        ShyboyAfterMeetingNotShytime,
        ShyboyBooooom,
        ShyboyShowArrow,
        ShyboyArrowMatchColor,
        ShyboyShowNearby,
        ShyboyShowNearbyRange,
    }

    public override bool CanClickUseVentButton => false;

    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 50);

        OptionShytime = FloatOptionItem.Create(RoleInfo, 10, OptionName.ShyboyShytime, new(0f, 15f, 0.5f), 5f, false);
        OptionNotShy = FloatOptionItem.Create(RoleInfo, 11, OptionName.ShyboyAfterMeetingNotShytime, new(0f, 30f, 1f), 10f, false);
        OptionShyDieBom = BooleanOptionItem.Create(RoleInfo, 12, OptionName.ShyboyBooooom, false, false)
            .SetInfo(GetString("AprilfoolOnly")).SetEnabled(() => Event.April || Event.Special);

        OptionShowArrow = BooleanOptionItem.Create(RoleInfo, 13, OptionName.ShyboyShowArrow, true, false);
        OptionArrowMatchColor = BooleanOptionItem.Create(RoleInfo, 14, OptionName.ShyboyArrowMatchColor, true, false, OptionShowArrow);

        OptionShowNearby = BooleanOptionItem.Create(RoleInfo, 15, OptionName.ShyboyShowNearby, true, false);
        OptionShowNearbyRange = FloatOptionItem.Create(RoleInfo, 16, OptionName.ShyboyShowNearbyRange, new(0.5f, 15f, 0.5f), 4.0f, false, OptionShowNearby);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        double Coold = Math.Round(Shytime + 1 / 4 - Shydeath);
        AURoleOptions.EngineerCooldown = (float)Coold;
        AURoleOptions.EngineerInVentMaxTime = 0;
    }

    public override void StartGameTasks()
    {
        Shydeathdi = Player.Is(CustomRoles.Lighting) ? Main.DefaultImpostorVision : Main.DefaultCrewmateVision;
        if (Player.Is(CustomRoles.Sunglasses))
            Shydeathdi *= Sunglasses.SunglassesVisionmagnification.GetFloat() * 0.01f;

        Shydeathdi *= 4.5f;
        Shydeathdi = Mathf.Min(Shydeathdi, 4);
    }

    public override void OnStartMeeting()
    {
        Notify = true;
        Shydeath = 0;
        AfterMeeting = 0;
        StartGameTasks();

        foreach (var id in registeredArrows)
            TargetArrow.Remove(Player.PlayerId, id);
        registeredArrows.Clear();
    }

    public override void OnDestroy()
    {
        foreach (var id in registeredArrows)
            TargetArrow.Remove(Player.PlayerId, id);
        registeredArrows.Clear();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.CalledMeeting || GameStates.ExiledAnimate || !MyState.HasSpawned) return;
        if (!Player.IsAlive()) return;

        Cool += Time.fixedDeltaTime;
        if (0.25 < Cool)
        {
            Cool = 0;
            var cooldown = (float)Math.Round(Shytime + 1 / 4 - Shydeath);
            if (Last != cooldown)
            {
                Last = cooldown;
                Player.MarkDirtySettings();
            }
            Player.RpcResetAbilityCooldown(log: false);
        }

        AfterMeeting += Time.fixedDeltaTime;

        if (GameStates.IsInTask && Notshy <= AfterMeeting - 5)
        {
            if (Notify)
            {
                Notify = false;
                Player.RpcProtectedMurderPlayer();
            }

            Vector2 GSpos = player.transform.position;
            bool Hito = false;
            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
            {
                if (pc != player)
                {
                    float HitoDistance = Vector2.Distance(GSpos, pc.transform.position);
                    var vector = (Vector2)pc.transform.position - GSpos;
                    float dis = vector.magnitude;
                    if (HitoDistance <= Shydeathdi && !PhysicsHelpers.AnyNonTriggersBetween(GSpos, pc.transform.position, dis, Constants.ShadowMask))
                    {
                        Hito = true;
                        break;
                    }
                }
            }

            if (Hito)
                Shydeath += Time.fixedDeltaTime;
            else
                Shydeath -= Time.fixedDeltaTime * 1 / 4;

            if (Shydeath <= -0.25f)
                Shydeath = 0;

            if (Shytime <= Shydeath)
            {
                Logger.Info("もぉみんなかまうからシャイ君しんぢゃったぁ～!", "Shyboy");
                MyState.DeathReason = CustomDeathReason.Suicide;
                Player.RpcMurderPlayer(Player);
                Shydeath = -1;

                if ((Event.April || Event.Special) && OptionShyDieBom.GetBool())
                {
                    var bombcount = 0;
                    foreach (var pc in PlayerCatch.AllAlivePlayerControls)
                    {
                        if (pc != player)
                        {
                            float HitoDistance = Vector2.Distance(GSpos, pc.transform.position);
                            var vector = (Vector2)pc.transform.position - GSpos;
                            float dis = vector.magnitude;
                            if (HitoDistance <= Shydeathdi && !PhysicsHelpers.AnyNonTriggersBetween(GSpos, pc.transform.position, dis, Constants.ShipAndObjectsMask))
                            {
                                bombcount++;
                                CustomRoleManager.OnCheckMurder(Player, pc, pc, pc, true, true, 10, CustomDeathReason.Bombed);
                                Logger.Info($"Booooooooooooom! => {pc.Data.GetLogPlayerName()}", "ShyboyDie");
                            }
                        }
                    }
                    if (3 <= bombcount)
                        Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[3]);
                }
            }
        }

        if (GameStates.IsInTask && ShowArrow)
        {
            var aliveIds = new HashSet<byte>(
                PlayerCatch.AllAlivePlayerControls
                    .Where(pc => pc.PlayerId != Player.PlayerId)
                    .Select(pc => pc.PlayerId));

            foreach (var id in aliveIds)
            {
                if (registeredArrows.Add(id))
                    TargetArrow.Add(Player.PlayerId, id);
            }

            foreach (var id in registeredArrows.ToArray())
            {
                if (!aliveIds.Contains(id))
                {
                    TargetArrow.Remove(Player.PlayerId, id);
                    registeredArrows.Remove(id);
                }
            }
        }

        if (GameStates.IsInTask)
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (isForMeeting) return "";
        if (!Player.IsAlive()) return "";
        if (!Is(seer) || !Is(seen)) return "";
        if (!ShowArrow) return "";

        var arrows = "";
        foreach (var id in registeredArrows)
        {
            var pc = PlayerCatch.GetPlayerById(id);
            if (pc == null || !pc.IsAlive()) continue;

            var arr = TargetArrow.GetArrows(seer, id);
            if (string.IsNullOrEmpty(arr)) continue;

            string colorCode = "#ffffff";
            if (ArrowMatchColor)
            {
                int colorId = pc.Data.DefaultOutfit.ColorId;
                if (colorId >= 0 && colorId < Palette.PlayerColors.Length)
                    colorCode = "#" + UnityEngine.ColorUtility.ToHtmlStringRGB(Palette.PlayerColors[colorId]);
            }
            arrows += $"<color={colorCode}>{arr}</color>";
        }
        return arrows;
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting || isForHud) return "";
        if (!Player.IsAlive()) return "";
        if (!Is(seer) || !Is(seen)) return "";
        if (!ShowNearby) return "";

        string shapes = "";
        Vector2 GSpos = Player.transform.position;

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            if (pc.PlayerId == Player.PlayerId) continue;

            float HitoDistance = Vector2.Distance(GSpos, pc.transform.position);
            var vector = (Vector2)pc.transform.position - GSpos;
            float dis = vector.magnitude;

            if (HitoDistance <= ShowNearbyRange && !PhysicsHelpers.AnyNonTriggersBetween(GSpos, pc.transform.position, dis, Constants.ShadowMask))
            {
                int colorId = pc.Data.DefaultOutfit.ColorId;
                string colorCode = "#ffffff";
                if (colorId >= 0 && colorId < Palette.PlayerColors.Length)
                    colorCode = "#" + UnityEngine.ColorUtility.ToHtmlStringRGB(Palette.PlayerColors[colorId]);
                shapes += $"<size=110%><color={colorCode}>■</color></size>";
            }
        }

        if (!string.IsNullOrEmpty(shapes))
            return $"<size=70%>{shapes}</size>";

        return "";
    }

    public override bool AllEnabledColor => true;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;
    public override string GetAbilityButtonText() => GetString("ShyBoyText");
    public override bool OverrideAbilityButton(out string text)
    {
        text = "ShyBoy_Ability";
        return true;
    }
    public override void CheckWinner(GameOverReason reason)
    {
        if (!Player.IsAlive()) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Shyboy, Player.PlayerId))
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
            CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
        }

        Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
        if (Shytime <= 3 && Notshy <= 5)
            Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]);
    }

    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();

    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
        var sp1 = new Achievement(RoleInfo, 1, 1, 2, 2);
        var l1 = new Achievement(RoleInfo, 2, 1, 1, 2, true);
        achievements.Add(0, n1);
        achievements.Add(1, sp1);
        achievements.Add(2, l1);
    }
}