using HarmonyLib;
using UnityEngine;

namespace TownOfHost.Patches
{
    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
    public static class AutoStartPatch
    {
        private static float timer = 0f;
        private static int lastPlayerCount = 0; // 前のフレームの人数を記憶

        public static void Postfix()
        {
            // ホスト以外は動作しない
            if (!AmongUsClient.Instance.AmHost) return;

            // 自動スタート設定がOFFなら動作しない
            if (!Options.OptionAutoStartSetting.GetBool()) return;

            // GMのみ有効 → GMじゃないなら動作しない
            if (Options.OptionAutoStartGM.GetBool() && !Options.EnableGM.GetBool()) return;

            // ロビー以外ではリセット
            if (!GameStates.IsLobby)
            {
                timer = 0f;
                lastPlayerCount = 0;
                return;
            }

            int playerCount = PlayerControl.AllPlayerControls.Count;

            // 途中で15人に達したときの猶予処理（20秒待つ）
            if (Options.OptionAutoStartLimitAnotherSetting.GetBool() && lastPlayerCount != 15 && playerCount == 15)
            {
                float limit15 = Options.OptionAutoStartLimitAnother.GetFloat();

                // 15人用の制限時間をすでに切っている、もしくは残り時間が20秒未満の場合、残り20秒になるようにタイマーを調整
                if (timer > limit15 - 20f)
                {
                    timer = limit15 - 20f;
                }
            }
            lastPlayerCount = playerCount;

            // タイマー進行
            timer += Time.deltaTime;

            // ★ 15人時の別設定がONならそちらを優先
            float limit;

            if (Options.OptionAutoStartLimitAnotherSetting.GetBool() && playerCount == 15)
            {
                limit = Options.OptionAutoStartLimitAnother.GetFloat();
            }
            else
            {
                limit = Options.OptionAutoStartLimit.GetFloat();
            }

            // タイマーが規定値を超えたら開始
            if (timer >= limit)
            {
                timer = 0f;

                var gsm = DestroyableSingleton<GameStartManager>.Instance;
                if (gsm != null)
                {
                    gsm.countDownTimer = 0.1f; // 即開始
                    gsm.startState = GameStartManager.StartingStates.Countdown;
                }
            }
        }
    }
}