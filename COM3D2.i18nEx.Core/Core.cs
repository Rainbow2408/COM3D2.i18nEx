using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

        private ITranslationLoader GetLoader(string loaderName)
        {
            if (string.IsNullOrEmpty(loaderName))
                return new BasicTranslationLoader();

            var loadersPath = Path.Combine(Paths.TranslationsRoot, "loaders");
            if (!Directory.Exists(loadersPath))
                Directory.CreateDirectory(loadersPath);

            loaderName = loaderName.Trim();

            if (loaderName == "BasicLoader")
                return new BasicTranslationLoader();

            var loaderPath = Path.Combine(loadersPath, $"{loaderName}.dll");
            if (!File.Exists(loaderPath))
                return new BasicTranslationLoader();

            try
            {
                var ass = Assembly.LoadFile(loaderPath);
                var loader = ass.GetTypes().FirstOrDefault(t => t.GetInterface(nameof(ITranslationLoader)) != null);

                Logger.LogInfo($"Invoking loader {loader}");

                if (loader != null)
                    return Activator.CreateInstance(loader) as ITranslationLoader;

                Logger.LogWarning(
                                  $"Loader \"{loaderName}.dll\" doesn't contain any translation loader implementations!");
                return new BasicTranslationLoader();
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Failed to load translation loader \"{loaderName}.dll\". Reason: {e.Message}");
                return new BasicTranslationLoader();
            }
        }

        /// <summary>
        /// Loads the per-language configuration file from a translation folder.
        /// Note: This is NOT the plugin configuration - it's the translation pack's config.ini
        /// (e.g., BepInEx/i18nEx/English/config.ini) which can specify custom translation loaders.
        /// Uses ExIni library for INI parsing (separate from BepInEx.Configuration).
        /// </summary>
        /// <param name="tlLang">Path to the translation language folder</param>
        /// <returns>IniFile if config exists, null otherwise</returns>
        private IniFile LoadLanguageConfig(string tlPath)
        {
            var iniFile = Path.Combine(tlPath, "config.ini");
            if (!File.Exists(iniFile))
                return null;
            try
            {
                return IniFile.FromFile(iniFile);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Failed to read config.ini. Reason: {e.Message}");
            }
            return null;
        }
    }
}
