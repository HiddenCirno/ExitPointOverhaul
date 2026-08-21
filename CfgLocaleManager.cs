using BepInEx.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace ExfilImprovements
{
    /// <summary>
    /// 本地化管理器（参考 ImmersiveRaidTime.CfgLocaleManager）：
    /// 从插件 DLL 同目录的 locales/*.json 加载多语言词典，F12 配置菜单可切换显示语言
    /// （当前语言配置项位于 "Language / 语言" 分区）。Get(key) 按当前语言取值，
    /// 缺失时回退到 FallbackLangName，再缺失则原样返回 key（便于排查漏译）。
    /// 增强点：
    ///   - 多个插件共享同一 locales 目录时，同语言多文件采用"合并"而非整体覆盖，
    ///     避免不同插件（如 ImmersiveRaidTime 与 ExfilImprovements）的词典互相踩掉；
    ///   - Get() 对未初始化状态做了空防护。
    /// 部署时把 locales/ExfilImprovements.*.json 一并拷到 BepInEx/plugins/locales/ 即可。
    /// </summary>
    public static class CfgLocaleManager
    {
        public static ConfigEntry<string> CurrentLanguage;

        private static readonly Dictionary<string, Dictionary<string, string>> _loadedTranslations =
            new Dictionary<string, Dictionary<string, string>>();
        private const string FallbackLangName = "English";

        public static void Initialize(ConfigFile config)
        {
            string dirPath = Path.Combine(
                Path.GetDirectoryName(typeof(CfgLocaleManager).Assembly.Location), "locales");

            _loadedTranslations.Clear();
            List<string> availableLanguages = new List<string>();

            if (Directory.Exists(dirPath))
            {
                string[] jsonFiles = Directory.GetFiles(dirPath, "*.json");
                foreach (string file in jsonFiles)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        LocaleData data = JsonConvert.DeserializeObject<LocaleData>(json);

                        if (data != null && !string.IsNullOrEmpty(data.Language) && data.Translate != null)
                        {
                            if (_loadedTranslations.TryGetValue(data.Language, out var existing))
                            {
                                // 同语言多文件合并（共享 locales 目录时不同插件的词典互不覆盖）
                                foreach (KeyValuePair<string, string> kv in data.Translate)
                                {
                                    existing[kv.Key] = kv.Value;
                                }
                            }
                            else
                            {
                                _loadedTranslations[data.Language] = data.Translate;
                                availableLanguages.Add(data.Language);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Plugin.LogSource?.LogWarning($"[{PluginsInfo.NAME}] 语言文件加载失败 ({file}): {e.Message}");
                    }
                }
            }

            if (availableLanguages.Count == 0)
            {
                availableLanguages.Add(FallbackLangName);
                _loadedTranslations[FallbackLangName] = new Dictionary<string, string>();
            }

            // 绑定配置菜单语言（Get 里对未初始化状态有空防护，此处可安全回退到 fallback 词典）
            CurrentLanguage = config.Bind(
                "Language / 语言",
                "Menu Language / 配置菜单语言",
                availableLanguages.Contains(FallbackLangName) ? FallbackLangName : availableLanguages[0],
                new ConfigDescription(
                    CfgLocaleManager.Get("cfg_language_desc"),
                    new AcceptableValueList<string>(availableLanguages.ToArray()),
                    new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_language_name") }
                ));
        }

        /// <summary>按当前语言取文案；缺失回退到 English；再缺失原样返回 key。</summary>
        public static string Get(string key)
        {
            if (CurrentLanguage != null
                && _loadedTranslations.TryGetValue(CurrentLanguage.Value, out var currentDict)
                && currentDict.TryGetValue(key, out var text))
            {
                return text;
            }

            if (_loadedTranslations.TryGetValue(FallbackLangName, out var fallbackDict)
                && fallbackDict.TryGetValue(key, out var fallbackText))
            {
                return fallbackText;
            }

            return key;
        }
    }
}
