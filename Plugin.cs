using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ExfilImprovements
{
    [BepInPlugin(PluginsInfo.GUID, PluginsInfo.NAME, PluginsInfo.VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource LogSource;

        /// <summary>撤离点确认缓冲总开关。</summary>
        public static ConfigEntry<bool> EnableBuffer;
        /// <summary>缓冲倒计时时长（秒），倒计时结束后撤离点进入开放状态。</summary>
        public static ConfigEntry<float> BufferSeconds;
        /// <summary>所有撤离点对 PMC/SCAV 全部开放，且不因概率失效。</summary>
        public static ConfigEntry<bool> EnableAllExits;
        /// <summary>信号弹在信号区域内成功起效时显示通知。</summary>
        public static ConfigEntry<bool> EnableFlareHint;

        private static GameObject _host;

        private void Awake()
        {
            LogSource = Logger;

            EnableBuffer = Config.Bind("Exfil", "EnableBuffer", true,
                "为撤离点增加确认缓冲：进入撤离区域后不立即开始撤离计时，按住原版互动键（默认 F）开始缓冲倒计时，松开取消，倒计时结束撤离点开放（参考转移点交互）。");
            BufferSeconds = Config.Bind("Exfil", "BufferSeconds", 3f,
                "缓冲倒计时时长（秒）。按住互动键后倒计时，倒计时结束撤离点进入正常开放状态（开始 ExfiltrationTime 撤离计时）。");
            EnableAllExits = Config.Bind("Exfil", "EnableAllExits", true,
                "所有撤离点对 PMC/SCAV 全部开放（InfiltrationMatch 恒通过）且不因概率失效（RollChance 恒通过）。");
            EnableFlareHint = Config.Bind("Exfil", "EnableFlareHint", true,
                "信号弹在信号区域内被成功接收（类型匹配 + 发射位置在区域内）时，向玩家显示通知提示。");

            _host = new GameObject("ExfilImprovementsHost", typeof(ExfilBufferBehaviour));
            Object.DontDestroyOnLoad(_host);
            _host.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                var harmony = new Harmony(PluginsInfo.GUID);
                harmony.PatchAll();
            }
            catch (System.Exception ex)
            {
                LogSource.LogError($"[{PluginsInfo.NAME}] PatchAll 失败: {ex}");
            }

            LogSource.LogInfo($"[{PluginsInfo.NAME}] 加载完成。EnableBuffer={EnableBuffer.Value}，" +
                $"EnableAllExits={EnableAllExits.Value}，EnableFlareHint={EnableFlareHint.Value}");
        }
    }
}
