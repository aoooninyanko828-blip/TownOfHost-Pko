using HarmonyLib;
using UnityEngine;
using TownOfHost.Modules;

namespace TownOfHost.Patches
{
    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
    public static class AutoStartPatch
    {
        private static float timer = 0f;
        private static int lastPlayerCount = 0;

        public static void Postfix()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!Options.OptionAutoStartSetting.GetBool()) return;
            if (Options.OptionAutoStartGM.GetBool() && !Options.EnableGM.GetBool()) return;

            if (!GameStates.IsLobby)
            {
                timer = 0f;
                lastPlayerCount = 0;
                return;
            }

            int playerCount = PlayerControl.AllPlayerControls.Count;

            if (Options.OptionAutoStartLimitAnotherSetting.GetBool() && lastPlayerCount != 15 && playerCount == 15)
            {
                float limit15 = Options.OptionAutoStartLimitAnother.GetFloat();
                if (timer > limit15 - 20f)
                    timer = limit15 - 20f;
            }
            lastPlayerCount = playerCount;

            timer += Time.deltaTime;

            float limit = (Options.OptionAutoStartLimitAnotherSetting.GetBool() && playerCount == 15)
                ? Options.OptionAutoStartLimitAnother.GetFloat()
                : Options.OptionAutoStartLimit.GetFloat();

            if (timer >= limit)
            {
                timer = 0f;

                // ★ 自動スタート前にモデレーター名プレフィックスを除去（暗転防止）
                Moderator.StripModeratorDisplayNamesForGame();

                _ = new LateTask(() =>
                {
                    var gsm = DestroyableSingleton<GameStartManager>.Instance;
                    if (gsm == null) return;
                    gsm.countDownTimer = 0.1f;
                    gsm.startState = GameStartManager.StartingStates.Countdown;
                }, 0.15f, "AutoStart.DelayedStart", true);
            }
        }
    }
}