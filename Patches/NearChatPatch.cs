using HarmonyLib;
using TownOfHost.Modules;
using UnityEngine;

namespace TownOfHost.Patches;

// ★ 試合中もチャットボタンを有効にする
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class GameChatVisiblePatch
{
    public static void Postfix(HudManager __instance)
    {
        if (!GameStates.IsInTask) return;
        if (__instance.Chat == null) return;
        if (!Options.OptionGameChatSetting.GetBool()) return;

        if (__instance.Chat.chatButton != null)
            __instance.Chat.chatButton.gameObject.SetActive(true);

        if (PlayerControl.LocalPlayer != null && !PlayerControl.LocalPlayer.IsAlive())
        {
            if (!__instance.Chat.IsOpenOrOpening)
                __instance.Chat.SetVisible(true);
        }
    }
}

// ★ 試合中のチャット送信処理
[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
public static class BlockNormalChatInGamePatch
{
    public static bool Prefix(ChatController __instance)
    {
        if (!GameStates.IsInTask) return true;
        if (GameStates.IsMeeting) return true;
        if (!Options.OptionGameChatSetting.GetBool()) return true;

        string text = (__instance.freeChatField?.textArea?.text ?? "").Trim();
        if (string.IsNullOrEmpty(text)) return false;

        // ★ /cmd で始まるコマンド処理
        if (text.StartsWith("/cmd ", System.StringComparison.OrdinalIgnoreCase))
        {
            // ★ ゲッサーコマンドはタスクターン中使用不可
            string lower = text.ToLower();
            if (lower.Contains("/cmd bt") || lower.Contains("/cmd /bt"))
            {
                Utils.SendMessage(
                    "<color=#ff6666>ゲッサーコマンドは会議中のみ使用できます。</color>",
                    PlayerControl.LocalPlayer.PlayerId);
                __instance.freeChatField?.textArea?.Clear();
                return false;
            }

            // ★ 秘匿チャットコマンドは通す
            return true;
        }

        // ★ 通常チャット処理
        if (!Options.OptionGameChatNormalChat.GetBool())
        {
            // 通常チャット無効
            ChatBubbleShower.Show(
                "<color=#ff6666>試合中の通常チャットは無効です。</color>",
                "<color=#ffaa00>⚠ チャット制限</color>");
            __instance.freeChatField?.textArea?.Clear();
            return false;
        }

        // ★ 近チャ機能が有効な場合
        if (Options.OptionGameChatNormalNearChat.GetBool())
        {
            int range = Options.OptionGameChatNormalNearChatRange.GetInt();
            SendNearChat(text, range, isHideChat: false);
            __instance.freeChatField?.textArea?.Clear();
            return false;
        }

        // ★ 通常チャット（全員送信）はそのまま通す
        return true;
    }

    private static void SendNearChat(string message, int range, bool isHideChat)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        var sender = PlayerControl.LocalPlayer;
        var senderPos = (UnityEngine.Vector2)sender.GetTruePosition();
        string colorTag = isHideChat ? "#aaaaff" : "#ffffff";
        string prefix = isHideChat ? "【近秘匿】" : "【近チャ】";

        int sentCount = 0;
        foreach (var target in PlayerCatch.AllPlayerControls)
        {
            if (!target.IsAlive()) continue;

            float dist = UnityEngine.Vector2.Distance(
                senderPos, (UnityEngine.Vector2)target.GetTruePosition());

            if (dist > range) continue;

            Utils.SendMessage(
                $"<color={colorTag}>{prefix} {UtilsName.GetPlayerColor(sender, true)}: {message}</color>",
                target.PlayerId,
                $"<color={colorTag}>近チャット ({(int)dist}m)</color>");
            sentCount++;
        }

        Logger.Info(
            $"{sender.Data.GetLogPlayerName()} 近チャ送信 ({sentCount}人, 範囲:{range})",
            "NearChat");
    }
}

