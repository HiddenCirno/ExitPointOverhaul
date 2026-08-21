using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.GlobalEvents;
using EFT.Interactive;
using HarmonyLib;
using UnityEngine;

namespace ExfilImprovements.Patches
{
    /// <summary>
    /// 信号弹起效提示：FlareShootDetectorZone.OnFlareEventRaised 成功后把发射者加入内部
    /// _shotPlayerProfiles（类型匹配 + 发射位置在区域内才算成功），但没有任何玩家 UI 反馈。
    /// Postfix 检测本机玩家是否刚被记录，命中则用 NotificationManager 弹通知。
    /// </summary>
    [HarmonyPatch(typeof(FlareShootDetectorZone), "OnFlareEventRaised")]
    public static class FlareHintPatch
    {
        private static readonly FieldInfo _shotProfilesField =
            typeof(FlareShootDetectorZone).GetField("_shotPlayerProfiles", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Postfix(FlareShootDetectorZone __instance, FlareSuccessEvent flareEvent)
        {
            if (!Plugin.EnableFlareHint.Value)
            {
                return;
            }
            Player myPlayer = GamePlayerOwner.MyPlayer;
            if (myPlayer == null || flareEvent == null || flareEvent.FiredPlayer == null)
            {
                return;
            }
            if (flareEvent.FiredPlayer.ProfileId != myPlayer.ProfileId)
            {
                return; // 只提示本机玩家自己的信号弹
            }
            if (_shotProfilesField == null)
            {
                return;
            }
            var set = _shotProfilesField.GetValue(__instance) as HashSet<string>;
            if (set != null && set.Contains(myPlayer.ProfileId))
            {
                NotificationManager.DisplayMessageNotification(
                    "信号弹已起效，撤离点可通行！",
                    ENotificationDurationType.Default,
                    ENotificationIconType.Default,
                    null);
            }
        }
    }

    /// <summary>
    /// 信号弹再次进入提示：玩家发射过成功信号弹（_shotPlayerProfiles 已含本机）后，
    /// 再次进入该信号区域时也弹出"已可通行"提示（而不是只在发射瞬间提示一次）。
    /// </summary>
    [HarmonyPatch(typeof(FlareShootDetectorZone), "OnTriggerEnterHandler")]
    public static class FlareEnterHintPatch
    {
        private static readonly FieldInfo _shotProfilesField =
            typeof(FlareShootDetectorZone).GetField("_shotPlayerProfiles", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Postfix(FlareShootDetectorZone __instance, Collider collider)
        {
            if (!Plugin.EnableFlareHint.Value)
            {
                return;
            }
            Player myPlayer = GamePlayerOwner.MyPlayer;
            if (myPlayer == null || collider == null)
            {
                return;
            }
            // 只处理本机玩家进入
            Player entering = Singleton<GameWorld>.Instance.GetPlayerByCollider(collider);
            if (entering == null || entering.ProfileId != myPlayer.ProfileId)
            {
                return;
            }
            if (_shotProfilesField == null)
            {
                return;
            }
            var set = _shotProfilesField.GetValue(__instance) as HashSet<string>;
            if (set != null && set.Contains(myPlayer.ProfileId))
            {
                NotificationManager.DisplayMessageNotification(
                    "信号弹已起效，撤离点可通行！",
                    ENotificationDurationType.Default,
                    ENotificationIconType.Default,
                    null);
            }
        }
    }
}
