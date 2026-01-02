using System;
using System.IO;
using System.Linq;
using COM3D2.i18nEx.Core.Util;
using I2.Loc;
using UnityEngine;

namespace COM3D2.i18nEx.Core.TranslationManagers
{
    internal class I2TranslationManager : TranslationManagerBase
    {
        private readonly GameObject go = new();

        private void Update()
        {
            if (Configuration.I2Translation.ReloadTranslationsKey.Value.IsPressed)
                ReloadActiveTranslations();
        }

        public override void LoadLanguage()
        {
            DontDestroyOnLoad(go);

            Core.Logger.LogInfo("Loading UI translations");
            LoadTranslations();
        }

        private void LoadTranslations()
        {
            var csvFiles = Core.TranslationLoader.GetUITranslationFileNames();

            var source = go.GetComponent<LanguageSource>() ?? go.AddComponent<LanguageSource>();
            source.name = "i18nEx";
            source.ClearAllData();

            if (csvFiles == null)
            {
                Core.Logger.LogInfo("No UI translations found! Skipping...");
                return;
            }

            foreach (var csvFilePath in csvFiles.OrderByDescending(k => k, StringComparer.InvariantCultureIgnoreCase))
            {
                if (Configuration.I2Translation.VerboseLogging.Value)
                    Core.Logger.LogInfo($"Loading unit {csvFilePath}");

                //Fixes subfoldered files being loaded improperly.
                var categoryName = Path.GetFileNameWithoutExtension(csvFilePath);//tlFile.Replace("\\", "/").Splice(0, -5);

				if (Configuration.I2Translation.VerboseLogging.Value)
                    Core.Logger.LogInfo($"Loading category {categoryName}");

                string csvFile;
                using (var f =
                    new StreamReader(
                                     Core.TranslationLoader
                                         .OpenUiTranslation(csvFilePath)))
                {
                    csvFile = f.ReadToEnd().ToLF();
                }

                source.Import_CSV(categoryName, csvFile, eSpreadsheetUpdateMode.AddNewTerms);
            }

            Core.Logger.LogInfo(
                                $"Loaded the following languages: {string.Join(",", source.mLanguages.Select(d => d.Name).ToArray())}");

            LocalizationManager.LocalizeAll(true);
        }

        public override void ReloadActiveTranslations()
        {
            Core.Logger.LogInfo("Reloading current I2 translations");
            LoadTranslations();
        }
    }
}
