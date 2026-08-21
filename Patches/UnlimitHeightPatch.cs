using EFT.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ExfilImprovements.Patches
{
    /// <summary>
    /// 双击 O 撤离点列表面板"解除高度限制"：
    /// 原版 _container/_timersPanel 有固定高度，条目多时超出的被裁剪/显示不全。
    /// 这里在显示所有条目后，给容器加 ContentSizeFitter（垂直自适应内容高度），
    /// 停用可能的裁剪（RectMask2D/Mask）和固定高度约束（LayoutElement），让所有
    /// 撤离点/转移点完整堆叠显示。同时输出组件结构日志用于校准。
    /// </summary>
    [HarmonyPatch(typeof(ExtractionTimersPanel), "ShowTimer")]
    public static class UnlimitHeightPatch
    {
        public static void Postfix(ExtractionTimersPanel __instance, bool showExits)
        {
            try
            {
                if (!showExits)
                {
                    return;
                }
                if (!Plugin.EnableUnlimitHeight.Value)
                {
                    return;
                }
                RectTransform container = __instance._container;
                RectTransform panelRt = __instance._timersPanel;
                if (container == null || panelRt == null)
                {
                    return;
                }

                LogComponents(container, "container");
                LogComponents(panelRt, "timersPanel");

                Unlimit(container);
                Unlimit(panelRt);

                LayoutRebuilder.ForceRebuildLayoutImmediate(container);
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);

                Plugin.LogSource.LogInfo($"[UnlimitHeight] 已解除：container.size={container.sizeDelta} rect={container.rect} " +
                    $"panel.size={panelRt.sizeDelta} rect={panelRt.rect} child={container.childCount}");
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError($"[UnlimitHeight] 失败: {ex}");
            }
        }

        private static void Unlimit(RectTransform rt)
        {
            // 垂直自适应内容高度
            ContentSizeFitter fitter = rt.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            }
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 停用裁剪
            RectMask2D rectMask = rt.GetComponent<RectMask2D>();
            if (rectMask != null)
            {
                rectMask.enabled = false;
            }
            Mask mask = rt.GetComponent<Mask>();
            if (mask != null)
            {
                mask.enabled = false;
            }

            // 停用固定高度约束
            LayoutElement layoutElement = rt.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredHeight = -1;
            }
        }

        private static void LogComponents(RectTransform rt, string label)
        {
            Component[] comps = rt.GetComponents<Component>();
            string list = string.Empty;
            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null)
                {
                    continue;
                }
                string state = (c is Behaviour b) ? (b.enabled ? "(on)" : "(off)") : string.Empty;
                list += c.GetType().Name + state + " ";
            }
            Plugin.LogSource.LogInfo($"[UnlimitHeight] {label} 组件: {list} size={rt.sizeDelta} rect={rt.rect} child={rt.childCount}");
        }
    }
}
