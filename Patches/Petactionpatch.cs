using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using TownOfHost.Roles.Core;

namespace TownOfHost.Patches;

/// <summary>
/// ペットを撫でたときにRPCを送信し、役職のOnPetを呼ぶPatch。
/// EHRのLocalPetPatch + ExternalRpcPetPatchを参考に実装。
/// </summary>

// ★ ホスト自身がペットを撫でたとき
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.TryPet))]
internal static class LocalPetPatch
{
    private static readonly Dictionary<byte, long> LastProcess = new();

    public static bool Prefix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (GameStates.IsLobby || !__instance.IsAlive()) return true;
        if (__instance.petting) return true;

        __instance.petting = true;

        if (!LastProcess.ContainsKey(__instance.PlayerId))
            LastProcess[__instance.PlayerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 2;
        if (LastProcess[__instance.PlayerId] + 1 >= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return true;

        ExternalRpcPetPatch.Prefix(__instance.MyPhysics, (byte)RpcCalls.Pet);

        LastProcess[__instance.PlayerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return true;
    }

    public static void Postfix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        __instance.petting = false;
    }
}

// ★ 誰かがペットを撫でたRPCを受信したとき（ホストのみ処理）
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
internal static class ExternalRpcPetPatch
{
    private static readonly Dictionary<byte, long> LastProcess = new();

    public static void Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callID)
    {
        if ((RpcCalls)callID != RpcCalls.Pet) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.IsLobby) return;

        var pc = __instance.myPlayer;
        if (pc == null || !pc.IsAlive()) return;

        if (!LastProcess.ContainsKey(pc.PlayerId))
            LastProcess[pc.PlayerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 2;
        if (LastProcess[pc.PlayerId] + 1 >= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return;

        LastProcess[pc.PlayerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Logger.Info($"{pc.Data?.GetLogPlayerName()} がペットを撫でた", "PetActionPatch");

        OnPetUse(pc);
    }

    private static void OnPetUse(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive()) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.IsLobby || GameStates.IsMeeting) return;

        if (pc.inVent || pc.inMovingPlat || pc.onLadder || pc.walkingToVent) return;
        if (pc.MyPhysics.Animations.IsPlayingEnterVentAnimation()) return;
        if (pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) return;

        if (PetActionManager.Handlers.TryGetValue(pc.PlayerId, out var handler))
        {
            handler.Invoke();
            Logger.Info($"{pc.Data?.GetLogPlayerName()} のOnPet実行", "PetActionPatch");
        }
    }
}

/// <summary>
/// ペットIDを変更するヘルパー
/// </summary>
public static class PetsHelper
{
    public static void SetPet(PlayerControl pc, string petId)
    {
        if (pc == null) return;
        pc.RpcSetPet(petId);
    }

    public static void RemovePet(PlayerControl pc)
    {
        if (pc == null || !pc.Data.IsDead || pc.IsAlive()) return;
        if (pc.CurrentOutfit.PetId == "") return;
        SetPet(pc, "");
    }
}

/// <summary>
/// 役職ごとのペット撫でハンドラを管理するクラス。
/// </summary>
public static class PetActionManager
{
    private static readonly string[] EhrPetIds =
    [
        "pet_Goose", "pet_Bedcrab", "pet_DancingSkeletonPet", "pet_BredPet",
        "pet_YuleGoatPet", "pet_Bush", "pet_Charles", "pet_ChewiePet",
        "pet_clank", "pet_coaltonpet", "pet_Creb", "pet_Cube",
        "pet_lny_dragon", "pet_Doggy", "pet_Ellie", "pet_Strawb",
        "pet_frankendog", "pet_D2GhostPet", "pet_test", "pet_GuiltySpark",
        "pet_Stickmin", "pet_HamPet", "pet_Hamster", "pet_Alien",
        "pet_poro", "pet_Crow", "pet_Lava", "pet_Crewmate",
        "pet_Mister", "pet_nancy", "pet_napstamate", "pet_Pip",
        "pet_pocketCircuitCar", "pet_D2PoukaPet", "pet_Pusheen", "pet_Pate",
        "pet_Rammy", "pet_Robot", "pet_Snow", "pet_spaceCat",
        "pet_Squig", "pet_Stormy", "pet_nuggetPet", "pet_Charles_Red",
        "pet_UFO", "pet_D2WormPet"
    ];

    private const string DefaultPetIdForPetAction = "pet_test";

    public static readonly Dictionary<byte, Action> Handlers = new();

    public static void Register(byte playerId, Action action)
    {
        Handlers[playerId] = action;
        EnsureDefaultPet(playerId);
    }

    public static void Unregister(byte playerId)
    {
        Handlers.Remove(playerId);
    }

    public static void Reset()
    {
        Handlers.Clear();
    }

    public static void EnsureDefaultPet(byte playerId)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInGame || GameStates.IsLobby) return;

        var pc = PlayerCatch.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive()) return;

        // ★ HasPetで毎回チェック（AutoPetSentガードなし）
        if (HasPet(pc)) return;

        if (Array.IndexOf(EhrPetIds, DefaultPetIdForPetAction) < 0)
            Logger.Warn($"ペットIDがリストに見つかりません: {DefaultPetIdForPetAction}", "PetActionPatch");

        PetsHelper.SetPet(pc, DefaultPetIdForPetAction);
        UpdatePetIdInCamouflageCache(playerId, DefaultPetIdForPetAction);
        Logger.Info($"{pc.Data?.GetLogPlayerName()} にGlitchペットを自動付与: {DefaultPetIdForPetAction}", "PetActionPatch");
    }

    private static bool HasPet(PlayerControl pc)
    {
        string currentPet = pc.Data?.DefaultOutfit?.PetId ?? pc.CurrentOutfit?.PetId ?? "";
        if (string.IsNullOrEmpty(currentPet)) return false;

        string petId = currentPet.ToLowerInvariant();
        return petId != "none" &&
               petId != "pet_none" &&
               petId != "pet_emptypet" &&
               petId != "pet_enmptypet";
    }

    private static void UpdatePetIdInCamouflageCache(byte playerId, string petId)
    {
        if (!Camouflage.PlayerSkins.TryGetValue(playerId, out var outfit)) return;
        outfit.PetId = petId;
        Camouflage.PlayerSkins[playerId] = outfit;
    }
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
internal static class AutoPetAssignPatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        _ = new LateTask(() =>
        {
            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
                PetActionManager.EnsureDefaultPet(pc.PlayerId);
        }, 0.6f, "AutoPetAssignAfterIntro", true);

        _ = new LateTask(() =>
        {
            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
                PetActionManager.EnsureDefaultPet(pc.PlayerId);
        }, 2.0f, "AutoPetAssignAfterIntroRetry", true);
    }
}

// ★ 会議後にペットを再付与（カモフラージュでリセットされても再付与される）
[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
internal static class AfterMeetingPetAssignPatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        _ = new LateTask(() =>
        {
            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
                PetActionManager.EnsureDefaultPet(pc.PlayerId);
        }, 1.5f, "AfterMeetingPetAssign", true);
    }
}