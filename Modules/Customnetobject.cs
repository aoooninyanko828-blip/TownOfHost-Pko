using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.InnerNet.GameDataMessages;
using Hazel;
using InnerNet;
using UnityEngine;
using HarmonyLib;

namespace TownOfHost
{
    public class CustomNetObject
    {
        public static readonly List<CustomNetObject> AllObjects = new();
        private static int MaxId = -1;

        private static readonly Queue<Action> SpawnQueue = new();
        private static bool IsSpawning = false;

        protected int Id;
        public PlayerControl PlayerControl;
        public Vector2 Position;

        protected virtual bool IsDynamic => false;

        public void Despawn()
        {
            if (!AmongUsClient.Instance.AmHost) return;

            try
            {
                if (PlayerControl != null && PlayerControl.gameObject != null)
                {
                    // ★ AllPlayerControls に残っていれば削除
                    try
                    {
                        if (PlayerControl.AllPlayerControls.Contains(PlayerControl))
                            PlayerControl.AllPlayerControls.Remove(PlayerControl);
                    }
                    catch { }

                    MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
                    writer.StartMessage(5);
                    writer.Write(AmongUsClient.Instance.GameId);
                    writer.StartMessage(5);
                    writer.WritePacked(PlayerControl.NetId);
                    writer.EndMessage();
                    writer.EndMessage();
                    AmongUsClient.Instance.SendOrDisconnect(writer);
                    writer.Recycle();

                    AmongUsClient.Instance.RemoveNetObject(PlayerControl);
                    UnityEngine.Object.Destroy(PlayerControl.gameObject);
                }
            }
            catch { }
            finally
            {
                AllObjects.Remove(this);
            }
        }

        protected void Hide(PlayerControl player)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (player.AmOwner)
            {
                _ = new LateTask(() =>
                {
                    try { PlayerControl.transform.FindChild("Names").FindChild("NameText_TMP").gameObject.SetActive(false); } catch { }
                    PlayerControl.Visible = false;
                }, 0.1f, "CNO.Hide.Local", true);
                return;
            }

            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(6);
            writer.Write(AmongUsClient.Instance.GameId);
            writer.WritePacked(player.OwnerId);
            writer.StartMessage(5);
            writer.WritePacked(PlayerControl.NetId);
            writer.EndMessage();
            writer.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();
        }

        protected virtual void OnFixedUpdate()
        {
            if (!IsDynamic) return;
            try
            {
                if (!AmongUsClient.Instance.AmHost) return;
                ushort num = (ushort)(PlayerControl.NetTransform.lastSequenceId + 2U);
                MessageWriter mw = AmongUsClient.Instance.StartRpcImmediately(
                    PlayerControl.NetTransform.NetId, 21, SendOption.None);
                NetHelpers.WriteVector2(Position, mw);
                mw.Write(num);
                AmongUsClient.Instance.FinishRpcImmediately(mw);
            }
            catch { }
        }

        protected void SetAppearance(int colorId, string skinId = "", string hatId = "", string petId = "", string visorId = "")
        {
            if (PlayerControl == null) return;

            var capturedPC = PlayerControl;
            var outfit = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default];
            string origName = outfit.PlayerName;
            int origColor = outfit.ColorId;
            string origHat = outfit.HatId;
            string origSkin = outfit.SkinId;
            string origPet = outfit.PetId;
            string origVisor = outfit.VisorId;

            var sender = CustomRpcSender.Create("CNO.SetAppearance", SendOption.Reliable);
            MessageWriter writer = sender.stream;
            sender.StartMessage();

            outfit.PlayerName = origName;
            outfit.ColorId = colorId;
            outfit.HatId = hatId ?? "";
            outfit.SkinId = skinId ?? "";
            outfit.PetId = petId ?? "";
            outfit.VisorId = visorId ?? "";

            writer.StartMessage(1);
            writer.WritePacked(PlayerControl.LocalPlayer.Data.NetId);
            PlayerControl.LocalPlayer.Data.Serialize(writer, false);
            writer.EndMessage();