// ★ 他プレイヤーのチャットを試合中に処理
[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
public static class GameChatAddChatPatch
{
    public static bool Prefix(
        [HarmonyArgument(0)] PlayerControl sourcePlayer,
        [HarmonyArgument(1)] string chatText)
    {
        if (!GameStates.IsInTask) return true;
        if (GameStates.IsMeeting) return true;
        if (!Options.OptionGameChatSetting.GetBool()) return true;
        if (sourcePlayer == null) return true;

        // ★ 自分のメッセージは表示
        if (sourcePlayer.PlayerId == PlayerControl.LocalPlayer?.PlayerId) return true;

        // ★ コマンドは非表示（秘匿チャットはSendMessageで配信済み）
        if (chatText.StartsWith("/cmd", System.StringComparison.OrdinalIgnoreCase))
            return false;

        // ★ 死亡者は全チャット閲覧可能
        if (PlayerControl.LocalPlayer != null && !PlayerControl.LocalPlayer.IsAlive())
            return true;

        // ★ 近チャ有効時は自分に届いたもの（Utils.SendMessage経由）のみ表示
        // 生のチャットは非表示
        return false;
    }
}

// ★ 非ホスト側の近チャ送信（RPCでホストに依頼）
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
public static class NearChatRpcSendPatch
{
    public static bool Prefix(PlayerControl __instance, string chatText)
    {
        if (!GameStates.IsInTask) return true;
        if (GameStates.IsMeeting) return true;
        if (!Options.OptionGameChatSetting.GetBool()) return true;
        if (!Options.OptionGameChatNormalChat.GetBool()) return true;
        if (!Options.OptionGameChatNormalNearChat.GetBool()) return true;
        if (AmongUsClient.Instance.AmHost) return true; // ホストは直接処理済み
        if (chatText.StartsWith("/cmd", System.StringComparison.OrdinalIgnoreCase)) return true;

        // ★ 非ホストは通常のRPCで送信（ホスト側のAddChatでフィルタリング）
        // 近チャはホスト側の OnReceiveChat で処理
        return true;
    }
}

// ★ 秘匿チャット近チャ処理（OnReceiveChatに追記する形で対応）
// ChatCommands.csのOnReceiveChatで秘匿チャットコマンドが来たとき、
// OptionGameChatHideNearChat が有効なら近チャとして送信する
public static class GameChatNearChatHelper
{
    // ★ ゲッサーコマンド無効チェック
    public static bool IsGuesserCommandBlocked()
    {
        return GameStates.IsInTask && !GameStates.IsMeeting;
    }

    // ★ 秘匿チャットを近チャとして送信
    public static bool TrySendHideNearChat(
        PlayerControl sender, string message, string markColor, string titleMark)
    {
        if (!Options.OptionGameChatSetting.GetBool()) return false;
        if (!Options.OptionGameChatHideChat.GetBool()) return false;
        if (!Options.OptionGameChatHideNearChat.GetBool()) return false;

        int range = Options.OptionGameChatHideNearChatRange.GetInt();
        var senderPos = (UnityEngine.Vector2)sender.GetTruePosition();

        foreach (var target in PlayerCatch.AllPlayerControls)
        {
            if (!target.IsAlive()) continue;
            float dist = UnityEngine.Vector2.Distance(
                senderPos, (UnityEngine.Vector2)target.GetTruePosition());
            if (dist > range) continue;

            Utils.SendMessage(
                $"<color={markColor}>【近秘匿】 {message}</color>",
                target.PlayerId,
                $"{titleMark} <size=80%>({(int)dist}m)</size>");
        }

        return true;
    }
}

// ★ 試合開始時にチャットを表示
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
public static class ShowChatOnGameStartPatch
{
    public static void Postfix()
    {
        if (!Options.OptionGameChatSetting.GetBool()) return;
        _ = new LateTask(() =>
        {
            try
            {
                var hud = DestroyableSingleton<HudManager>.Instance;
                if (hud?.Chat == null) return;
                hud.Chat.SetVisible(true);
            }
            catch (System.Exception e)
            {
                Logger.Error(e.ToString(), "ShowChatOnGameStart");
            }
        }, 3f, "ShowChatOnGameStart", true);
    }
}

// ★ 試合終了時にチャットをリセット
[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
public static class RestoreChatOnGameEndPatch
{
    public static void Postfix()
    {
        ChatBubbleShower.Reset();
        _ = new LateTask(() =>
        {
            try
            {
                var hud = DestroyableSingleton<HudManager>.Instance;
                hud?.Chat?.SetVisible(false);
            }
            catch { }
        }, 1f, "RestoreChatOnGameEnd", true);
    }
}

// ★ ChatBubbleShower の Update を呼ぶ
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class ChatBubbleShowerUpdatePatch
{
    public static void Postfix()
    {
        ChatBubbleShower.Update();
    }
}