namespace ExfilImprovements
{
    /// <summary>
    /// BepInEx.ConfigurationManager 特性类（CM 按类型名 "ConfigurationManagerAttributes"
    /// 反射识别），用于给 F12 配置菜单项提供自定义显示名（DispName）。与 ImmersiveRaidTime 同款。
    /// </summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public string DispName;
        public int? Order;
        public bool? IsAdvanced;
    }
}
