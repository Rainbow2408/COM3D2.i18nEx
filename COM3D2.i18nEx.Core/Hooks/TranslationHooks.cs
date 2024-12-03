using System;
using System.Collections.Generic;
using System.IO;
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
        private static Dictionary<string, string> OfficialLanguageDict = null;

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
                parent && parent.name == "YotogiPlayPanel")
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
            if (overrideLanguage != "Japanese" &&
                (__result.IsNullOrWhiteSpace() || __result.IndexOf('/') >= 0 && Term.Contains(__result)))
                __result = LocalizationManager.GetTranslation(Term, FixForRTL, maxLineLengthForRTL, ignoreRTLnumbers,
                                                              applyParameters, localParametersRoot, "Japanese");
            else if (Configuration.I2Translation.VerboseLogging.Value)
                Core.Logger.LogInfo($"[I2Loc] Translating term \"{Term}\" => \"{__result}\"");
        }

        [HarmonyPatch(typeof(ConfigMgr), nameof(ConfigMgr.Update))]
        [HarmonyPrefix]
        private static bool OnConfigMgrUpdate()
        {
            return false;
        }

        [HarmonyPatch(typeof(ConfigManager), nameof(ConfigManager.LoadConfig))]
        [HarmonyPostfix]
        private static void ConfigManagerPostLoad()
        {
            LoadPopupSelect();
            ReloadPopupList();
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

            EventDelegate.Add(pop.onChange, new EventDelegate.Callback(UILabel_Label.SetCurrentSelection));
            EventDelegate.Add(pop.onChange, delegate ()
            {
                SetCurrentLanguage(Traverse.Create(configManager_).Method("GetCurrentPopupListValue").GetValue<string>());
                LoadPopupSelect();
                ReloadPopupList();
            });

            UIsystemLanguage.enabled = false;
        }

        private static string OfficialLanguageConvert(string Term)
        {
            if (string.IsNullOrEmpty(Term) && !(Term == "-"))
                return Term;

            string translatedTerm = LocalizationManager.GetTranslation(Term, false, 0, true, false, null, null);

            if (!string.IsNullOrEmpty(translatedTerm))
            {
                string finalTranslation = "[KISS] " + translatedTerm;
                OfficialLanguageDict ??= new();
                OfficialLanguageDict[finalTranslation] = Term;
                return finalTranslation;
            }

            return Term;
        }

        private static bool CheckConfigLanguageName()
        {
            if (string.IsNullOrEmpty(Configuration.General.ActiveLanguage.Value))
                return false;

            string Language = Utility.ReFormatLanguageName(Configuration.General.ActiveLanguage.Value, out _, log: true);
            if (string.IsNullOrEmpty(Language))
            {
                Core.Logger.LogWarning($"Invalid language name: {Configuration.General.ActiveLanguage.Value}");
                Configuration.General.ActiveLanguage.Value = string.Empty;
                return false;
            }

            Configuration.General.ActiveLanguage.Value = Language;
            return true;
        }

        private static void LoadPopupSelect()
        {
            if (pop == null) return;

            if (CheckConfigLanguageName()) pop.value = "i18n/Lang/" + Configuration.General.ActiveLanguage.Value;
            else
            {
                if (pop.value.StartsWith("[KISS] ")) if (OfficialLanguageDict != null && OfficialLanguageDict.TryGetValue(pop.value, out string Term)) pop.value = Term;
                pop.value = OfficialLanguageConvert(pop.value);
                UILabel_Label.SetCurrentSelection();
            }
        }

        private static void ReloadPopupList()
        {
            if (pop == null || UIsystemLanguage == null)
            {
                Core.Logger.LogWarning("Cannot reload popup list: UI components not initialized");
                return;
            }
            try
            {
                pop.Clear();
                foreach (string item in Directory.GetDirectories("BepInEx\\i18nEx", "*", SearchOption.TopDirectoryOnly))
                {
                    if (Utility.CheckLanguageName(Path.GetFileName(item), out string language))
                        pop.AddItem("i18n/Lang/" + language);
                }
                foreach (string item in UIsystemLanguage.items)
                {
                    string lang = OfficialLanguageConvert(item);
                    if (!pop.items.Contains(lang)) pop.AddItem(lang);
                }
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
        private static void SetCurrentLanguage(string langTerm)
        {
            // Logic from Product.Language.systemLanguage setter and ConfigManager.Init->systemLanguage.onChange
            //   Tip: Recommended language name reference I2.Loc.GoogleLanguages.mLanguageDef Dictionary
            //   Example: "Chinese/Traditional" => "Chinese (Traditional)"

            string PostTerm = "i18n/Lang/";
            if (langTerm.StartsWith("[KISS] "))
            {
                if (OfficialLanguageDict != null && OfficialLanguageDict.TryGetValue(langTerm, out string Term))
                    LocalizationManager.CurrentLanguage = Utility.ReFormatLanguageName(Term.Substring("System/言語/".Length), out _, log: true);
                else
                    LocalizationManager.CurrentLanguage = Product.EnumConvert.ToI2LocalizeLanguageName(Product.Language.Japanese);
                Configuration.General.ActiveLanguage.Value = string.Empty;
            }
            else if (langTerm.StartsWith(PostTerm))
            {
                string language = Utility.ReFormatLanguageName(langTerm.Substring(PostTerm.Length), out _, log: true);
                LocalizationManager.CurrentLanguage = language;
                Configuration.General.ActiveLanguage.Value = language;
            }

            // Reload all languages
            foreach (LanguageSource languageSource in LocalizationManager.Sources) languageSource.LoadAllLanguages(false);
        }
    }
}