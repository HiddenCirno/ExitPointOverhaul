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
        /// <summary>双击 O 撤离点列表解除高度限制（容器自适应内容，条目完整显示）。</summary>
        public static ConfigEntry<bool> EnableUnlimitHeight;

        private static GameObject _host;

        private void Awake()
        {
            LogSource = Logger;

            // 先加载多语言词典并绑定"配置菜单语言"项，后续配置的 DispName/Description 按所选语言显示
            CfgLocaleManager.Initialize(Config);

            EnableBuffer = Config.Bind("Exfil", "EnableBuffer", true,
                new ConfigDescription(
                    CfgLocaleManager.Get("cfg_enable_buffer_desc"),
                    null,
                    new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_enable_buffer_name") }));
            BufferSeconds = Config.Bind("Exfil", "BufferSeconds", 3f,
                new ConfigDescription(
                    CfgLocaleManager.Get("cfg_buffer_seconds_desc"),
                    null,
                    new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_buffer_seconds_name") }));
            EnableAllExits = Config.Bind("Exfil", "EnableAllExits", true,
                new ConfigDescription(
                    CfgLocaleManager.Get("cfg_enable_all_exits_desc"),
                    null,
                    new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_enable_all_exits_name") }));
            EnableFlareHint = Config.Bind("Exfil", "EnableFlareHint", true,
                new ConfigDescription(
                    CfgLocaleManager.Get("cfg_enable_flare_hint_desc"),
                    null,
                    new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_enable_flare_hint_name") }));
            EnableUnlimitHeight = Config.Bind("Exfil", "EnableUnlimitHeight", true,
                new ConfigDescription(
                    CfgLocaleManager.Get("cfg_enable_unlimit_height_desc"),
                    null,
                    new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_enable_unlimit_height_name") }));

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
