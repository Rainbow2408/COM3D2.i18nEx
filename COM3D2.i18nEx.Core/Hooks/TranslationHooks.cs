using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using COM3D2.i18nEx.Core.Scripts;
using COM3D2.i18nEx.Core.Util;
using HarmonyLib;
using I2.Loc;
using UnityEngine;

namespace COM3D2.i18nEx.Core.Hooks
{
    internal static class TranslationHooks
    {
        private static bool initialized;
        private static Harmony instance;
        private static UIPopupList UIsystemLanguage = null;
        private static UILabel UILabel_Label = null;
        private static UIPopupScrollList pop = null;
        private static LanguageSource I2Source = null;
        private static Dictionary<string, string> LanguageDict = null;

        public static void Initialize()
        {
            if (initialized)
                return;

            ScriptTranslationHooks.Initialize();
            TextureReplaceHooks.Initialize();
            UIFixes.Initialize();

            instance = Harmony.CreateAndPatchAll(typeof(TranslationHooks), "horse.coder.i18nex.hooks.base");

            initialized = true;
        }

        [HarmonyPatch(typeof(Product), nameof(Product.systemLanguage), MethodType.Getter)]
        [HarmonyPostfix]
        private static void GetSystemLanguage(ref Product.Language __result)
        {
            if (CheckConfigLanguageName())
            {
                __result = Configuration.I2Translation.EngUIStyle.Value ? Product.Language.English : Product.Language.Japanese;
            }
        }

        [HarmonyPatch(typeof(Product), nameof(Product.systemLanguage), MethodType.Setter)]
        [HarmonyPrefix]
        private static void SetSystemLanguage(ref bool __runOriginal)
        {
            if (CheckConfigLanguageName())
            {
                SetCurrentLanguage("i18n/Lang/" + Configuration.General.ActiveLanguage.Value);
                __runOriginal = false;
            }
        }

        [HarmonyPatch(typeof(Product), nameof(Product.subTitleScenarioLanguage), MethodType.Getter)]
        [HarmonyPostfix]
        private static void SubTitleScenarioLanguage(ref Product.Language __result)
        {
            __result = Product.Language.English;
        }

        [HarmonyPatch(typeof(Product), nameof(Product.IsSupportLanguage))]
        [HarmonyPrefix]
        private static bool OnIsSupportLanguage(ref bool __result)
        {
            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(Product), nameof(Product.supportSubtitles), MethodType.Getter)]
        [HarmonyPostfix]
        private static void SupportSubtitle(ref bool __result)
        {
            __result = true;
        }

        [HarmonyPatch(typeof(Product), nameof(Product.supportMultiLanguage), MethodType.Getter)]
        [HarmonyPostfix]
        private static void SupportMultiLanguage(ref bool __result)
        {
            __result = true;
        }

        [HarmonyPatch(typeof(Product), nameof(Product.isJapan), MethodType.Getter)]
        [HarmonyPostfix]
        private static void IsJapan(ref bool __result)
        {
            __result = false;
        }

        [HarmonyPatch(typeof(SceneNetorareCheck), "Start")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> FixNTRCheckScene(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var ins in instructions)
                if (ins.opcode == OpCodes.Call && ins.operand is MethodInfo minfo && minfo.Name == "get_isJapan")
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                else
                    yield return ins;
        }

