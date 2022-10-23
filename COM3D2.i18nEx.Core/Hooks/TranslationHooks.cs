using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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

        [HarmonyPatch(typeof(Product), nameof(Product.subTitleScenarioLanguage), MethodType.Getter)]
        [HarmonyPostfix]
        private static void SubTitleScenarioLanguage(ref Product.Language __result)
        {
            __result = Product.systemLanguage == Product.Language.Japanese ? Product.Language.English : Product.systemLanguage;
        }

        [HarmonyPatch(typeof(Product), nameof(Product.IsSupportLanguage))]
        [HarmonyPrefix]
        private static bool OnIsSupportLanguage(ref bool __result, ref Product.Language language)
        {
            // TODO: Might need smarter logic if/when game supports multiple TL
            List<string> langList = new List<string>();
            foreach (var languageSource in LocalizationManager.Sources)
                foreach (var languageS in languageSource.mLanguages)
                    if (!langList.Contains(languageS.Name)) langList.Add(languageS.Name);
            if (langList.Contains(GetI2Dict[language])) __result = true;
            else __result = false;
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

        [HarmonyPatch(typeof(Product.EnumConvert), nameof(Product.EnumConvert.GetString))]
        [HarmonyPostfix]
        private static void GetString(ref string __result, ref Product.Language language)
        {
            if (GetDict[language] == null) __result = "日本語";
            else __result = GetDict[language];
        }

        static Dictionary<Product.Language, string> GetDict = new Dictionary<Product.Language, string>()
        {
            {Product.Language.Japanese,"日本語"},
            {Product.Language.English,"英語"},
            {Product.Language.SimplifiedChinese, "中国語 (簡体字)"},
            {Product.Language.TraditionalChinese, "中国語 (繁体字)"},
            {Product.Language.Afrikaans, "アフリカーンス語"},
            {Product.Language.Arabic, "アラビア語"},
            {Product.Language.Basque, "バスク語"},
            {Product.Language.Belarusian, "ベラルーシ語"},
            {Product.Language.Bulgarian, "ブルガリア語"},
            {Product.Language.Catalan, "カタロニア語"},
            {Product.Language.Czech, "チェコ語"},
            {Product.Language.Danish, "デンマーク語"},
            {Product.Language.Dutch, "オランダ語"},
            {Product.Language.Estonian, "エストニア語"},
            {Product.Language.Faroese, "フェロー語"},
            {Product.Language.Finnish, "フィンランド語"},
            {Product.Language.French, "フランス語"},
            {Product.Language.German, "ドイツ語"},
            {Product.Language.Greek, "ギリシャ語"},
            {Product.Language.Hebrew, "ヘブライ語"},
            {Product.Language.Icelandic, "アイスランド語"},
            {Product.Language.Indonesian, "インドネシア語"},
            {Product.Language.Italian, "イタリア語"},
            {Product.Language.Korean, "韓国語"},
            {Product.Language.Latvian, "ラトビア語"},
            {Product.Language.Lithuanian, "リトアニア語"},
            {Product.Language.Norwegian, "ノルウェー語"},
            {Product.Language.Polish, "ポーランド語"},
            {Product.Language.Portuguese, "ポルトガル語"},
            {Product.Language.Romanian,"ルーマニア語"},
            {Product.Language.Russian,"ロシア語"},
            {Product.Language.SerboCroatian, "セルビアクロアチア語"},
            {Product.Language.Slovak,"スロバキア語"},
            {Product.Language.Slovenian,"スロベニア語"},
            {Product.Language.Spanish,"スペイン語"},
            {Product.Language.Swedish,"スウェーデン語"},
            {Product.Language.Thai,"タイ語"},
            {Product.Language.Turkish,"トルコ語"},
            {Product.Language.Ukrainian,"ウクライナ語"},
            {Product.Language.Vietnamese,"ベトナム語"},
            {Product.Language.Hungarian,"ハンガリー語"}
        };

        [HarmonyPatch(typeof(Product.EnumConvert), nameof(Product.EnumConvert.ToI2LocalizeLanguageName))]
        [HarmonyPostfix]
        private static void ToI2LocalizeLanguageName(ref string __result, ref Product.Language language)
        {
            if (GetI2Dict[language] == null) __result = "Japanese";
            else __result = GetI2Dict[language];
        }

        static Dictionary<Product.Language, string> GetI2Dict = new Dictionary<Product.Language, string>()
        {
            {Product.Language.Japanese,"Japanese"},
            {Product.Language.English,"English"},
            {Product.Language.SimplifiedChinese,"Chinese (Simplified)"},
            {Product.Language.TraditionalChinese,"Chinese (Traditional)"},
            {Product.Language.Afrikaans,"Afrikaans"},
            {Product.Language.Arabic,"Arabic"},
            {Product.Language.Basque,"Basque"},
            {Product.Language.Belarusian,"Belarusian"},
            {Product.Language.Bulgarian,"Bulgarian"},
            {Product.Language.Catalan,"Catalan"},
            {Product.Language.Czech,"Czech"},
            {Product.Language.Danish,"Danish"},
            {Product.Language.Dutch,"Dutch"},
            {Product.Language.Estonian,"Estonian"},
            {Product.Language.Faroese,"Faroese"},
            {Product.Language.Finnish,"Finnish"},
            {Product.Language.French,"French"},
            {Product.Language.German,"German"},
            {Product.Language.Greek,"Greek"},
            {Product.Language.Hebrew,"Hebrew"},
            {Product.Language.Icelandic,"Icelandic"},
            {Product.Language.Indonesian,"Indonesian"},
            {Product.Language.Italian,"Italian"},
            {Product.Language.Korean,"Korean"},
            {Product.Language.Latvian,"Latvian"},
            {Product.Language.Lithuanian,"Lithuanian"},
            {Product.Language.Norwegian,"Norwegian"},
            {Product.Language.Polish,"Polish"},
            {Product.Language.Portuguese,"Portuguese"},
            {Product.Language.Romanian,"Romanian"},
            {Product.Language.Russian,"Russian"},
            {Product.Language.SerboCroatian, "Serbo Croatian"},
            {Product.Language.Slovak,"Slovak"},
            {Product.Language.Slovenian,"Slovenian"},
            {Product.Language.Spanish,"Spanish"},
            {Product.Language.Swedish,"Swedish"},
            {Product.Language.Thai,"Thai"},
            {Product.Language.Turkish,"Turkish"},
            {Product.Language.Ukrainian,"Ukrainian"},
            {Product.Language.Vietnamese,"Vietnamese"},
            {Product.Language.Hungarian,"Hungarian"}
        };

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
    }
}
