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
        private static ConfigManager _configManager = null;

        // 快取的欄位參照（使用 AccessTools 以提升效能）
        private static readonly AccessTools.FieldRef<ConfigMgr, ConfigManager> _configMgrRef =
            AccessTools.FieldRefAccess<ConfigMgr, ConfigManager>("configMgr");
        private static readonly AccessTools.FieldRef<ConfigManager, UIPopupList> _systemLanguageRef =
            AccessTools.FieldRefAccess<ConfigManager, UIPopupList>("systemLanguage");
        private static readonly MethodInfo _getCurrentPopupListValueMethod =
            AccessTools.Method(typeof(ConfigManager), "GetCurrentPopupListValue");

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
                __result = (Configuration.EngUIStyle?.Value == true) ? Product.Language.English : Product.Language.Japanese;
            }
        }

        [HarmonyPatch(typeof(Product), nameof(Product.systemLanguage), MethodType.Setter)]
        [HarmonyPrefix]
        private static void SetSystemLanguage(ref bool __runOriginal)
        {
            if (CheckConfigLanguageName())
            {
                var activeLanguage = Configuration.ActiveLanguage?.Value ?? "";
                SetCurrentLanguage(I18nPrefix + activeLanguage);
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
            if ((Configuration.OverrideSubtitleOpacity?.Value == true) &&
                parent                                                    && parent.name == "YotogiPlayPanel")
            {
                if (Math.Abs(value - __instance.messageBgAlpha) < 0.001)
                    return false;
                value = Mathf.Clamp(Configuration.SubtitleOpacity?.Value ?? 1f, 0f, 1f);
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
            // 翻譯回退邏輯：i18nEx → 遊戲內建對應語言 → 日文 → 原處理
            // 如果結果為空或無效（包含 '/' 且 Term 包含結果）
            if (__result.IsNullOrWhiteSpace() || (__result.IndexOf('/') >= 0 && Term.Contains(__result)))
            {
                // 1. 首先嘗試從 i18nEx 自定義翻譯取得（已由主邏輯處理）

                // 2. 如果當前語言不是日文，嘗試從遊戲內建的對應語言取得
                if (!string.IsNullOrEmpty(overrideLanguage) && overrideLanguage != "Japanese")
                {
                    I2Source ??= LocalizationManager.Sources
                                .Skip(1)
                                .FirstOrDefault(source => source.name == "I2Languages");

                    if (I2Source != null && I2Source.TryGetTranslation(Term, out string translationFromBuiltin, overrideLanguage))
                    {
                        __result = translationFromBuiltin;
                        if (applyParameters)
                            LocalizationManager.ApplyLocalizationParams(ref __result, localParametersRoot);
                        if (LocalizationManager.IsRight2Left && FixForRTL)
                            __result = LocalizationManager.ApplyRTLfix(__result, maxLineLengthForRTL, ignoreRTLnumbers);

                        if (Configuration.UIVerboseLogging?.Value == true)
                            Core.Logger.LogInfo($"[I2Loc] Found translation in built-in {overrideLanguage}: \"{Term}\" => \"{__result}\"");
                        return;
                    }
                }

                // 3. 如果還是沒找到，嘗試從日文取得
                if (I2Source != null && I2Source.TryGetTranslation(Term, out string japaneseTranslation, "Japanese"))
                {
                    __result = japaneseTranslation;
                    if (applyParameters)
                        LocalizationManager.ApplyLocalizationParams(ref __result, localParametersRoot);
                    if (LocalizationManager.IsRight2Left && FixForRTL)
                        __result = LocalizationManager.ApplyRTLfix(__result, maxLineLengthForRTL, ignoreRTLnumbers);

                    if (Configuration.UIVerboseLogging?.Value == true)
                        Core.Logger.LogInfo($"[I2Loc] Fallback to Japanese: \"{Term}\" => \"{__result}\"");
                    return;
                }

                // 4. 如果還是沒找到，保持原本的處理（__result 保持原值）
                if (Configuration.UIVerboseLogging?.Value == true)
                    Core.Logger.LogWarning($"[I2Loc] No translation found for term: \"{Term}\"");
            }
            else if (Configuration.UIVerboseLogging?.Value == true)
            {
                Core.Logger.LogInfo($"[I2Loc] Translating term \"{Term}\" => \"{__result}\"");
            }
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
            _configManager = _configMgrRef(BaseMgr<ConfigMgr>.Instance);
            UIsystemLanguage = _systemLanguageRef(_configManager);
            GameObject PopupList = UIsystemLanguage.transform.gameObject;

            // 使用 Transform.Find 簡化子物件搜尋
            Transform labelTransform = PopupList.transform.Find("Label");
            UILabel_Label = labelTransform?.GetComponent<UILabel>();

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
                    string language = (string)_getCurrentPopupListValueMethod.Invoke(_configManager, null);

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

        // 重入保護標記
        private static bool _isReloadingLanguage = false;

        /// <summary>
        /// 準備語言重新載入：在 LoadLanguage 之前呼叫
        /// </summary>
        internal static void PrepareLanguageReload()
        {
            if (_isReloadingLanguage) return;  // 防止重入
            _isReloadingLanguage = true;

            if (CheckConfigLanguageName())
            {
                SetCurrentLanguage(I18nPrefix + Configuration.ActiveLanguage.Value);
            }
        }

        /// <summary>
        /// 完成語言重新載入：在 LoadLanguage 之後呼叫
        /// </summary>
        internal static void FinalizeLanguageReload()
        {
            if (!_isReloadingLanguage) return;  // 確保已經呼叫過 Prepare
            
            ReloadPopupList();
            _isReloadingLanguage = false;
        }

        // 儲存語言是否為官方語言的字典
        private static Dictionary<string, bool> OfficialLanguageDict = null;

        /// <summary>
        /// 翻譯語言術語並快取結果
        /// </summary>
        /// <param name="term">語言術語（如 "i18n/Lang/English" 或 "System/言語/English"）</param>
        /// <param name="isOfficialLanguage">輸出：該語言是否有官方內建版本</param>
        /// <returns>翻譯後的顯示名稱</returns>
        private static string TranslateAndCacheTerm(string term, out bool isOfficialLanguage)
        {
            isOfficialLanguage = false;

            // Check input - 如果 term 為空或為 "-" 則直接返回
            if (string.IsNullOrEmpty(term) || term == "-")
                return term;

            // Ensure dictionaries are initialized
            LanguageDict ??= new();
            OfficialLanguageDict ??= new();

            // 嘗試解析語言名稱
            if (!TryParseLanguage(term, out string langName, out _))
            {
                return term;
            }

            // 分別取得插件翻譯和內建翻譯
            string i18nTranslation = LocalizationManager.GetTranslation(I18nPrefix + langName, false);
            
            // 嘗試從 Product.Language 枚舉中找到對應的語言，取得正確的 System 術語
            string systemTranslation = null;
            foreach (Product.Language enumValue in Enum.GetValues(typeof(Product.Language)))
            {
                // 比較 I2 語言名稱（如 "English", "Chinese (Traditional)"）
                if (string.Equals(Product.EnumConvert.ToI2LocalizeLanguageName(enumValue), langName, StringComparison.OrdinalIgnoreCase))
                {
                    // 使用 GetString 取得日文術語（如 "英語", "中国語 (繁体字)"）作為 System 前綴
                    string systemLangName = Product.EnumConvert.GetString(enumValue);
                    systemTranslation = LocalizationManager.GetTranslation(SystemPrefix + systemLangName, false);
                    break;
                }
            }

            isOfficialLanguage = !string.IsNullOrEmpty(systemTranslation);

            // 優先使用插件翻譯，否則使用內建翻譯
            string translatedLang = !string.IsNullOrEmpty(i18nTranslation) ? i18nTranslation : systemTranslation;
            bool hasTranslation = !string.IsNullOrEmpty(translatedLang);

            string resultLang = hasTranslation ? translatedLang : term;
            string resultTerm = hasTranslation ? (I18nPrefix + langName) : term;

            LanguageDict[resultLang] = resultTerm;
            OfficialLanguageDict[resultTerm] = isOfficialLanguage;

            return resultLang;
        }

        /// <summary>
        /// 翻譯語言術語並快取結果（向後相容版本）
        /// </summary>
        private static string TranslateAndCacheTerm(string term)
        {
            return TranslateAndCacheTerm(term, out _);
        }

        /// <summary>
        /// 查詢語言是否為官方語言
        /// </summary>
        internal static bool IsOfficialLanguage(string term)
        {
            return OfficialLanguageDict?.TryGetValue(term, out bool isOfficial) == true && isOfficial;
        }

        /// <summary>
        /// 初始化官方語言字典，從 LanguageManager 取得所有語言並建立快取
        /// </summary>
        internal static void InitializeOfficialLanguageDict()
        {
            var allLanguages = LanguageManager.GetAllLanguages();
            
            foreach (var lang in allLanguages)
            {
                // 呼叫 TranslateAndCacheTerm 來建立 OfficialLanguageDict
                TranslateAndCacheTerm(I18nPrefix + lang.FullName, out _);
            }
            
            Core.Logger.LogInfo($"Initialized OfficialLanguageDict with {OfficialLanguageDict?.Count ?? 0} entries");
        }

        private static bool CheckConfigLanguageName()
        {
            var activeLanguage = Configuration.ActiveLanguage?.Value ?? "";
            
            // Check if there is a configured language
            if (string.IsNullOrEmpty(activeLanguage))
                return false;

            // Format language name
            string language = Utility.ReFormatLanguageName(activeLanguage, out string message, log: true);
            if (string.IsNullOrEmpty(language))
            {
                Core.Logger.LogWarning($"Invalid language name: {activeLanguage}.{message}");
                if (Configuration.ActiveLanguage != null)
                    Configuration.ActiveLanguage.Value = string.Empty;
                return false;
            }
            // If the formatted language is different from the configuration value, update configuration
            if (language != activeLanguage && Configuration.ActiveLanguage != null)
                Configuration.ActiveLanguage.Value = language;

            return true;
        }

        /// <summary>
        /// 語言項目資訊，包含顯示名稱和是否為官方語言
        /// </summary>
        private struct LanguageItem
        {
            public string DisplayName;
            public bool IsOfficialLanguage;
            public bool IsDisabled;  // 前綴有 "-" 表示禁用
        }

        /// <summary>
        /// 獲取已啟用的語言列表資訊（按選擇順序，最新在前）
        /// </summary>
        private static IEnumerable<LanguageItem> GetEnabledLanguageItems()
        {
            string languageList = Configuration.LanguageList?.Value ?? "";

            if (string.IsNullOrEmpty(languageList) || !Directory.Exists(Paths.TranslationsRoot))
                yield break;

            var languages = languageList
                .Split(',')
                .Select(lang => lang.Trim())
                .Where(lang => !string.IsNullOrEmpty(lang))
                .ToList();

            var invalidLanguages = new List<string>();

            foreach (string rawLang in languages)
            {
                bool isDisabled = rawLang.StartsWith("-");
                string langName = isDisabled ? rawLang.Substring(1) : rawLang;

                string formattedLang = Utility.ReFormatLanguageName(langName, out _);
                if (string.IsNullOrEmpty(formattedLang))
                {
                    // 記錄無效的語言名稱
                    invalidLanguages.Add(rawLang);
                    continue;
                }

                string displayName = TranslateAndCacheTerm(I18nPrefix + formattedLang, out bool isOfficial);

                yield return new LanguageItem
                {
                    DisplayName = displayName,
                    IsOfficialLanguage = isOfficial,
                    IsDisabled = isDisabled
                };
            }

            // 如果有無效的語言，從設定中移除
            if (invalidLanguages.Count > 0 && Configuration.LanguageList != null)
            {
                var validLanguages = languages.Except(invalidLanguages).ToList();
                Configuration.LanguageList.Value = string.Join(",", validLanguages.ToArray());
            }
        }

        /// <summary>
        /// 取得所有可用語言（排除禁用的語言）
        /// </summary>
        /// <param name="disabledLanguages">禁用的語言顯示名稱集合</param>
        private static IEnumerable<string> GetAvailableLanguages(HashSet<string> disabledLanguages)
        {
            // Return all official languages (filtering disabled ones)
            return UIsystemLanguage.items
                .Select(item => TranslateAndCacheTerm(item))
                .Where(lang => disabledLanguages == null || !disabledLanguages.Contains(lang));
        }

        private static void ReloadPopupList(bool LoadSelectOnly = false, bool ForceLoadI2Name = false)
        {
            // 防禦性檢查：確保 UI 元件已初始化
            if (pop == null || UIsystemLanguage == null)
                return;

            try
            {
                // 確保 pop.value 不為 null
                string currentValue = pop.value ?? "";

                // Determine the current value to display
                string val = string.Empty;
                var activeLanguage = Configuration.ActiveLanguage?.Value ?? "";

                // If the translation for the current value is in the dictionary
                if (!string.IsNullOrEmpty(currentValue) && LanguageDict != null && LanguageDict.TryGetValue(currentValue, out string Term))
                {
                    if (CheckConfigLanguageName())
                    {
                        // Use configured language
                        val = TranslateAndCacheTerm(I18nPrefix + activeLanguage);
                    }
                    else
                    {
                        // Use official language
                        val = TranslateAndCacheTerm(Term);
                    }
                }
                // If current value is empty or force loading is required
                else if (currentValue.IsNullOrWhiteSpace() || ForceLoadI2Name)
                {
                    val = TranslateAndCacheTerm(UIsystemLanguage.value);
                }

                // Only update when the value actually needs to change
                if (!string.IsNullOrEmpty(val) && val != pop.value)
                {
                    if (Configuration.UIVerboseLogging?.Value == true)
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

                    // 收集禁用的語言和已啟用的語言
                    var disabledLanguages = new HashSet<string>();
                    var languageItems = GetEnabledLanguageItems().ToList();

                    // 優先添加已選擇的語言（按順序），收集禁用的語言
                    foreach (var item in languageItems)
                    {
                        if (item.IsDisabled)
                        {
                            // 禁用的語言不添加到列表，但記錄下來以便過濾官方語言
                            if (item.IsOfficialLanguage)
                                disabledLanguages.Add(item.DisplayName);
                            continue;
                        }

                        if (!string.IsNullOrEmpty(item.DisplayName) && !pop.items.Contains(item.DisplayName))
                        {
                            pop.AddItem(item.DisplayName);
                        }
                    }

                    // Get all custom and official languages (filtering disabled ones)
                    var allLanguages = GetAvailableLanguages(disabledLanguages);

                    // 添加其他未選擇的語言
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
        // 語言前綴常數
        internal const string I18nPrefix = "i18n/Lang/";
        internal const string SystemPrefix = "System/言語/";

        private static void SetCurrentLanguage(string language)
        {
            // Logic from Product.Language.systemLanguage setter and ConfigManager.Init->systemLanguage.onChange
            //   Tip: Recommended language name reference I2.Loc.GoogleLanguages.mLanguageDef Dictionary
            //   Example: "Chinese/Traditional" => "Chinese (Traditional)"

            // 提前處理空值情況
            if (string.IsNullOrEmpty(language))
            {
                Core.Logger.LogWarning("Language is null or empty. Using default Japanese instead.");
                SetDefaultJapanese();
                return;
            }

            // 嘗試解析語言
            if (!TryParseLanguage(language, out string formattedLanguage, out bool isOfficialLanguage))
            {
                Core.Logger.LogWarning($"Could not process selected language: {language}. Using Japanese instead.");
                SetDefaultJapanese();
                return;
            }

            // 設定語言
            LocalizationManager.CurrentLanguage = formattedLanguage;

            // If it's an official language, don't save to configuration
            if (Configuration.ActiveLanguage != null)
                Configuration.ActiveLanguage.Value = isOfficialLanguage ? string.Empty : formattedLanguage;

            if (Configuration.UIVerboseLogging?.Value == true)
                Core.Logger.LogInfo($"Setting language to: {formattedLanguage} (from {language})");

            ReloadAllLanguages();
        }

        /// <summary>
        /// 嘗試解析語言字串並返回格式化後的語言名稱
        /// </summary>
        /// <param name="language">輸入的語言字串（可能是翻譯名稱、i18n/Lang/XXX 或 System/言語/XXX 格式）</param>
        /// <param name="formattedLanguage">格式化後的語言名稱</param>
        /// <param name="isOfficialLanguage">是否為官方語言</param>
        /// <returns>解析成功返回 true，否則返回 false</returns>
        private static bool TryParseLanguage(string language, out string formattedLanguage, out bool isOfficialLanguage)
        {
            formattedLanguage = null;
            isOfficialLanguage = false;

            // 情況1：language 為翻譯後的名稱（如 "繁體中文"），透過 LanguageDict 查找 termValue
            // 情況2：language 為 "i18n/Lang/XXX" 格式，直接作為 termValue 使用（插件管理）
            // 情況3：language 為 "System/言語/XXX" 格式，直接作為 termValue 使用（遊戲原始管理）
            string termValue = GetTermValue(language);
            if (string.IsNullOrEmpty(termValue))
                return false;

            // 判斷是官方語言還是自定義 i18nEx 語言
            isOfficialLanguage = !termValue.StartsWith(I18nPrefix);
            string prefix = isOfficialLanguage ? SystemPrefix : I18nPrefix;

            if (termValue.Length <= prefix.Length)
                return false;

            string languageName = termValue.Substring(prefix.Length);
            formattedLanguage = Utility.ReFormatLanguageName(languageName, out _, log: true);

            return !string.IsNullOrEmpty(formattedLanguage);
        }

        /// <summary>
        /// 從語言字串取得對應的 term value
        /// </summary>
        private static string GetTermValue(string language)
        {
            if (language.StartsWith(I18nPrefix) || language.StartsWith(SystemPrefix))
                return language;

            if (LanguageDict != null && LanguageDict.TryGetValue(language, out string dictValue))
                return dictValue;

            return null;
        }

        /// <summary>
        /// 設定預設語言為日文
        /// </summary>
        private static void SetDefaultJapanese()
        {
            LocalizationManager.CurrentLanguage = Product.EnumConvert.ToI2LocalizeLanguageName(Product.Language.Japanese);
            if (Configuration.ActiveLanguage != null)
                Configuration.ActiveLanguage.Value = string.Empty;
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