        [HarmonyPatch(typeof(SubtitleDisplayManager), nameof(SubtitleDisplayManager.messageBgAlpha), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool OnGetConfigMessageAlpha(SubtitleDisplayManager __instance, ref float value)
        {
            var parent = __instance.transform.parent;
            if (Configuration.I2Translation.OverrideSubtitleOpacity.Value &&
                parent                                                    && parent.name == "YotogiPlayPanel")
            {
                if (Math.Abs(value - __instance.messageBgAlpha) < 0.001)
                    return false;
                value = Mathf.Clamp(Configuration.I2Translation.SubtitleOpacity.Value, 0f, 1f);
            }

            return true;
        }

        [HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.GetTranslation))]
        [HarmonyPostfix]
        private static void OnGetTranslation(ref string __result,
                                             string Term,
                                             bool FixForRTL,
                                             int maxLineLengthForRTL,
                                             bool ignoreRTLnumbers,
                                             bool applyParameters,
                                             GameObject localParametersRoot,
                                             string overrideLanguage)
        {
            if (__result.IsNullOrWhiteSpace() || __result.IndexOf('/') >= 0 && Term.Contains(__result))
            {
                if (overrideLanguage != "Japanese")
                    __result = LocalizationManager.GetTranslation(Term, FixForRTL, maxLineLengthForRTL, ignoreRTLnumbers,
                                                                  applyParameters, localParametersRoot, "Japanese");
                else if (overrideLanguage == "Japanese")
                {
                    I2Source ??= LocalizationManager.Sources
                                .Skip(1)
                                .FirstOrDefault(source => source.name == "I2Languages");
                    if (I2Source != null && I2Source.TryGetTranslation(Term, out __result, overrideLanguage))
                    {
                        if (applyParameters)
                            LocalizationManager.ApplyLocalizationParams(ref __result, localParametersRoot);
                        if (LocalizationManager.IsRight2Left && FixForRTL)
                            __result = LocalizationManager.ApplyRTLfix(__result, maxLineLengthForRTL, ignoreRTLnumbers);
                    }
                }
            }
            else if (Configuration.I2Translation.VerboseLogging.Value)
                Core.Logger.LogInfo($"[I2Loc] Translating term \"{Term}\" => \"{__result}\"");
        }

        [HarmonyPatch(typeof(ConfigMgr), nameof(ConfigMgr.Update))]
        [HarmonyPrefix]
        private static bool OnConfigMgrUpdate()
        {
            return false;
        }

        [HarmonyPatch(typeof(ConfigMgr), nameof(ConfigMgr.OpenConfigPanel))]
        [HarmonyPostfix]
        private static void OpenConfigPanelPostLoad()
        {
            DisablePopupListLocalize();
        }

        private static void DisablePopupListLocalize()
        {
            if (UILabel_Label == null)
                return;

            var locs = UILabel_Label.GetComponent<NGUILabelLocalizeSupport>();
            var loc = UILabel_Label.GetComponent<Localize>();

            // Process if either component exists
            if (locs != null)
                UnityEngine.Object.Destroy(locs);
            if (loc != null)
                UnityEngine.Object.Destroy(loc);
            if (pop != null && pop.onChange != null)
                EventDelegate.Remove(pop.onChange, UILabel_Label.SetCurrentSelection);
        }

        [HarmonyPatch(typeof(ConfigManager), nameof(ConfigManager.LoadConfig))]
        [HarmonyPostfix]
        private static void ConfigManagerPostLoad()
        {
            ReloadPopupList(true);
        }

        [HarmonyPatch(typeof(ConfigManager), nameof(ConfigManager.Init))]
        [HarmonyPostfix]
        private static void ConfigManagerPostInit()
        {
            ConfigManager configManager_ = Traverse.Create(BaseMgr<ConfigMgr>.Instance).Field("configMgr").GetValue<ConfigManager>();
            UIsystemLanguage = Traverse.Create(configManager_).Field("systemLanguage").GetValue<UIPopupList>();
            GameObject PopupList = UIsystemLanguage.transform.gameObject;

            GameObject Label = null;
            for (int i = 0; i < PopupList.transform.childCount; i++)
            {
                if (PopupList.transform.GetChild(i).name == "Label")
                {
                    Label = PopupList.transform.GetChild(i).gameObject;
                    break;
                }
            }
            UILabel_Label = Label.GetComponent<UILabel>();

            pop = PopupList.AddComponent<UIPopupScrollList>();
            pop.trueTypeFont = UIsystemLanguage.trueTypeFont;
            pop.isLocalized = UIsystemLanguage.isLocalized;
            pop.value = UIsystemLanguage.value;
            pop.alignment = UIsystemLanguage.alignment;
            pop.fontSize = UIsystemLanguage.fontSize;
            pop.atlas = UIsystemLanguage.atlas;
            pop.textColor = UIsystemLanguage.textColor;
            pop.highlightColor = UIsystemLanguage.highlightColor;
            pop.backgroundSprite = UIsystemLanguage.backgroundSprite;
            pop.highlightSprite = UIsystemLanguage.highlightSprite;
            pop.position = UIsystemLanguage.position;

            ReloadPopupList();

            EventDelegate.Add(pop.onChange, delegate ()
            {
                // If user selected a new value, update language
                if (pop.isActiveAndEnabled && pop.isOpen)
                {
                    // Get the selected language
                    string language = Traverse.Create(configManager_).Method("GetCurrentPopupListValue").GetValue<string>();

                    // Remove Prefix
                    language = NoLocalize.RemoveNoLocalizePrefix(language);

                    // Set the selected language
                    SetCurrentLanguage(language);

                    // Reload popup list
                    ReloadPopupList();
                }
                else ReloadPopupList(true);
            });

            UIsystemLanguage.enabled = false;
        }

