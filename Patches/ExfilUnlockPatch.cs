using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using CommonAssets.Scripts.Game;
using EFT;
using EFT.Interactive;
using EFT.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ExfilImprovements.Patches
{
    /// <summary>
    /// 全员所有撤离点开放：InfiltrationMatch 恒通过（PMC/SCAV 都能进任何撤离点），
    /// RollChance 恒通过（不因概率把撤离点设为 NotPresent）。
    /// 三个 InfiltrationMatch 都要 patch——它是虚方法，玩家身份不同会分派到不同实现
    /// （基类 ExfiltrationPoint / ScavExfiltrationPoint / SharedExfiltrationPoint）。
    /// </summary>
    public static class ExfilUnlockPatch
    {
        [HarmonyPatch(typeof(ExfiltrationPoint), "InfiltrationMatch")]
        public static class ExfilInfiltrationPatch
        {
            public static bool Prefix(ref bool __result)
            {
                if (!Plugin.EnableAllExits.Value)
                {
                    return true;
                }
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(ScavExfiltrationPoint), "InfiltrationMatch")]
        public static class ScavInfiltrationPatch
        {
            public static bool Prefix(ref bool __result)
            {
                if (!Plugin.EnableAllExits.Value)
                {
                    return true;
                }
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(SharedExfiltrationPoint), "InfiltrationMatch")]
        public static class SharedInfiltrationPatch
        {
            public static bool Prefix(ref bool __result)
            {
                if (!Plugin.EnableAllExits.Value)
                {
                    return true;
                }
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(ExfiltrationController), "RollChance")]
        public static class RollChancePatch
        {
            public static bool Prefix(ref bool __result)
            {
                if (!Plugin.EnableAllExits.Value)
                {
                    return true;
                }
                __result = true;
                return false;
            }
        }

        /// <summary>
        /// 跨势力使用的关键修复：纯 ScavExfiltrationPoint 不经过 InitAllExfiltrationPoints 初始化，
        /// 由服务器按 EligibleIds 分配，对 PMC 玩家视角状态是 NotPresent（未分配）。而 Proceed 只在
        /// Status == RegularMode/Countdown 时才触发 OnStartExtraction 开始撤离——NotPresent 直接不撤离。
        /// 因此即使 InfiltrationMatch 已恒 true（双方都能"进入"区域），PMC 也无法在 Scav 撤离点撤离。
        ///
        /// Postfix：玩家满足撤离要求（UnmetRequirements 空）但撤离点处于不可用状态时，强制置为
        /// RegularMode。SetStatus 的 setter 会对 Entered 玩家重放 Proceed（此时 Status 已是
        /// RegularMode，原方法的 RegularMode 分支触发 OnStartExtraction），从而真正开始撤离计时。
        /// </summary>
        [HarmonyPatch(typeof(ExfiltrationPoint), "Proceed")]
        public static class ForceOpenPatch
        {
            public static void Postfix(ExfiltrationPoint __instance, Player player)
            {
                if (!Plugin.EnableAllExits.Value)
                {
                    return;
                }
                if (player == null || __instance == null)
                {
                    return;
                }
                try
                {
                    // 有撤离要求未满足（如付费/交物品）则不强开，尊重撤离点自身要求
                    if (__instance.UnmetRequirements(player).Any())
                    {
                        return;
                    }
                    if (__instance.Status == EExfiltrationStatus.RegularMode
                        || __instance.Status == EExfiltrationStatus.Countdown)
                    {
                        return; // 已在正常撤离流程
                    }
                    __instance.SetStatusLogged(EExfiltrationStatus.RegularMode, "ExfilImprovements.ForceOpen");
                }
                catch (System.Exception ex)
                {
                    Plugin.LogSource.LogError($"[{PluginsInfo.NAME}] ForceOpen 失败: {ex}");
                }
            }
        }

        /// <summary>
        /// 双击 O 的撤离点列表（ExtractionTimersPanel.SetTime 数据源 = ExfiltrationController.EligiblePoints）：
        /// 原版按 EntryPoint 过滤，只返回 PMC 自身撤离点。这里改为返回全部撤离点
        /// （ExfiltrationPoints + ScavExfiltrationPoints 去重，含 Shared），并顺手把 Status 为
        /// NotPresent 的 Scav 撤离点强制置为 RegularMode，避免 O 列表显示红锁。
        /// </summary>
        [HarmonyPatch(typeof(ExfiltrationController), "EligiblePoints", new Type[] { typeof(string) })]
        public static class EligiblePointsPatch
        {
            public static bool Prefix(ExfiltrationController __instance, ref ExfiltrationPoint[] __result)
            {
                if (!Plugin.EnableAllExits.Value)
                {
                    return true;
                }
                var all = new List<ExfiltrationPoint>();
                // 用 GetInstanceID 去重（Unity 对象唯一），不能用 List.Contains——其 Equals 对 Unity 对象不可靠，
                // SharedExfiltrationPoint 同时存在于 ExfiltrationPoints 和 ScavExfiltrationPoints，会重复加入。
                var seen = new HashSet<int>();
                if (__instance.ExfiltrationPoints != null)
                {
                    foreach (ExfiltrationPoint p in __instance.ExfiltrationPoints)
                    {
                        if (p != null && seen.Add(p.GetInstanceID()))
                        {
                            all.Add(p);
                        }
                    }
                }
                if (__instance.ScavExfiltrationPoints != null)
                {
                    foreach (ScavExfiltrationPoint s in __instance.ScavExfiltrationPoints)
                    {
                        if (s != null)
                        {
                            if (seen.Add(s.GetInstanceID()))
                            {
                                all.Add(s);
                            }
                            if (s.Status == EExfiltrationStatus.NotPresent)
                            {
                                try
                                {
                                    s.SetStatusLogged(EExfiltrationStatus.RegularMode, "ExfilImprovements.OList");
                                }
                                catch (System.Exception)
                                {
                                }
                            }
                        }
                    }
                }
                __result = all.ToArray();
                return false;
            }
        }

        /// <summary>
        /// 防御：ExtractionTimersPanel.SetTime 用 Settings.Name 作为 _timers 字典的 key，
        /// 重复 Name 会抛 "An item with the same key has already been added"（Fika COOP 等
        /// 会自己组合 points 数组）。前缀按 Name 过滤重复，保证任何来源都不崩溃。
        /// </summary>
        [HarmonyPatch(typeof(ExtractionTimersPanel), "SetTime")]
        public static class SetTimeDedupPatch
        {
            public static void Prefix(ref ExfiltrationPoint[] points)
            {
                if (points == null || points.Length < 2)
                {
                    return;
                }
                var seen = new HashSet<string>();
                var list = new List<ExfiltrationPoint>();
                for (int i = 0; i < points.Length; i++)
                {
                    if (points[i] == null || points[i].Settings == null)
                    {
                        continue;
                    }
                    if (seen.Add(points[i].Settings.Name))
                    {
                        list.Add(points[i]);
                    }
                }
                points = list.ToArray();
            }
        }

        /// <summary>
        /// 撤离缓冲确认倒计时颜色：与转移点同款的 LocationTransitTimerPanel（宽度/布局/文本与转移点一致，
        /// 默认橙黄），仅当由撤离缓冲调用（ExfilBufferBehaviour.IsBufferCountdownShown 为 true）时染绿。
        /// 只染不透明主体 Image（跳过低 alpha 的光晕，避免辉光动画被破坏/屏幕泛绿），
        /// 并保存原色，撤离缓冲结束（OnBufferFinished → RestoreColors）时恢复——否则颜色被永久改写，
        /// 转移点确认倒计时下次 Show 会变绿（组件污染）。
        /// </summary>
        [HarmonyPatch(typeof(LocationTransitTimerPanel), "Show")]
        public static class LocationTransitGreenPatch
        {
            private const float MIN_ALPHA = 0.5f; // 低于此视为半透明光晕，不染
            private static readonly Dictionary<int, Color> _originalColors = new Dictionary<int, Color>();
            private static readonly Dictionary<int, Image> _coloredImages = new Dictionary<int, Image>();

            public static void Postfix(LocationTransitTimerPanel __instance)
            {
                if (!ExfilBufferBehaviour.IsBufferCountdownShown)
                {
                    return; // 转移点确认倒计时保持橙黄
                }
                ExfilBufferBehaviour.IsBufferCountdownShown = false;
                GameUI ui = MonoBehaviourSingleton<GameUI>.Instance;
                if (ui == null || ui.BattleUiPanelExitTrigger == null)
                {
                    return;
                }
                Color exfilGreen = ui.BattleUiPanelExitTrigger._countdownColor;
                Image[] images = __instance.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    Image img = images[i];
                    if (img == null || img.color.a < MIN_ALPHA)
                    {
                        continue; // 跳过半透明光晕
                    }
                    int id = img.GetInstanceID();
                    if (!_originalColors.ContainsKey(id))
                    {
                        _originalColors[id] = img.color;
                        _coloredImages[id] = img;
                    }
                    img.color = exfilGreen;
                }
            }

            /// <summary>恢复撤离缓冲期间染绿的 Image 原色，避免污染转移点组件。</summary>
            public static void RestoreColors()
            {
                foreach (KeyValuePair<int, Image> kv in _coloredImages)
                {
                    if (kv.Value != null && _originalColors.TryGetValue(kv.Key, out Color original))
                    {
                        kv.Value.color = original;
                    }
                }
                _originalColors.Clear();
                _coloredImages.Clear();
            }
        }
    }
}