            try { capturedPC.Shapeshift(PlayerControl.LocalPlayer, false); } catch { }

            sender.StartRpc(capturedPC.NetId, RpcCalls.Shapeshift)
                .WriteNetObject(PlayerControl.LocalPlayer)
                .Write(false)
                .EndRpc();

            outfit.PlayerName = origName;
            outfit.ColorId = origColor;
            outfit.HatId = origHat;
            outfit.SkinId = origSkin;
            outfit.PetId = origPet;
            outfit.VisorId = origVisor;

            writer.StartMessage(1);
            writer.WritePacked(PlayerControl.LocalPlayer.Data.NetId);
            PlayerControl.LocalPlayer.Data.Serialize(writer, false);
            writer.EndMessage();

            sender.EndMessage();
            sender.SendMessage();
        }

        protected void SetName(string name)
        {
            if (PlayerControl == null) return;
            if (PlayerControl.cosmetics?.nameText != null)
                PlayerControl.cosmetics.nameText.text = name;

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.NetId, (byte)RpcCalls.SetName, SendOption.Reliable);
            writer.Write(PlayerControl.Data.NetId);
            writer.Write(name);
            writer.Write(false);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void SnapToPosition(Vector2 position)
        {
            if (PlayerControl == null) return;
            Position = position;

            try { PlayerControl.NetTransform.SnapTo(position); } catch { }

            ushort sid = (ushort)(PlayerControl.NetTransform.lastSequenceId + 100U);
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.NetTransform.NetId, (byte)RpcCalls.SnapTo, SendOption.Reliable);
            NetHelpers.WriteVector2(position, writer);
            writer.Write(sid);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void CreateNetObject(Vector2 position)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (GameStates.IsLobby || GameStates.IsEnded) return;

            SpawnQueue.Enqueue(() => DoCreate(position));
            ProcessQueue();
        }

        private static void ProcessQueue()
        {
            if (IsSpawning || SpawnQueue.Count == 0) return;
            IsSpawning = true;
            var action = SpawnQueue.Dequeue();

            try { action(); }
            catch
            {
                IsSpawning = false;
                ProcessQueue();
            }
        }

        private void DoCreate(Vector2 position)
        {
            PlayerControl = UnityEngine.Object.Instantiate(
                AmongUsClient.Instance.PlayerPrefab, Vector2.zero, Quaternion.identity);
            PlayerControl.PlayerId = 254;
            PlayerControl.isNew = false;
            PlayerControl.notRealPlayer = true;

            try { PlayerControl.NetTransform.SnapTo(new Vector2(50f, 50f)); } catch { }

            AmongUsClient.Instance.NetIdCnt += 1U;

            MessageWriter msg = MessageWriter.Get(SendOption.Reliable);
            msg.StartMessage(5);
            msg.Write(AmongUsClient.Instance.GameId);
            msg.StartMessage(4);
            SpawnGameDataMessage item = AmongUsClient.Instance.CreateSpawnMessage(PlayerControl, -2, SpawnFlags.None);
            item.SerializeValues(msg);
            msg.EndMessage();

            for (uint i = 1; i <= 3; ++i)
            {
                msg.StartMessage(4);
                msg.WritePacked(2U);
                msg.WritePacked(-2);
                msg.Write((byte)SpawnFlags.None);
                msg.WritePacked(1);
                msg.WritePacked(AmongUsClient.Instance.NetIdCnt - i);
                msg.StartMessage(1);
                msg.EndMessage();
                msg.EndMessage();
            }

            msg.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(msg);
            msg.Recycle();

            // ★ AllPlayerControls には残す（バニラのキルターゲット選択に入れるため）
            // （削除しない）

            PlayerControl.cosmetics.colorBlindText.color = Color.clear;

            // ★ Data.IsDead = false を確実に設定（キルターゲット条件）
            try { PlayerControl.Data.IsDead = false; } catch { }

            Position = position;
            ++MaxId;
            Id = MaxId;
            if (MaxId == int.MaxValue) MaxId = -1;

            AllObjects.Add(this);

            var capturedPC = PlayerControl;
            var capturedSelf = this;

            _ = new LateTask(() =>
            {
                if (capturedPC == null || capturedPC.gameObject == null) return;

                foreach (var pc in PlayerCatch.AllPlayerControls)
                {
                    if (pc.AmOwner) continue;

                    var sender = CustomRpcSender.Create("CNO.AssignId", SendOption.Reliable);
                    MessageWriter writer = sender.stream;
                    sender.StartMessage(pc.OwnerId);

                    writer.StartMessage(1);
                    writer.WritePacked(capturedPC.NetId);
                    writer.Write(pc.PlayerId);
                    writer.EndMessage();

                    sender.StartRpc(capturedPC.NetId, RpcCalls.MurderPlayer)
                        .WriteNetObject(capturedPC)
                        .Write((int)MurderResultFlags.FailedError)
                        .EndRpc();

                    writer.StartMessage(1);
                    writer.WritePacked(capturedPC.NetId);
                    writer.Write((byte)254);
                    writer.EndMessage();

                    sender.EndMessage();
                    sender.SendMessage();
                }

                capturedPC.CachedPlayerData = PlayerControl.LocalPlayer.Data;
            }, 0.1f, "CNO.AssignId", true);

            _ = new LateTask(() =>
            {
                capturedSelf.OnCreated();
                IsSpawning = false;
                ProcessQueue();
            }, 0.4f, "CNO.OnCreated", true);
        }

        protected virtual void OnCreated() { }

        public virtual void OnMeeting()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            Despawn();
        }

        public static void FixedUpdate()
        {
            foreach (var cno in AllObjects.ToArray())
                cno?.OnFixedUpdate();
        }

        public static CustomNetObject Get(int id)
            => AllObjects.FirstOrDefault(x => x.Id == id);

        // ★ ホスト用：AllObjectsから検索
        public static CustomNetObject GetKillableTarget(PlayerControl killer, float range = 1.8f)
        {
            if (killer == null) return null;
            var pos = killer.GetTruePosition();
            return AllObjects
                .Where(o => o is IKillableDummy)
                .OrderBy(o => Vector2.Distance(pos, o.Position))
                .FirstOrDefault(o => Vector2.Distance(pos, o.Position) <= range);
        }

        // ★ 非ホスト用：AllPlayerControlsからダミーPCを直接検索
        public static PlayerControl GetNearbyDummyPC(PlayerControl killer, float range = 1.8f)
        {
            if (killer == null) return null;
            var pos = killer.GetTruePosition();

            PlayerControl closest = null;
            float closestDist = float.MaxValue;

            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || !pc.notRealPlayer) continue;
                float dist = Vector2.Distance(pos, pc.GetTruePosition());
                if (dist <= range && dist < closestDist)
                {
                    closestDist = dist;
                    closest = pc;
                }
            }
            return closest;
        }

        public static void Reset()
        {
            try
            {
                SpawnQueue.Clear();
                IsSpawning = false;

                foreach (var obj in AllObjects.ToList())
                {
                    try { obj?.Despawn(); } catch { }
                }
                AllObjects.Clear();
            }
            catch { }
        }
    }

    public interface IKillableDummy
    {
        void OnKilled(PlayerControl killer);
    }

    // =========================================================================
    // ライフサイクルパッチ
    // =========================================================================

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.OnDestroy))]
    public static class CNO_ShipStatus_Destroy_Patch
    {
        public static void Prefix() => CustomNetObject.Reset();
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
    public static class CNO_ExitGame_Patch
    {
        public static void Prefix() => CustomNetObject.Reset();
    }

    // =========================================================================
    // ★ キルボタンをダミーに向ける（ホスト・非ホスト両対応）
    //   AllPlayerControls にダミーが残っているので、バニラは自動でターゲット選択する。
    //   MODクライアントはここで明示的にセット。
    // =========================================================================

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class HudManager_Update_Patch
    {
        public static void Postfix(HudManager __instance)
        {
            if (PlayerControl.LocalPlayer == null || __instance.KillButton == null) return;
            if (!PlayerControl.LocalPlayer.CanMove || PlayerControl.LocalPlayer.Data.IsDead) return;

            if (AmongUsClient.Instance.AmHost)
            {
                // ★ ホスト：AllObjectsから検索してセット
                var dummy = CustomNetObject.GetKillableTarget(PlayerControl.LocalPlayer, 1.8f);
                if (dummy?.PlayerControl != null)
                    __instance.KillButton.SetTarget(dummy.PlayerControl);
            }
            else
            {
                // ★ 非ホストMODクライアント：AllPlayerControlsから検索
                var dummyPc = CustomNetObject.GetNearbyDummyPC(PlayerControl.LocalPlayer, 1.8f);
                if (dummyPc != null)
                    __instance.KillButton.SetTarget(dummyPc);
            }
            // ★ バニラはAllPlayerControlsにダミーが含まれているので自動でターゲットに入る
        }
    }

    // =========================================================================
    // ★ キルボタン押下（MODクライアント向け）
    // =========================================================================

    [HarmonyPatch(typeof(KillButton), nameof(KillButton.DoClick))]
    public static class KillButton_DoClick_Patch
    {
        public static bool Prefix(KillButton __instance)
        {
            if (PlayerControl.LocalPlayer == null) return true;
            if (!PlayerControl.LocalPlayer.CanMove || PlayerControl.LocalPlayer.Data.IsDead) return true;
            if (PlayerControl.LocalPlayer.killTimer > 0f) return true;

            if (AmongUsClient.Instance.AmHost)
            {
                // ★ ホスト：直接キル
                var dummy = CustomNetObject.GetKillableTarget(PlayerControl.LocalPlayer, 1.8f);
                if (dummy is IKillableDummy kd)
                {
                    kd.OnKilled(PlayerControl.LocalPlayer);
                    PlayerControl.LocalPlayer.SetKillCooldown(
                        Main.AllPlayerKillCooldown.GetValueOrDefault(
                            PlayerControl.LocalPlayer.PlayerId, 10f));
                    return false;
                }
            }
            else
            {
                // ★ 非ホストMODクライアント：RPCでホストに依頼
                var dummyPc = CustomNetObject.GetNearbyDummyPC(PlayerControl.LocalPlayer, 1.8f);
                if (dummyPc != null)
                {
                    var writer = AmongUsClient.Instance.StartRpcImmediately(
                        PlayerControl.LocalPlayer.NetId,
                        (byte)CustomRPC.SyncModSystem,
                        SendOption.Reliable,
                        0);
                    writer.Write((int)RPC.ModSystem.KillDummy);
                    writer.Write(PlayerControl.LocalPlayer.PlayerId);
                    writer.Write(dummyPc.NetId);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);

                    PlayerControl.LocalPlayer.SetKillCooldown(
                        Main.AllPlayerKillCooldown.GetValueOrDefault(
                            PlayerControl.LocalPlayer.PlayerId, 10f));
                    return false;
                }
            }
            return true;
        }
    }

    // =========================================================================
    // ★ CheckMurderフック
    //   バニラ・MOD両対応：ターゲットがダミーなら OnKilled を呼ぶ
    //   ターゲット関係なく近くにダミーがいればそちらを優先
    // =========================================================================

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
    public static class DummyKillPatch
    {
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            if (!AmongUsClient.Instance.AmHost) return true;
            if (__instance == null || !__instance.IsAlive()) return true;

            // ★ ターゲット自体がダミーPC（バニラがダミーをターゲットにした場合）
            if (target != null && target.notRealPlayer)
            {
                var dummyCno = CustomNetObject.AllObjects
                    .FirstOrDefault(o => o.PlayerControl == target);
                if (dummyCno is IKillableDummy kdTarget)
                {
                    kdTarget.OnKilled(__instance);
                    return false;
                }
            }

            // ★ ターゲットは通常プレイヤーだが、近くにダミーがいる場合はダミーを優先
            var dummy = CustomNetObject.GetKillableTarget(__instance, 2.0f);
            if (dummy is IKillableDummy kd)
            {
                kd.OnKilled(__instance);
                return false;
            }

            return true;
        }
    }
}