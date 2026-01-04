using BepInEx.Configuration;
using COM3D2.i18nEx.Core.TranslationManagers;
using UnityEngine;

namespace COM3D2.i18nEx.Core
{
    /// <summary>
    /// 核心配置類別 - 使用 BepInEx.Configuration
    /// </summary>
    public static class Configuration
    {
        private static bool initialized = false;

        #region === General Settings ===
        
        /// <summary>Currently selected language</summary>
        public static ConfigEntry<string> ActiveLanguage { get; private set; }
        
        /// <summary>Maximum number of languages in the selector</summary>
        public static ConfigEntry<int> MaxLanguagesInList { get; private set; }
        
        /// <summary>Key combination to reload configuration</summary>
        public static ConfigEntry<KeyboardShortcut> ReloadConfigKey { get; private set; }
        
        /// <summary>Global key combination to reload all translations</summary>
        public static ConfigEntry<KeyboardShortcut> GeneralReloadTranslationsKey { get; private set; }
        
        /// <summary>Debug key to print all language sources</summary>
        public static ConfigEntry<KeyboardShortcut> DebugPrintLanguageSourcesKey { get; private set; }
        
        /// <summary>Internal: Reset game subtitle type on next run</summary>
        public static ConfigEntry<bool> FixSubtitleType { get; private set; }
        
        /// <summary>Comma-separated list of enabled languages</summary>
        public static ConfigEntry<string> LanguageList { get; private set; }
        
        #endregion

        #region === Script Translations Settings ===
        
        /// <summary>Dump untranslated script lines</summary>
        public static ConfigEntry<bool> DumpScriptTranslations { get; private set; }
        
        /// <summary>Route translations to different textboxes</summary>
        public static ConfigEntry<TranslationsReroute> RerouteTranslationsTo { get; private set; }
        
        /// <summary>Send untranslated story text to clipboard</summary>
        public static ConfigEntry<bool> SendScriptToClipboard { get; private set; }
        
        /// <summary>Time to wait before sending to clipboard</summary>
        public static ConfigEntry<double> ClipboardCaptureTime { get; private set; }
        
        /// <summary>Enable verbose logging for script translations</summary>
        public static ConfigEntry<bool> ScriptVerboseLogging { get; private set; }
        
        /// <summary>Number of translation files cached</summary>
        public static ConfigEntry<int> MaxTranslationFilesCached { get; private set; }
        
        /// <summary>Key to reload script translations</summary>
        public static ConfigEntry<KeyboardShortcut> ScriptReloadTranslationsKey { get; private set; }
        
        #endregion

        #region === Texture Replacement Settings ===
        
        /// <summary>Dump original textures</summary>
        public static ConfigEntry<bool> DumpTextures { get; private set; }
        
        /// <summary>Skip dumping game's own .tex files</summary>
        public static ConfigEntry<bool> SkipDumpingCMTextures { get; private set; }
        
        /// <summary>Enable verbose logging for texture replacement</summary>
        public static ConfigEntry<bool> TextureVerboseLogging { get; private set; }
        
        /// <summary>Number of texture replacements cached</summary>
        public static ConfigEntry<int> MaxTexturesCached { get; private set; }
        
        /// <summary>Key to reload texture replacements</summary>
        public static ConfigEntry<KeyboardShortcut> TextureReloadTranslationsKey { get; private set; }
        
        #endregion

        #region === UI Translation Settings ===
        
        /// <summary>Switch UI to English style</summary>
        public static ConfigEntry<bool> EngUIStyle { get; private set; }
        
        /// <summary>Custom UI font name</summary>
        public static ConfigEntry<string> CustomUIFont { get; private set; }
        
        /// <summary>Dump untranslated UI texts</summary>
        public static ConfigEntry<bool> DumpTexts { get; private set; }
        
        /// <summary>Enable custom subtitle opacity control</summary>
        public static ConfigEntry<bool> OverrideSubtitleOpacity { get; private set; }
        
        /// <summary>Subtitle box opacity (0-1)</summary>
        public static ConfigEntry<float> SubtitleOpacity { get; private set; }
        
