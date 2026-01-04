using System.IO;

namespace COM3D2.i18nEx.Core
{
    internal static class Paths
    {
        public static string TranslationsRoot { get; private set; }
        public static string ConfigurationFilePath { get; private set; }

        public static void Initialize(string gameRoot)
        {
            Core.Logger.LogInfo("Initializing paths...");

            TranslationsRoot = Path.Combine(Path.Combine(gameRoot, "BepInEx"), "i18nEx");
            ConfigurationFilePath = Path.Combine(TranslationsRoot, "configuration.ini");

            if (!Directory.Exists(TranslationsRoot))
            {
                Core.Logger.LogInfo($"No root path found. Creating one in {TranslationsRoot}");
                Directory.CreateDirectory(TranslationsRoot);
            }

            // Check for old path and warn user
            var oldPath = Path.Combine(gameRoot, "i18nEx");
            if (Directory.Exists(oldPath))
            {
                Core.Logger.LogWarning("======================================");
                Core.Logger.LogWarning($"[IMPORTANT] Old translation directory detected: {oldPath}");
                Core.Logger.LogWarning($"Please manually move your translation files to: {TranslationsRoot}");
                Core.Logger.LogWarning("The plugin will NOT automatically move files.");
                Core.Logger.LogWarning("======================================");
            }
        }
    }
}