        private static string LanguageConvert(string Term, bool Official = false)
        {
            // Check input
            if (string.IsNullOrEmpty(Term) && !(Term == "-"))
                return Term;

            // Ensure dictionary is initialized
            LanguageDict ??= new();

            // Get translation
            string translatedTerm = LocalizationManager.GetTranslation(Term, false);
            bool hasTranslation = !string.IsNullOrEmpty(translatedTerm);

            // If it's an official language, add marker
            if (Official && hasTranslation)
                translatedTerm = "[KISS] " + translatedTerm;

            // Store mapping of original term and translation
            string resultTerm = hasTranslation ? translatedTerm : Term;
            LanguageDict[resultTerm] = Term;

            return resultTerm;
        }

        private static bool CheckConfigLanguageName()
        {
            // Check if there is a configured language
            if (string.IsNullOrEmpty(Configuration.General.ActiveLanguage.Value))
                return false;

            // Format language name
            string language = Utility.ReFormatLanguageName(Configuration.General.ActiveLanguage.Value, out string message, log: true);
            if (string.IsNullOrEmpty(language))
            {
                Core.Logger.LogWarning($"Invalid language name: {Configuration.General.ActiveLanguage.Value}.{message}");
                Configuration.General.ActiveLanguage.Value = string.Empty;
                return false;
            }
            // If the formatted language is different from the configuration value, update configuration
            if (language != Configuration.General.ActiveLanguage.Value)
                Configuration.General.ActiveLanguage.Value = language;
            
            return true;
        }

        // Helper method: Get all available languages
        private static IEnumerable<string> GetAvailableLanguages()
        {
            // Check if directory exists
            string i18nExPath = Path.Combine("BepInEx", "i18nEx");
            if (!Directory.Exists(i18nExPath))
            {
                Directory.CreateDirectory(i18nExPath);

                // Return all official languages
                return UIsystemLanguage.items.Select(item => LanguageConvert(item, true));
            }

            // Get all custom languages
            var customLanguages = Directory.GetDirectories(i18nExPath, "*", SearchOption.TopDirectoryOnly)
                .Select(item =>
                {
                    string langName = Path.GetFileName(item);
                    return Utility.CheckLanguageName(langName, out string f_lang) ?
                           LanguageConvert("i18n/Lang/" + f_lang) :
                           null;
                })
                .Where(language => language != null);

            // Get all official languages
            var officialLanguages = UIsystemLanguage.items.Select(item => LanguageConvert(item, true));

            // Merge and return all languages
            return customLanguages.Concat(officialLanguages);
        }

