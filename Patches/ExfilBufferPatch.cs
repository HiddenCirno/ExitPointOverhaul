using EFT;
using EFT.Communications;
using EFT.Interactive;
using EFT.Interactive.SecretExfiltrations;
using EFT.UI;
using HarmonyLib;

namespace ExfilImprovements.Patches
{
    /// <summary>
    /// 撤离点确认缓冲：玩家进入撤离区域（OnTriggerEnter → Proceed）时，若未进入本停留的
    /// 开放阶段，则拦截 Proceed（return false），登记到缓冲管理器，并触发原版交互刷新
    /// （ForceInteractionsChanged → InteractionInjectPatch 注入原版交互按钮）。
    /// 之后由 ExfilBufferBehaviour 推进"按下互动键 → 原版倒计时 → 开放"流程。
    ///
    /// 二次交互撤离点（秘密 / V-Ex 黑车 / 手动激活）跳过缓冲，保持原版行为——
    /// 它们本身需要额外的交互步骤（交物品、交钱上车、激活开关），不应再叠加确认缓冲。
    /// </summary>
    [HarmonyPatch(typeof(ExfiltrationPoint), "Proceed")]
    public static class ExfilBufferPatch
    {
        /// <summary>
        /// 判断撤离点是否"本身需要二次交互"（跳过缓冲，保持原版）：
        ///   - 秘密撤离点 SecretExfiltrationPoint（需交指定物品并发现）；
        ///   - V-Ex 黑车撤离点：交付资金上车撤离，是唯一限制人数的撤离点
        ///     （Settings.PlayersCount > 0，对应 AbstractGame 的 BattleUiPmcCount 座位 UI）；
        ///   - Manual 类型（EExfiltrationType.Manual → AwaitsManualActivation，需激活开关）；
        ///   - 需要恢复供电（WorldEventRequirement，如储备站 D2 / 实验室电梯 / 海关 ZB-013）：
        ///     撤离点激活依赖 GameTrigger（TriggerZone→HandlerExfiltration→ExternalSetStatus）把 Status
        ///     置为 RegularMode/Countdown。WorldEventRequirement.Met 只看 Status（循环判定），缓冲完成
        ///     直接调 Proceed 会把"供电未恢复"时 Status 已被自动激活的撤离点放行，绕过了供电前提，
        ///     所以这类撤离点必须跳过缓冲，完全交给原版 Proceed 决定。
        /// 其余普通条件撤离点（物品/排除物品/合作等）仍走缓冲，但条件本身不修改。
        /// </summary>
        public static bool IsSecondaryInteractionExfil(ExfiltrationPoint point)
        {
            if (point == null)
            {
                return false;
            }
            // 未初始化副本（从未 LoadSettings，如场景里同名但从未初始化的 Scav 撤离点，
            // 如实验室电梯的 Scav 副本）：Status 仍是默认 Pending，无任何配置/要求。
            // 跳过缓冲走原版，避免缓冲完成后误撤离（且 ForceOpenPatch 也不会强开它）。
            if (point.Status == EExfiltrationStatus.Pending)
            {
                return true;
            }
            if (point is SecretExfiltrationPoint)
            {
                return true;
            }
            if (point.Settings != null)
            {
                if (point.Settings.ExfiltrationType == EExfiltrationType.Manual)
                {
                    return true;
                }
                if (point.Settings.PlayersCount > 0) // V-Ex 黑车（唯一限人数撤离点）
                {
                    return true;
                }
            }
            // 需要恢复供电的撤离点：跳过缓冲，保持原版（供电未满足时原版 Proceed 会挡）
            if (point.Requirements != null)
            {
                for (int i = 0; i < point.Requirements.Length; i++)
                {
                    if (point.Requirements[i] is WorldEventRequirement)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool Prefix(ExfiltrationPoint __instance, Player player)
        {
            if (!Plugin.EnableBuffer.Value)
            {
                return true;
            }
            if (player == null || IsSecondaryInteractionExfil(__instance))
            {
                return true; // 二次交互撤离点：放行原版 Proceed
            }
            int id = player.Id;
            if (ExfilBufferBehaviour.BufferedDone.ContainsKey(id))
            {
                return true; // 已开放 → 正常 Proceed
            }
            if (ExfilBufferBehaviour.Buffering.ContainsKey(id) || ExfilBufferBehaviour.Opening.ContainsKey(id))
            {
                return false; // 等待互动 / 倒计时中 → 跳过 Proceed
            }
            ExfilBufferBehaviour.Buffering[id] = new ExfilBufferBehaviour.BufferedEntry
            {
                Player = player,
                Point = __instance
            };
            // 进入撤离区域提示（与转移点一致：长按互动提示；文案走插件本地化词典）
            try
            {
                NotificationManager.DisplaySingletonNotification(
                    CfgLocaleManager.Get("exfil_enter_hint"),
                    ENotificationDurationType.Default);
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError($"[{PluginsInfo.NAME}] 进入提示失败: {ex}");
            }
            // 触发原版交互刷新：InteractionInjectPatch 会注入"互动"按钮
            try
            {
                player.ForceInteractionsChanged();
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError($"[{PluginsInfo.NAME}] 缓冲交互刷新失败: {ex}");
            }
            return false; // 进入等待互动阶段
        }
    }

    /// <summary>
    /// 原版交互注入：玩家在缓冲中（Buffering/Opening）时，把 GamePlayerOwner 的交互状态
    /// 覆盖为我们的"互动"按钮（AvailableInteractionState，长按互动键触发 StartCountdown），
    /// 使视线/交互按钮使用原版组件而非自绘。倒计时由 LocationTransitTimerPanel 显示。
    /// </summary>
    [HarmonyPatch(typeof(GamePlayerOwner), "InteractionsChangedHandler")]
    public static class InteractionInjectPatch
    {
        public static void Postfix(GamePlayerOwner __instance)
        {
            if (!Plugin.EnableBuffer.Value)
            {
                return;
            }
            Player my = GamePlayerOwner.MyPlayer;
            if (my == null)
            {
                return;
            }
            if (ExfilBufferBehaviour.Buffering.ContainsKey(my.Id)
                || ExfilBufferBehaviour.Opening.ContainsKey(my.Id))
            {
                __instance.AvailableInteractionState.Value = ExfilBufferBehaviour.GetInteractionState();
            }
        }
    }
}
