using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using COM3D2.i18nEx.Core.Hooks;
using COM3D2.i18nEx.Core.Loaders;
using COM3D2.i18nEx.Core.TranslationManagers;
using COM3D2.i18nEx.Core.Util;
using ExIni;
using I2.Loc;
using UnityEngine;

namespace COM3D2.i18nEx.Core
{
    public class Core : MonoBehaviour
    {
#if COM3D25
        private const int MIN_SUPPORTED_VERSION = 34100;
#else
        private const int MIN_SUPPORTED_VERSION = 23400;
#endif
        internal static ScriptTranslationManager ScriptTranslate;
        internal static TextureReplaceManager TextureReplace;
        internal static I2TranslationManager I2Translation;
        private readonly List<TranslationManagerBase> managers = new();

        public static ILogger Logger { get; private set; }

        internal static BepInEx.BaseUnityPlugin pluginInstance;

        public bool Initialized { get; private set; }

        internal static ITranslationLoader TranslationLoader { get; private set; }

        private int GameVersion => (int)typeof(Misc).GetField(nameof(Misc.GAME_VERSION)).GetValue(null);

        internal static string CurrentSelectedLanguage { get; private set; }

        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

        private void Update()
        {
            // Check reload config key
            if (Configuration.ReloadConfigKey?.Value.IsDown() == true)
            {
                Logger.LogInfo("Reloading configuration...");
                pluginInstance?.Config.Reload();
            }

            // Check global reload translations key
            if (Configuration.GeneralReloadTranslationsKey?.Value.IsDown() == true)
            {
                Logger.LogInfo("Reloading current translation...");
                foreach (var mgr in managers)
                    mgr.ReloadActiveTranslations();
            }

            if (Configuration.DebugPrintLanguageSourcesKey?.Value.IsDown() == true)
                foreach (var languageSource in LocalizationManager.Sources)
                    Logger.LogInfo($"Got source {languageSource}");
        }

        public void Initialize(ILogger logger, string gameRoot, BepInEx.BaseUnityPlugin pluginInstance = null)
        {
            if (GameVersion < MIN_SUPPORTED_VERSION)
            {
                logger.LogWarning(
                                  $"This version of i18nEx core supports only game versions {MIN_SUPPORTED_VERSION} or newer. Detected game version: {GameVersion}");
                Destroy(this);
                return;
            }

            if (Initialized)
                return;

            Logger = logger;
            Logger.LogInfo("Initializing i18nEx...");

            Paths.Initialize(gameRoot);
            
            // Store plugin reference for config reload
            Core.pluginInstance = pluginInstance;
            
            // Initialize core configuration FIRST (always works, no ConfigurationManager dependency)
            if (pluginInstance != null)
            {
                Configuration.Initialize();
                
                // Then try ConfigurationManager GUI integration (optional)
                try
                {
                    ConfigurationManagerIntegration.Initialize();
                }
                catch (Exception ex)
                {
                    Logger.LogInfo($"ConfigurationManager GUI not available (optional): {ex.Message}");
                }
            }
            
            TranslationHooks.Initialize();
            InitializeTranslationManagers();

            Logger.LogInfo("i18nEx initialized!");
            Initialized = true;
        }

        private T RegisterTranslationManager<T>() where T : TranslationManagerBase
        {
            var res = gameObject.AddComponent<T>();
            managers.Add(res);
            return res;
        }

        private void InitializeTranslationManagers()
        {
            ScriptTranslate = RegisterTranslationManager<ScriptTranslationManager>();
            TextureReplace = RegisterTranslationManager<TextureReplaceManager>();
            I2Translation = RegisterTranslationManager<I2TranslationManager>();

            // Load initial language
            var activeLanguage = Configuration.ActiveLanguage?.Value ?? "English";
            LoadLanguage(activeLanguage);
            
            // Subscribe to language change events
            if (Configuration.ActiveLanguage != null)
            {
                Configuration.ActiveLanguage.SettingChanged += (sender, args) =>
                {
                    LoadLanguage(Configuration.ActiveLanguage.Value);
                };
            }
            
            // Subscribe to DumpTexts change events
            if (Configuration.DumpTexts != null)
            {
                Configuration.DumpTexts.SettingChanged += (sender, args) =>
                {
                    I2TranslationDump.Feature(Configuration.DumpTexts.Value);
                };
            }
        }

        private void LoadLanguage(string langName)
        {
            var tlLang = Path.Combine(Paths.TranslationsRoot, langName);
            if (!Utility.CheckLanguageName(langName, out _))
            {
                TranslationLoader ??= new BasicTranslationLoader();
                TranslationLoader?.UnloadCurrentTranslation();
                foreach (var mgr in managers)
                    mgr.LoadLanguage();
                I2TranslationDump.Unload();
                CurrentSelectedLanguage = string.Empty;
                return;
            }

            if (!Directory.Exists(tlLang))
            {
                Logger.LogWarning($"No translations for language \"{langName}\" was found! Create Directory.");
                Directory.CreateDirectory(tlLang);
            }

            TranslationLoader?.UnloadCurrentTranslation();

            var iniFile = LoadLanguageConfig(tlLang);

            TranslationLoader =
                iniFile == null ? new BasicTranslationLoader() : GetLoader(iniFile["Info"]["Loader"].Value);

            Logger.LogInfo($"Selecting language for {TranslationLoader}");
            TranslationLoader.SelectLanguage(langName, tlLang, iniFile);

            foreach (var mgr in managers)
                mgr.LoadLanguage();

            CurrentSelectedLanguage = langName;
            I2TranslationDump.Initialize();
        }

        /// <summary>
        /// Loads the per-language configuration file from a translation folder.
        /// Note: This is NOT the plugin configuration - it's the translation pack's config.ini
        /// (e.g., BepInEx/i18nEx/English/config.ini) which can specify custom translation loaders.
        /// Uses ExIni library for INI parsing (separate from BepInEx.Configuration).
        /// </summary>
        /// <param name="tlLang">Path to the translation language folder</param>
        /// <returns>IniFile if config exists, null otherwise</returns>
        private static IniFile LoadLanguageConfig(string tlLang)
        {
            var tlConfig = Path.Combine(tlLang, "config.ini");
            if (!File.Exists(tlConfig))
                return null;
            try
            {
                return IniFile.FromFile(tlConfig);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Failed to read configuration file for current translation: {e.Message}");
            }

            return null;
        }

        private static ITranslationLoader GetLoader(string loaderName)
        {
            return typeof(Core).Assembly.GetTypes()
                               .Where(t => t.IsClass && !t.IsAbstract &&
                                           typeof(ITranslationLoader).IsAssignableFrom(t) &&
                                           t.Name.Equals(loaderName, StringComparison.InvariantCultureIgnoreCase))
                               .Select(t => (ITranslationLoader)Activator.CreateInstance(t)).FirstOrDefault() ??
                   new BasicTranslationLoader();
        }
    }
}