        /// <summary>Enable verbose logging for UI translation</summary>
        public static ConfigEntry<bool> UIVerboseLogging { get; private set; }
        
        /// <summary>Key to print font names</summary>
        public static ConfigEntry<KeyboardShortcut> PrintFontNamesKey { get; private set; }
        
        /// <summary>Key to reload UI translations</summary>
        public static ConfigEntry<KeyboardShortcut> UIReloadTranslationsKey { get; private set; }
        
        #endregion

        /// <summary>
        /// 初始化配置系統
        /// </summary>
        public static void Initialize()
        {
            if (initialized)
                return;

            var config = Core.pluginInstance?.Config;
            if (config == null)
            {
                Core.Logger.LogWarning("Plugin config not available for Configuration initialization");
                return;
            }

            BindGeneralSettings(config);
            BindScriptTranslationSettings(config);
            BindTextureReplacementSettings(config);
            BindUITranslationSettings(config);

            initialized = true;
            Core.Logger.LogInfo("Configuration initialized with 25 settings");
        }

        private static void BindGeneralSettings(ConfigFile config)
        {
            ActiveLanguage = config.Bind(
                "General",
                "ActiveLanguage",
                "English",
                "Currently selected language"
            );

            MaxLanguagesInList = config.Bind(
                "General",
                "MaxLanguagesInList",
                6,
                new ConfigDescription(
                    "Maximum number of languages to display in the in-game language selector",
                    new AcceptableValueRange<int>(1, 20))
            );

            ReloadConfigKey = config.Bind(
                "General",
                "ReloadConfigKey",
                new KeyboardShortcut(KeyCode.F12, KeyCode.LeftControl),
                "The key to reload configuration"
            );

            GeneralReloadTranslationsKey = config.Bind(
                "General",
                "ReloadTranslationsKey",
                new KeyboardShortcut(KeyCode.F12, KeyCode.LeftAlt),
                "The key to reload translation"
            );

            DebugPrintLanguageSourcesKey = config.Bind(
                "General",
                "DebugPrintLanguageSourcesKey",
                new KeyboardShortcut(KeyCode.Keypad0),
                "Debug key to print all I2 language sources to console"
            );

            LanguageList = config.Bind(
                "General",
                "LanguageList",
                "",
                "Comma-separated list of enabled languages (managed by Language Manager GUI)"
            );

            FixSubtitleType = config.Bind(
                "General",
                "FixGameSubtitleType",
                true,
                "DO NOT TOUCH: If enabled, i18nEx will reset game subtitle type to Japanese on the next game run"
            );
        }

        private static void BindScriptTranslationSettings(ConfigFile config)
        {
            ClipboardCaptureTime = config.Bind(
                "Script Translations",
                "ClipboardCaptureTime",
                0.25,
                new ConfigDescription(
                    "If `SendScriptToClipboard` is enabled, specifies the time to wait before sending all input to clipboard.",
                    new AcceptableValueRange<double>(0.1, 5.0))
            );

            DumpScriptTranslations = config.Bind(
                "Script Translations",
                "DumpUntranslatedLines",
                false,
                "If enabled, dumps untranslated script lines (along with built-in translations, if present)."
            );

            MaxTranslationFilesCached = config.Bind(
                "Script Translations",
                "CacheSize",
                1,
                new ConfigDescription(
                    "Specifies how many text translation files should be kept in memory at once\n" +
                    "Having bigger cache can improve performance at the cost of memory usage",
                    new AcceptableValueRange<int>(1, 50))
            );

            ScriptReloadTranslationsKey = config.Bind(
                "Script Translations",
                "ReloadTranslationsKey",
                new KeyboardShortcut(KeyCode.Keypad1, KeyCode.LeftAlt),
                "The key (or key combination) to reload all script translations."
            );

            RerouteTranslationsTo = config.Bind(
                "Script Translations",
                "RerouteTranslationsTo",
                TranslationsReroute.RouteToLocal,
                "Route translations to different textboxes:\n" +
                "• None - Disabled (Local in Local box, Japanese in Japanese box)\n" +
                "• RouteToLocal - Show Japanese in Local box if no translation\n" +
                "• RouteToJapanese - Show translation in Japanese box"
            );

            SendScriptToClipboard = config.Bind(
                "Script Translations",
                "SendToClipboard",
                false,
                "Send untranslated story text to clipboard"
            );

            ScriptVerboseLogging = config.Bind(
                "Script Translations",
                "VerboseLogging",
                false,
                "If enabled, logs precise translation info\n" +
                "Useful if you're writing new translations."
            );
        }

