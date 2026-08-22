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
        /// <summary>撤离点恒定开放：RollChance 恒通过，撤离点不因概率失效设为 NotPresent。</summary>
        public static ConfigEntry<bool> EnableAlwaysOpen;
        /// <summary>PMC/SCAV 共用所有撤离点：InfiltrationMatch 恒通过，跨势力进入并可用。</summary>
        public static ConfigEntry<bool> EnableCrossFaction;
        /// <summary>信号弹在信号区域内成功起效时显示通知。</summary>
        public static ConfigEntry<bool> EnableFlareHint;

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
            EnableAlwaysOpen = Config.Bind("Exfil", "EnableAlwaysOpen", true,
                new ConfigDescription(
                    CfgLocaleManager.Get("cfg_enable_always_open_desc"),
                    null,
                    new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_enable_always_open_name") }));
            EnableCrossFaction = Config.Bind("Exfil", "EnableCrossFaction", true,
                new ConfigDescription(
                    CfgLocaleManager.Get("cfg_enable_cross_faction_desc"),
                    null,
                    new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_enable_cross_faction_name") }));
            EnableFlareHint = Config.Bind("Exfil", "EnableFlareHint", true,
                new ConfigDescription(
                    CfgLocaleManager.Get("cfg_enable_flare_hint_desc"),
                    null,
                    new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_enable_flare_hint_name") }));

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
                $"EnableAlwaysOpen={EnableAlwaysOpen.Value}，EnableCrossFaction={EnableCrossFaction.Value}，" +
                $"EnableFlareHint={EnableFlareHint.Value}");
        }
    }
}
