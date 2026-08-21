using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.Interactive;
using EFT.UI;
using UnityEngine;

namespace ExfilImprovements
{
    /// <summary>
    /// 撤离点确认缓冲管理器（完全复用原版交互组件，与转移点机制一致）：
    ///   1) 玩家进入撤离区域 → Proceed 挂起（见 ExfilBufferPatch），登记到 Buffering，
    ///      通过 GamePlayerOwner.InteractionsChangedHandler 注入原版交互状态（"互动"按钮，
    ///      Name 与转移点一致为 "Transit/Interaction"）；
    ///   2) 玩家按下互动键 → InteractionAction 触发 StartCountdown：
    ///      Buffering→Opening + 显示与转移点同款的确认倒计时组件 LocationTransitTimerPanel
    ///      （宽度/布局与转移点一致，文本 "Confirmation {0:F1}" 由组件本地化处理，颜色由
    ///      LocationTransitGreenPatch 改为撤离绿）+ 玩家进入原版长按动作 Plant；
    ///   3) 按住保持 → Plant 成功回调 → 撤离点开放（Proceed）；中途松开/打断 → 取消回到等待。
    /// </summary>
    public class ExfilBufferBehaviour : MonoBehaviour
    {
        public class BufferedEntry
        {
            public Player Player;
            public ExfiltrationPoint Point;
        }

        /// <summary>阶段1：进入撤离区域、等待互动键。键 = player.Id。</summary>
        public static readonly Dictionary<int, BufferedEntry> Buffering = new Dictionary<int, BufferedEntry>();
        /// <summary>阶段2：已按互动键、长按倒计时中。键 = player.Id。</summary>
        public static readonly Dictionary<int, BufferedEntry> Opening = new Dictionary<int, BufferedEntry>();
        /// <summary>阶段3：已开放（同一次停留内不再重复缓冲）。键 = player.Id。</summary>
        public static readonly Dictionary<int, BufferedEntry> BufferedDone = new Dictionary<int, BufferedEntry>();

        private static AvailableInteractionState _interactionState;

        /// <summary>标识当前 LocationTransitTimerPanel.Show 是否来自撤离缓冲（用于染绿，转移点确认保持橙黄）。</summary>
        public static bool IsBufferCountdownShown;

        /// <summary>
        /// 缓冲交互状态（原版交互按钮，Name 与转移点一致）：玩家长按互动键时触发 StartCountdown。
        /// </summary>
        public static AvailableInteractionState GetInteractionState()
        {
            if (_interactionState == null)
            {
                _interactionState = new AvailableInteractionState();
                InteractionAction action = new InteractionAction
                {
                    Name = "Transit/Interaction", // 与转移点一致（本地化显示"互动"）
                    Action = StartCountdown
                };
                _interactionState.Actions.Add(action);
                _interactionState.SelectedAction = action;
            }
            return _interactionState;
        }

        /// <summary>
        /// 互动键按下触发：进入长按倒计时阶段。
        /// 用与转移点同款的 LocationTransitTimerPanel 确认倒计时组件（宽度/文本/本地化与转移点一致），
        /// 颜色由 LocationTransitGreenPatch 改为撤离绿。玩家进入 CurrentManagedState.Plant 长按动作，
        /// 保持按住 BufferSeconds 秒成功，松开则取消。
        /// </summary>
        public static void StartCountdown()
        {
            Player my = GamePlayerOwner.MyPlayer;
            if (my == null)
            {
                return;
            }
            if (!Buffering.TryGetValue(my.Id, out BufferedEntry entry))
            {
                return; // 不在等待互动阶段
            }
            // 必须站立（参考转移点：非 Idle 时提示 "NeedIdle" 并取消）
            if (!(my.CurrentState is IdlePlayerState))
            {
                NotificationManager.DisplayMessageNotification("NeedIdle".Localized(null),
                    ENotificationDurationType.Default, ENotificationIconType.Default, null);
                return;
            }

            Buffering.Remove(my.Id);
            Opening[my.Id] = entry;

            GameUI ui = MonoBehaviourSingleton<GameUI>.Instance;
            if (ui != null)
            {
                // 标记为撤离缓冲调用，LocationTransitGreenPatch 据此染绿；转移点确认保持橙黄
                IsBufferCountdownShown = true;
                ui.LocationTransitTimerPanel.Show(Plugin.BufferSeconds.Value, "Confirmation {0:F1}");
            }
            my.CurrentManagedState.Plant(true, true, Plugin.BufferSeconds.Value,
                (bool successful) => OnBufferFinished(entry, successful));
        }

        /// <summary>长按动作回调：成功 → 撤离点开放；失败（松开/被打断）→ 取消，回到等待。</summary>
        public static void OnBufferFinished(BufferedEntry entry, bool successful)
        {
            GameUI ui = MonoBehaviourSingleton<GameUI>.Instance;
            if (ui != null)
            {
                ui.LocationTransitTimerPanel.Close();
            }
            // 恢复撤离缓冲期间染绿的组件颜色，避免污染转移点确认倒计时
            ExfilImprovements.Patches.ExfilUnlockPatch.LocationTransitGreenPatch.RestoreColors();
            if (entry == null || entry.Player == null || entry.Point == null)
            {
                return;
            }
            int id = entry.Player.Id;
            if (!successful)
            {
                // 松开取消：回到等待互动阶段
                Opening.Remove(id);
                Buffering[id] = entry;
                return;
            }
            // 长按完成：撤离点开放
            Opening.Remove(id);
            BufferedDone[id] = entry;
            try
            {
                entry.Point.Proceed(entry.Player, false);
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError($"[{PluginsInfo.NAME}] 长按结束开放撤离点失败: {ex}");
            }
        }

        private void Update()
        {
            if (!Plugin.EnableBuffer.Value)
            {
                return;
            }

            // 清理：玩家销毁 / 已离开撤离区域（不在 Entered 里）
            var toRemove = new List<int>();
            CollectLeavers(Buffering, toRemove);
            CollectLeavers(Opening, toRemove);
            CollectLeavers(BufferedDone, toRemove);
            for (int i = 0; i < toRemove.Count; i++)
            {
                Buffering.Remove(toRemove[i]);
                Opening.Remove(toRemove[i]);
                BufferedDone.Remove(toRemove[i]);
            }
        }

        private static void CollectLeavers(Dictionary<int, BufferedEntry> dict, List<int> toRemove)
        {
            foreach (KeyValuePair<int, BufferedEntry> kv in dict)
            {
                BufferedEntry e = kv.Value;
                if (e.Player == null || e.Point == null || !e.Point.Entered.Contains(e.Player))
                {
                    toRemove.Add(kv.Key);
                }
            }
        }
    }
}
