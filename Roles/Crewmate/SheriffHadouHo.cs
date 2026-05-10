using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class SheriffHadouHo : RoleBase, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(SheriffHadouHo),
            player => new SheriffHadouHo(player),
            CustomRoles.SheriffHadouHo,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Crewmate,
            60200,
            SetupOptionItem,
            "shh",
            "#f8cd46",
            (2, 0),
            countType: CountTypes.Crew
        );
    public SheriffHadouHo(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.False)
    {
        Cooldown = OptionCooldown.GetFloat();
        ChargeTime = OptionChargeTime.GetFloat();
        ShotLimit = OptionShotLimit.GetInt();
        SelfDestructOnMiss = OptionSelfDestructOnMiss.GetBool();
        BeamColorModeValue = OptionBeamColorMode.GetValue();

        IsCharging = false;
        chargeTimer = 0f;
        PlayerSpeed = 0f;
        ShowBeamMark = false;
        HasHitEvil = false;
        HitCrew = false;
        IsDead = false;
        IsFiring = false;
        _prevCharging = false;
        _prevBeamMark = false;
        BeamFacingLeft = false;
        PlayerColor = 0;

        CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
    }

    static OptionItem OptionCooldown;
    static float Cooldown;
    static OptionItem OptionChargeTime;
    static float ChargeTime;
    static OptionItem OptionShotLimit;
    static OptionItem OptionSelfDestructOnMiss;
    static bool SelfDestructOnMiss;
    static OptionItem OptionBeamColorMode;
    static int BeamColorModeValue;

    int ShotLimit;
    public bool IsCharging;
    float chargeTimer;
    float PlayerSpeed;
    public bool ShowBeamMark;
    bool HasHitEvil;
    bool HitCrew;
    bool IsDead;
    bool IsFiring;
    bool spawnCooldownStarted;
    bool _prevCharging;
    bool _prevBeamMark;
    bool BeamFacingLeft;
    int PlayerColor;

    enum BeamColorMode { Rainbow, Single, Yellow }
    enum OptionName
    {
        SheriffHadouHoChargeTime,
        SheriffHadouHoShotLimit,
        SheriffHadouHoSelfDestruct
    }

    static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionChargeTime = FloatOptionItem.Create(RoleInfo, 11, OptionName.SheriffHadouHoChargeTime,
            new(0.5f, 10f, 0.5f), 3f, false).SetValueFormat(OptionFormat.Seconds);
        OptionShotLimit = IntegerOptionItem.Create(RoleInfo, 12, OptionName.SheriffHadouHoShotLimit,
            new(1, 15, 1), 3, false).SetValueFormat(OptionFormat.Times);
        OptionSelfDestructOnMiss = BooleanOptionItem.Create(RoleInfo, 13, OptionName.SheriffHadouHoSelfDestruct, false, false);
        OptionBeamColorMode = StringOptionItem.Create(RoleInfo, 20, "SheriffHadouHoBeamColorMode", new string[] { "Rainbow", "Single", "Yellow" }, 2, false);
    }

    public override void Add()
    {
        PlayerSpeed = Main.AllPlayerSpeed[Player.PlayerId];
        PlayerColor = Player.Data.DefaultOutfit.ColorId;
        BeamColorModeValue = OptionBeamColorMode.GetValue();
        spawnCooldownStarted = false;
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = Cooldown;
    }

    public override bool CanClickUseVentButton => false;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;

    bool IUsePhantomButton.IsPhantomRole => true;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        if (IsFiring || ShowBeamMark || !Player.IsAlive() || IsCharging) return;
        if (ShotLimit <= 0) return;

        IsFiring = true;
        IsCharging = true;
        chargeTimer = 0f;
        Main.AllPlayerSpeed[Player.PlayerId] = Main.MinSpeed;
        Player.MarkDirtySettings();
        Utils.AllPlayerKillFlash();
        Player.SyncSettings();
        _prevCharging = true;
        _prevBeamMark = false;

        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
        SendRpc();
    }

    void SetRoleTextHeight(bool beaming)
    {
        var t = Player.cosmetics.nameText.transform.Find("RoleText");
        if (t == null) return;
        var rt = t.GetComponent<TMPro.TextMeshPro>();
        if (rt == null) return;
        if (beaming) { rt.text = "<alpha=#00>　</alpha>"; t.SetLocalY(0.35f); }
        else { rt.enabled = true; t.SetLocalY(0.35f); }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (!spawnCooldownStarted && Player.IsAlive() && !GameStates.Intro && GameStates.IsInTask && !GameStates.IsMeeting)
        {
            spawnCooldownStarted = true;
            Player.RpcResetAbilityCooldown(Sync: true);
        }

        if (MeetingHud.Instance != null)
        {
            if (IsCharging || ShowBeamMark || IsFiring)
            {
                ResetBeamState();
                UtilsNotifyRoles.NotifyRoles();
                SendRpc();
            }
            return;
        }

        if (!Player.IsAlive() && (IsCharging || ShowBeamMark))
        {
            ResetBeamState();
            Player.RpcSetColor((byte)PlayerColor);
            UtilsNotifyRoles.NotifyRoles();
            SendRpc();
            return;
        }

        bool changed = (IsCharging != _prevCharging) || (ShowBeamMark != _prevBeamMark);
        if (changed)
        {
            _prevCharging = IsCharging;
            _prevBeamMark = ShowBeamMark;
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        }

        if (ShowBeamMark && Player.IsAlive()) ApplyBeamHit();
        if (IsCharging)
        {
            chargeTimer += Time.fixedDeltaTime;
            if (chargeTimer >= ChargeTime) FireBeam();
        }
    }

    void ResetBeamState()
    {
        IsCharging = false; ShowBeamMark = false; IsFiring = false;
        _prevCharging = false; _prevBeamMark = false;
        HasHitEvil = false; HitCrew = false;
        Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
        Player.MarkDirtySettings();
        SetRoleTextHeight(false);
    }

    void FireBeam()
    {
        if (IsDead || !Player.IsAlive()) return;

        BeamFacingLeft = Player.cosmetics.FlipX;
        SendBeamDirection(BeamFacingLeft);
        Utils.AllPlayerKillFlash();

        IsCharging = false; chargeTimer = 0f;
        HasHitEvil = false; HitCrew = false;
        ShowBeamMark = true;

        SetRoleTextHeight(true);
        _prevCharging = false; _prevBeamMark = true;
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRpc();
        ApplyBeamHit();

        _ = new LateTask(() =>
        {
            ShowBeamMark = false; _prevBeamMark = false;
            SetRoleTextHeight(false);

            bool suicide = false;

            if (!HasHitEvil && !HitCrew && SelfDestructOnMiss)
            {
                suicide = true;
                UtilsGameLog.AddGameLog("SheriffHadouHo",
                    $"{UtilsName.GetPlayerColor(Player)}'s beam hit no one and they self-destructed.");
            }

            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
            SendRpc();

            if (!Player.IsAlive()) { IsFiring = false; return; }

            if (suicide)
            {
                Player.RpcSetColor((byte)PlayerColor);
                Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
                Player.MarkDirtySettings();
                PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = CustomDeathReason.Misfire;
                Player.RpcMurderPlayerV2(Player);
                IsFiring = false;
                return;
            }

            Player.RpcSetColor((byte)PlayerColor);
            Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
            Player.MarkDirtySettings();

            _ = new LateTask(() =>
            {
                if (!Player.IsAlive()) { IsFiring = false; return; }
                AURoleOptions.PhantomCooldown = Cooldown;
                Player.RpcResetAbilityCooldown(Sync: true);
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
                _ = new LateTask(() => { IsFiring = false; }, 0.3f, "SHHResetFiring", true);
            }, 0.2f, "SHHResetCooldown", true);
        }, 3f);
    }

    void ApplyBeamHit()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;

        var myPos = Player.GetTruePosition();
        Vector2 dir = BeamFacingLeft ? Vector2.left : Vector2.right;

        foreach (var target in PlayerCatch.AllAlivePlayerControls)
        {
            if (target.PlayerId == Player.PlayerId) continue;

            var toTarget = target.GetTruePosition() - myPos;
            float dot = Vector2.Dot(toTarget, dir);
            if (dot <= 0) continue;
            if ((toTarget - dir * dot).magnitude > 1.3f) continue;

            bool isEvil = Sheriff.CanBeKilledBy(target);

            if (isEvil)
            {
                CustomRoleManager.OnCheckMurder(Player, target, target, target, true, true, 1, CustomDeathReason.Hit);
                HasHitEvil = true;
                UtilsGameLog.AddGameLog("SheriffHadouHo",
                    $"{UtilsName.GetPlayerColor(Player)}'s beam hit {UtilsName.GetPlayerColor(target)} (Evil)");
            }
            else
            {
                CustomRoleManager.OnCheckMurder(Player, target, target, target, true, true, 1, CustomDeathReason.Hit);
                HitCrew = true;
                UtilsGameLog.AddGameLog("SheriffHadouHo",
                    $"{UtilsName.GetPlayerColor(Player)}'s beam hit {UtilsName.GetPlayerColor(target)} (Crewmate)");
            }
        }

        if (!HasHitEvil && !HitCrew)
        {
        }
        ShotLimit--;
        SendRpc();
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        ResetBeamState();
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRpc();
    }

    public override void OnStartMeeting() => ResetBeamState();

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;
        AURoleOptions.PhantomCooldown = Cooldown;
        Player.RpcResetAbilityCooldown();
    }

    public override bool GetTemporaryName(ref string name, ref bool NoMarker, bool isForMeeting, PlayerControl seer, PlayerControl seen = null)
    {
        seen ??= seer;
        if (!Player.IsAlive() || isForMeeting) return false;
        string myColor = "#" + ColorUtility.ToHtmlStringRGB(Palette.PlayerColors[Player.Data.DefaultOutfit.ColorId]);

        if (IsCharging && seen.PlayerId == Player.PlayerId)
        {
            bool fl = seer.PlayerId == Player.PlayerId ? Player.cosmetics.FlipX : BeamFacingLeft;
            string bigStar = $"<size=800%><color={myColor}>★</color></size>";
            string blank = "　　　";
            name = "<line-height=1200%>\n" + (fl ? bigStar + blank : blank + bigStar) + "</line-height>";
            NoMarker = true; return true;
        }

        if (seen == seer && Is(seer) && !seer.IsModClient() && (IsCharging || ShowBeamMark))
        {
            if (ShowBeamMark && seen.PlayerId == Player.PlayerId)
            { BuildBeamName(ref name, myColor, false); NoMarker = true; return true; }
            return false;
        }

        if (ShowBeamMark && seen.PlayerId == Player.PlayerId)
        {
            SetRoleTextHeight(true);
            BuildBeamName(ref name, myColor, true);
            NoMarker = true; return true;
        }
        return false;
    }

    void BuildBeamName(ref string name, string myColor, bool wider)
    {
        SetRoleTextHeight(true);
        bool fl = BeamFacingLeft;
        string star = $"<voffset=0.35em><size=800%><color={myColor}>★</color></size></voffset>";
        string beam = BuildBeamBlock();
        string blank = "<size=1200%>　</size>";
        string starBlank = fl ? star + blank : blank + star;
        string longBeam = fl ? beam + beam + starBlank : starBlank + beam + beam;
        string hugeBlank = "<alpha=#00>" + new string('　', 10) + "</alpha>";
        string ls = wider ? "<line-height=5300%>\n" : "<line-height=4300%>\n";
        string ss = "<size=5000%>", se = "</size></line-height>";
        name = fl
            ? ls + $"{ss}{longBeam}{se}{ss}{hugeBlank}{se}"
            : ls + $"{ss}{hugeBlank}{se}{ss}{longBeam}{se}";
    }

    string BuildBeamBlock()
    {
        if ((BeamColorMode)BeamColorModeValue == BeamColorMode.Yellow)
            return "<color=#f8cd46>━</color><color=#f8cd46>━</color><color=#f8cd46>━</color><color=#f8cd46>━</color><color=#f8cd46>━</color><color=#f8cd46>━</color><color=#f8cd46>━</color>";
        if ((BeamColorMode)BeamColorModeValue == BeamColorMode.Single)
            return "<color=#00CFFF>━</color><color=#00CFFF>━</color><color=#00CFFF>━</color><color=#00CFFF>━</color><color=#00CFFF>━</color><color=#00CFFF>━</color><color=#00CFFF>━</color>";

        return "<color=#ff0000>━</color><color=#ff7f00>━</color><color=#ffff00>━</color><color=#00ff00>━</color><color=#0000ff>━</color><color=#4b0082>━</color><color=#8b00ff>━</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || isForMeeting || !Player.IsAlive()) return "";
        string size = isForHud ? "" : "<size=60%>";
        if (ShotLimit <= 0) return $"{size}<color=#888888>Out of ammo</color>";
        if (!IsCharging) return $"{size}<color=#f8cd46>Phantom Button -> Charge Fire</color>";
        return $"{size}<color=#f8cd46>Charging... {(ChargeTime - chargeTimer):F1}s</color>";
    }

    public string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen != seer || isForMeeting || !Player.IsAlive()) return "";
        if (IsCharging && seer.PlayerId != Player.PlayerId) return $"<color=#f8cd46>Charging... {(int)(ChargeTime - chargeTimer)}s</color>";
        if (ShowBeamMark && seer.PlayerId != Player.PlayerId) return "<color=#f8cd46>Firing Beam</color>";
        return "";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        return Utils.ColorString(ShotLimit > 0 ? Color.yellow : Color.gray, $"({ShotLimit})");
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(IsCharging);
        sender.Writer.Write(ShowBeamMark);
        sender.Writer.Write(ShotLimit);
    }

    void SendBeamDirection(bool left)
    {
        using var sender = CreateSender();
        sender.Writer.Write((byte)1);
        sender.Writer.Write(left);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        if (reader.Length - reader.Position == 2)
        { reader.ReadByte(); BeamFacingLeft = reader.ReadBoolean(); return; }
        IsCharging = reader.ReadBoolean();
        ShowBeamMark = reader.ReadBoolean();
        ShotLimit = reader.ReadInt32();
    }
}