        private static void BindTextureReplacementSettings(ConfigFile config)
        {
            DumpTextures = config.Bind(
                "Texture Replacement",
                "DumpOriginalTextures",
                false,
                "If enabled, dumps textures that have no replacements."
            );

            MaxTexturesCached = config.Bind(
                "Texture Replacement",
                "CacheSize",
                10,
                new ConfigDescription(
                    "Specifies how many texture replacements should be kept in memory at once\n" +
                    "Having bigger cache can improve performance at the cost of memory usage",
                    new AcceptableValueRange<int>(1, 50))
            );

            TextureReloadTranslationsKey = config.Bind(
                "Texture Replacement",
                "ReloadTranslationsKey",
                new KeyboardShortcut(KeyCode.Keypad2, KeyCode.LeftAlt),
                "The key (or key combination) to reload all translations."
            );

            SkipDumpingCMTextures = config.Bind(
                "Texture Replacement",
                "SkipDumpingCMTextures",
                true,
                "Skip dumping game's own .tex files\n" +
                "If `DumpOriginalTextures` is enabled, setting this to `True` will disable dumping game's own .tex files\n" +
                "Use this if you don't want to dump all in-game textures."
            );

            TextureVerboseLogging = config.Bind(
                "Texture Replacement",
                "VerboseLogging",
                false,
                "If enabled, logs precise texture replacement info\n" +
                "Useful if you're writing new translations."
            );
        }

        private static void BindUITranslationSettings(ConfigFile config)
        {
            EngUIStyle = config.Bind(
                "UI Translation",
                "EngUIStyle",
                false,
                "Switch UI to English style (disable for Japanese version)"
            );

            // Get installed fonts and add empty string as first option (default/no custom font)
            var installedFonts = Font.GetOSInstalledFontNames();
            var fontOptions = new string[installedFonts.Length + 1];
            fontOptions[0] = ""; // Empty = use default font
            installedFonts.CopyTo(fontOptions, 1);

            CustomUIFont = config.Bind(
                "UI Translation",
                "CustomUIFont",
                "",
                new ConfigDescription(
                    "If specified, replaces the UI font with this one.\n" +
                    "IMPORTANT: The font **must** be installed on your machine and it **must** be a TrueType font.",
                    new AcceptableValueList<string>(fontOptions))
            );

            DumpTexts = config.Bind(
                "UI Translation",
                "DumpUntranslatedUITexts",
                false,
                "If enabled, dumps untranslated UI texts"
            );

            OverrideSubtitleOpacity = config.Bind(
                "UI Translation",
                "OverrideSubtitleOpacity",
                false,
                "If enabled, allows to change subtitle box opacity without affecting other elements."
            );

            PrintFontNamesKey = config.Bind(
                "UI Translation",
                "PrintFontNamesKey",
                new KeyboardShortcut(KeyCode.F11, KeyCode.LeftAlt),
                "The key (or key combination) do display all supported UI fonts in the console."
            );

            UIReloadTranslationsKey = config.Bind(
                "UI Translation",
                "ReloadTranslationsKey",
                new KeyboardShortcut(KeyCode.Keypad3, KeyCode.LeftAlt),
                "The key (or key combination) to reload all translations."
            );

            SubtitleOpacity = config.Bind(
                "UI Translation",
                "SubtitleOpacity",
                1.0f,
                new ConfigDescription(
                    "If OverrideSubtitleOpacity is true, specifies opacity of the subtitle box. Must be a decimal between 0 (transparent) and 1 (opaque).",
                    new AcceptableValueRange<float>(0.0f, 1.0f))
            );

            UIVerboseLogging = config.Bind(
                "UI Translation",
                "VerboseLogging",
                false,
                "If enabled, logs precise I2Loc loading and translation info\n" +
                "Useful if you're debugging."
            );
        }
    }
}
