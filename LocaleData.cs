using Newtonsoft.Json;
using System.Collections.Generic;

namespace ExfilImprovements
{
    /// <summary>
    /// 语言文件（locales/*.json）反序列化数据类，与 ImmersiveRaidTime 一致：
    /// { "Language": "English", "Translate": { "key": "text", ... } }
    /// </summary>
    public class LocaleData
    {
        [JsonProperty("Language")]
        public string Language { get; set; }

        [JsonProperty("Translate")]
        public Dictionary<string, string> Translate { get; set; }
    }
}
