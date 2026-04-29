using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using UnityEngine;

namespace TownOfHost
{
    // =========================================================================
    // ★ 1. バトロワ専用ダミークラス（キルされるとワープする）
    // =========================================================================
    public class RoyaleDummy : CustomNetObject, IKillableDummy
    {
        public void Spawn(Vector2 pos)
        {
            base.CreateNetObject(pos);
        }

        public void OnKilled(PlayerControl killer)
        {
            if (killer.AmOwner)
            {
                DummyBattleRoyaleManager.MyScore++;

                Vector2 newPos = DummyBattleRoyaleManager.GetRandomPosition();
                this.SnapToPosition(newPos);

                Utils.AllPlayerKillFlash();
                killer.SetKillTimer(0f);
            }
        }
    }

    // =========================================================================
    // ★ 2. バトロワのゲームルールと進行を管理するマネージャー
    // =========================================================================
    public static class DummyBattleRoyaleManager
    {
        public static OptionItem OptionEnable;
        public static OptionItem OptionTimeLimit;
        public static OptionItem OptionDummyCount;
        public static OptionItem OptionShowArrow;

        public static bool IsActive = false;
        public static float TimeLeft = 0f;
        public static int MyScore = 0;
        public static List<RoyaleDummy> ActiveDummies = new();

        public static void SetupOptionItem()
        {
            // ★ エラー修正：TOH-Pの正しいオプション作成フォーマットに変更しました！
            // CustomOptionTags.DummyBattleRoyale を付けたので、専用モードの時だけ表示されます。

            OptionEnable = BooleanOptionItem.Create(10000, "DummyBattleRoyaleEnable", false, TabGroup.MainSettings, false)
                .SetOptionName(() => "ダミーバトルロワイヤル有効")
                .SetTag(CustomOptionTags.DummyBattleRoyale);

            OptionTimeLimit = FloatOptionItem.Create(10001, "DummyBattleRoyaleTimeLimit", new(10f, 300f, 10f), 60f, TabGroup.MainSettings, false)
                .SetOptionName(() => "制限時間")
                .SetValueFormat(OptionFormat.Seconds)
                .SetTag(CustomOptionTags.DummyBattleRoyale)
                .SetParent(OptionEnable);

            OptionDummyCount = FloatOptionItem.Create(10002, "DummyBattleRoyaleDummyCount", new(1f, 30f, 1f), 5f, TabGroup.MainSettings, false)
                .SetOptionName(() => "ダミーの設置数")
                .SetTag(CustomOptionTags.DummyBattleRoyale)
                .SetParent(OptionEnable);

            OptionShowArrow = BooleanOptionItem.Create(10003, "DummyBattleRoyaleShowArrow", true, TabGroup.MainSettings, false)
                .SetOptionName(() => "一番近いダミーに矢印を表示")
                .SetTag(CustomOptionTags.DummyBattleRoyale)
                .SetParent(OptionEnable);
        }

        public static void OnGameStart()
        {
            // 有効になっていない場合は何もしない
            if (!OptionEnable.GetBool()) return;

            IsActive = true;
            TimeLeft = OptionTimeLimit.GetFloat();
            MyScore = 0;
            ActiveDummies.Clear();

            int dummyCount = (int)OptionDummyCount.GetFloat();
            for (int i = 0; i < dummyCount; i++)
            {
                var dummy = new RoyaleDummy();
                _ = new LateTask(() =>
                {
                    dummy.Spawn(GetRandomPosition());
                    ActiveDummies.Add(dummy);
                }, i * 0.5f, "SpawnRoyaleDummy");
            }
        }

        public static void OnFixedUpdate()
        {
            if (!IsActive || !GameStates.InGame) return;

            TimeLeft -= Time.fixedDeltaTime;
            UpdateUI();

            if (TimeLeft <= 0f)
            {
                IsActive = false;
                if (AmongUsClient.Instance.AmHost)
                {
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
                    }
                }
            }
        }

        public static void OnGameEnd()
        {
            IsActive = false;
            foreach (var dummy in ActiveDummies)
            {
                dummy.Despawn();
            }
            ActiveDummies.Clear();
        }

        private static void UpdateUI()
        {
            if (HudManager.Instance == null) return;

            var taskTextTransform = HudManager.Instance.transform.Find("TaskStuff/TaskText");
            if (taskTextTransform == null) return;

            var taskText = taskTextTransform.GetComponent<TMPro.TextMeshPro>();
            if (taskText == null) return;

            string uiText = $"<size=150%><color=#ffcc00>【ダミーバトロワ】</color></size>\n";
            uiText += $"残り時間: <color=#ff0000>{Mathf.CeilToInt(TimeLeft)}秒</color>\n";
            uiText += $"あなたのキル数: <size=130%><color=#00ff00>{MyScore}体</color></size>\n";

            if (OptionShowArrow.GetBool() && PlayerControl.LocalPlayer != null)
            {
                var myPos = PlayerControl.LocalPlayer.GetTruePosition();
                var closestDummy = ActiveDummies
                    .OrderBy(d => Vector2.Distance(myPos, d.Position))
                    .FirstOrDefault();

                if (closestDummy != null)
                {
                    float dist = Vector2.Distance(myPos, closestDummy.Position);
                    string arrow = GetArrowDirection(myPos, closestDummy.Position);
                    uiText += $"\n<color=#00ccff>次のターゲット: {arrow} {dist:F1}m</color>";
                }
            }

            taskText.text = uiText;
        }

        public static Vector2 GetRandomPosition()
        {
            if (ShipStatus.Instance == null || ShipStatus.Instance.AllVents == null || ShipStatus.Instance.AllVents.Length == 0)
                return new Vector2(0, 0);

            int randomIndex = UnityEngine.Random.Range(0, ShipStatus.Instance.AllVents.Length);
            return ShipStatus.Instance.AllVents[randomIndex].transform.position;
        }

        private static string GetArrowDirection(Vector2 from, Vector2 to)
        {
            var dir = (Vector3)to - (Vector3)from;
            dir.z = 0;

            if (dir.magnitude < 2) return "・";

            var angle = Vector3.SignedAngle(Vector3.down, dir, Vector3.back) + 180f + 22.5f;
            int index = ((int)(angle / 45f)) % 8;

            string[] Arrows = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };
            return Arrows[index];
        }
    }
}