        private static void ReloadPopupList(bool LoadSelectOnly = false, bool ForceLoadI2Name = false)
        {
            if (pop == null || UIsystemLanguage == null)
            {
                Core.Logger.LogWarning("Cannot reload popup list: UI components not initialized");
                return;
            }
            try
            {
                // Determine the current value to display
                string val = string.Empty;

                // If the translation for the current value is in the dictionary
                if (LanguageDict != null && LanguageDict.TryGetValue(pop.value, out string Term))
                {
                    if (CheckConfigLanguageName())
                    {
                        // Use configured language
                        val = LanguageConvert("i18n/Lang/" + Configuration.General.ActiveLanguage.Value);
                    }
                    else
                    {
                        // Use official language
                        val = LanguageConvert(Term, true);
                    }
                }
                // If current value is empty or force loading is required
                else if (pop.value.IsNullOrWhiteSpace() || ForceLoadI2Name)
                {
                    val = LanguageConvert(UIsystemLanguage.value, true);
                }

                // Only update when the value actually needs to change
                if (!string.IsNullOrEmpty(val) && val != pop.value)
                {
                    if (Configuration.I2Translation.VerboseLogging.Value)
                    {
                        Core.Logger.LogInfo($"Setting popup value from '{pop.value}' to '{val}'");
                    }
                    pop.value = val;
                }

                // If only loading selection is needed, don't update list content
                if (!LoadSelectOnly)
                {
                    // Clear and repopulate the language list
                    pop.Clear();

                    // Get all custom and official languages
                    var allLanguages = GetAvailableLanguages();

                    // Populate the dropdown list
                    foreach (string language in allLanguages)
                    {
                        if (!string.IsNullOrEmpty(language) && !pop.items.Contains(language))
                        {
                            pop.AddItem(language);
                        }
                    }
                }

                // Update display
                UILabel_Label.text = NoLocalize.MarkAsNoLocalize(val);
            }
            catch (UnauthorizedAccessException ex)
            {
                Core.Logger.LogError($"Access denied when reading language directories: {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                Core.Logger.LogError($"Language directory not found: {ex.Message}");
            }
            catch (Exception ex)
            {
                Core.Logger.LogError($"Error reloading popup list: {ex}");
            }
        }
        private static void SetCurrentLanguage(string language)
        {
            // Logic from Product.Language.systemLanguage setter and ConfigManager.Init->systemLanguage.onChange
            //   Tip: Recommended language name reference I2.Loc.GoogleLanguages.mLanguageDef Dictionary
            //   Example: "Chinese/Traditional" => "Chinese (Traditional)"

            if (string.IsNullOrEmpty(language))
            {
                // If language is empty, use default Japanese
                LocalizationManager.CurrentLanguage = Product.EnumConvert.ToI2LocalizeLanguageName(Product.Language.Japanese);
                Configuration.General.ActiveLanguage.Value = string.Empty;
                Core.Logger.LogInfo("Using default Japanese language");
                return;
            }

            bool isOfficialLanguage = language.StartsWith("[KISS] ");

            // Check if the value exists in the language dictionary
            if (LanguageDict != null && LanguageDict.TryGetValue(language, out string termValue))
            {
                // Extract actual language name
                string prefix = isOfficialLanguage ? "System/言語/" : "i18n/Lang/";
                if (termValue.StartsWith(prefix) && termValue.Length > prefix.Length)
                {
                    string languageName = termValue.Substring(prefix.Length);

                    // Format language name
                    string formattedLanguage = Utility.ReFormatLanguageName(languageName, out _, log: true);

                    if (!string.IsNullOrEmpty(formattedLanguage))
                    {
                        // Set current language
                        LocalizationManager.CurrentLanguage = formattedLanguage;

                        // If it's an official language, don't save to configuration
                        Configuration.General.ActiveLanguage.Value = isOfficialLanguage ? string.Empty : formattedLanguage;

                        if (Configuration.I2Translation.VerboseLogging.Value)
                            Core.Logger.LogInfo($"Setting language to: {formattedLanguage} (from {language})");

                        // Reload all languages
                        ReloadAllLanguages();
                        return;
                    }
                }
            }

            // If unable to process the selected language, use Japanese
            Core.Logger.LogWarning($"Could not process selected language: {language}. Using Japanese instead.");
            LocalizationManager.CurrentLanguage = Product.EnumConvert.ToI2LocalizeLanguageName(Product.Language.Japanese);
            Configuration.General.ActiveLanguage.Value = string.Empty;

            // Reload all languages
            ReloadAllLanguages();
        }

        // Helper method: Reload all languages
        private static void ReloadAllLanguages()
        {
            foreach (LanguageSource languageSource in LocalizationManager.Sources)
            {
                languageSource.LoadAllLanguages(false);
            }
        }
    }
}