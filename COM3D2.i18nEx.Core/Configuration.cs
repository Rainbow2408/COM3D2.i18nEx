﻿using System.IO;
using ExIni;

namespace COM3D2.i18nEx.Core
{
    internal static class Configuration
    {
        private static readonly IniFile configFile;

        static Configuration()
        {
            configFile = File.Exists(Paths.ConfigurationFilePath) ? IniFile.FromFile(Paths.ConfigurationFilePath) : new IniFile();
        }

        public static void Reload()
        {
            configFile.Merge(IniFile.FromFile(Paths.TranslationsRoot));
        }

        private static ConfigWrapper<T> Wrap<T>(string section, string key, string description = "", T @default = default)
        {
            return new ConfigWrapper<T>(configFile, Paths.ConfigurationFilePath, section, key, description, @default);
        }

        public static ConfigWrapper<int> MaxTranslationFilesCached = Wrap(
            "TextTranslations", 
            "CacheSize",
            "Specifies how many text translation files should be kept in memory at once\nHaving bigger cache can improve performance at the cost of memory usage",
            1);

        public static ConfigWrapper<string> ActiveLanguage = Wrap(
            "General",
            "ActiveLanguage",
            "Currently selected language",
            "English"
            );
    }

    internal static class Paths
    {
        public static string TranslationsRoot { get; private set; }
        public static string ConfigurationFilePath { get; private set; }

        public static void Initialize(string gameRoot)
        {
            Core.Logger.LogInfo("Initializing paths...");

            TranslationsRoot = Path.Combine(gameRoot, "i18nEx");
            ConfigurationFilePath = Path.Combine(TranslationsRoot, "configuration.ini");

            if (!Directory.Exists(TranslationsRoot))
            {
                Core.Logger.LogInfo($"No root path found. Creating one in {TranslationsRoot}");
                Directory.CreateDirectory(TranslationsRoot);
            }
        }
    }
}
