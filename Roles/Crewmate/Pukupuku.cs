using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate
{
    public sealed class Pukupuku : RoleBase
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Pukupuku),
                player => new Pukupuku(player),
                CustomRoles.Pukupuku,
                () => RoleTypes.Crewmate,
                CustomRoleTypes.Crewmate,
                65000,
                SetupOptionItem,
                "Pukupuku",
                "#55ccff",
                (7, 0),
                from: From.TownOfHost_Pko
            );

        private static OptionItem ModeOption;
        private static OptionItem OptionGuardCount;
        private static OptionItem OptionNotifyOnGuard;
        private static OptionItem OptionPostDeathRevengeEnabled;
        private static OptionItem OptionPostDeathRevengeTurnLimit;

        private enum AbilityMode { Off, Guard, Reflect, Revenge }
        private enum OptionName
        {
            PukupukuGuardCount,
            PukupukuPostDeathTurnLimit,
        }

        private bool tasksCompleted = false;
        private bool completedWhileAlive = false;
        private bool revengeDone = false;
        private int guardUsedCount = 0;
        private int guardMaxCount = 1;
        private bool notifyOnGuard = false;
        private bool postDeathRevenge = false;
        private int postDeathTurnLimit = 1;
        private int meetingCountAfterDeath = 0;
        private bool isDead = false;
        private PlayerControl killerRef = null;

        public Pukupuku(PlayerControl player)
            : base(RoleInfo, player)
        {
            guardMaxCount = OptionGuardCount.GetInt();
            notifyOnGuard = OptionNotifyOnGuard.GetBool();
            postDeathRevenge = OptionPostDeathRevengeEnabled.GetBool();
            postDeathTurnLimit = OptionPostDeathRevengeTurnLimit.GetInt();
        }

        private static void SetupOptionItem()
        {
            ModeOption = StringOptionItem.Create(
                RoleInfo, 10, "PukupukuAliveMode",
                new string[] { "Off", "Guard", "Reflect", "Revenge" },
                1, false);

            OptionGuardCount = IntegerOptionItem.Create(
                RoleInfo, 11, OptionName.PukupukuGuardCount,
                new(1, 10, 1), 1, false)
                .SetParent(ModeOption);

            OptionNotifyOnGuard = BooleanOptionItem.Create(
                RoleInfo, 12, "PukupukuNotifyOnGuard",
                true, false)
                .SetParent(ModeOption);

            OptionPostDeathRevengeEnabled = BooleanOptionItem.Create(
                RoleInfo, 20, "PukupukuPostDeathRevenge",
                false, false);

            OptionPostDeathRevengeTurnLimit = IntegerOptionItem.Create(
                RoleInfo, 21, OptionName.PukupukuPostDeathTurnLimit,
                new(1, 10, 1), 1, false)
                .SetParent(OptionPostDeathRevengeEnabled)
                .SetValueFormat(OptionFormat.day);
        }

        private AbilityMode GetMode() => (AbilityMode)ModeOption.GetValue();

        public override bool OnCompleteTask(uint taskid)
        {
            if (tasksCompleted) return true;
            if (!MyTaskState.IsTaskFinished) return true;

            if (!Player.Data.IsDead)
            {
                tasksCompleted = true;
                completedWhileAlive = true;
                Utils.SendMessage(
                    $"<color=#55ccff>【プクプク】タスクを完了しました！能力が解放されました。</color>",
                    Player.PlayerId);
            }
            else
            {
                if (!completedWhileAlive && postDeathRevenge && killerRef != null)
                {
                    if (postDeathTurnLimit <= 0 || meetingCountAfterDeath <= postDeathTurnLimit)
                        TryRevenge();
                }
            }

            return true;
        }

        public override bool OnCheckMurderAsTarget(MurderInfo info)
        {
            if (!tasksCompleted || !completedWhileAlive) return true;

            var killer = info.AttemptKiller;
            var target = info.AttemptTarget;

            switch (GetMode())
            {
                case AbilityMode.Guard:
                    if (guardUsedCount >= guardMaxCount) return true;

                    guardUsedCount++;
                    info.GuardPower = 2;
                    killer.RpcProtectedMurderPlayer(target);
                    killer.SetKillCooldown(target: target, force: true);

                    if (notifyOnGuard)
                    {
                        Utils.SendMessage(
                            $"<color=#55ccff>【プクプク】キルガードを発動しました！\n残り{guardMaxCount - guardUsedCount}回</color>",
                            Player.PlayerId);
                    }

                    Logger.Info($"{Player.Data.GetLogPlayerName()} ガード ({guardUsedCount}/{guardMaxCount})", "Pukupuku");
                    return false;

                case AbilityMode.Reflect:
                    info.DoKill = false;
                    Logger.Info($"{Player.Data.GetLogPlayerName()} キルを反射 → {killer.Data.GetLogPlayerName()}", "Pukupuku");

                    _ = new LateTask(() =>
                    {
                        if (!killer.IsAlive()) return;
                        PlayerState.GetByPlayerId(killer.PlayerId).DeathReason = CustomDeathReason.Revenge;
                        CustomRoleManager.OnCheckMurder(
                            Player, killer, Player, killer, true, false,
                            deathReason: CustomDeathReason.Revenge);
                        UtilsGameLog.AddGameLog("Pukupuku",
                            $"{UtilsName.GetPlayerColor(Player)}が{UtilsName.GetPlayerColor(killer)}のキルを反射した");
                    }, 0.1f, "Pukupuku.Reflect", true);

                    return false;

                case AbilityMode.Revenge:
                    killerRef = killer;
                    return true;

                default:
                    return true;
            }
        }

        public override void OnMurderPlayerAsTarget(MurderInfo info)
        {
            isDead = true;
            killerRef = info.AttemptKiller;

            if (tasksCompleted && completedWhileAlive && GetMode() == AbilityMode.Revenge)
                TryRevenge();
        }

        public override void OnStartMeeting()
        {
            if (isDead) meetingCountAfterDeath++;
        }

        private void TryRevenge()
        {
            if (revengeDone) return;
            if (killerRef == null || !killerRef.IsAlive()) return;

            revengeDone = true;
            PlayerState.GetByPlayerId(killerRef.PlayerId).DeathReason = CustomDeathReason.Poisoned;
            killerRef.RpcMurderPlayerV2(killerRef);

            UtilsGameLog.AddGameLog("Pukupuku",
                $"{UtilsName.GetPlayerColor(Player)}が{UtilsName.GetPlayerColor(killerRef)}を道連れにした");

            Logger.Info($"{Player.Data.GetLogPlayerName()} 道連れ → {killerRef.Data.GetLogPlayerName()}", "Pukupuku");
        }

        public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
            bool isForMeeting = false, bool isForHud = false)
        {
            seen ??= seer;
            if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
            if (isForMeeting) return "";

            string size = isForHud ? "" : "<size=60%>";
            string color = RoleInfo.RoleColorCode;

            if (!tasksCompleted)
                return $"{size}<color={color}>タスクを完了して能力を解放しよう</color>";

            return GetMode() switch
            {
                AbilityMode.Guard => $"{size}<color={color}>ガード: {guardUsedCount}/{guardMaxCount}回使用済み</color>",
                AbilityMode.Reflect => $"{size}<color={color}>キル反射：有効</color>",
                AbilityMode.Revenge => $"{size}<color={color}>道連れ：有効</color>",
                _ => ""
            };
        }
    }
}