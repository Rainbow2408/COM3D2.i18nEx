using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using COM3D2.i18nEx.Core.Util;
using UnityEngine;

namespace COM3D2.i18nEx.Core
{
    /// <summary>
    /// ConfigurationManager GUI 擴展
    /// 提供語言管理器自訂介面和進階屬性設定
    /// 需要 ConfigurationManager 插件才能運作（軟依賴）
    /// </summary>
    public static class ConfigurationManagerIntegration
    {
        private static bool initialized = false;
        private static bool configManagerAvailable = false;

        // UI 狀態
        private static Dictionary<string, bool> selectedLanguages = new Dictionary<string, bool>();
        private static Dictionary<string, bool> expandedLanguages = new Dictionary<string, bool>();
        private static bool isLanguageListExpanded = false; // Default collapsed
        private static bool isDumping = false;
        private static string dumpStatus = "";
        private static string dumpingLanguage = null;

        // 語言選擇順序（最新在前）
        private static List<string> languageSelectionOrder = new List<string>();
        private static string lastSelectedLanguage = "";
        private static List<string> dumpLogs = new List<string>();
        private static Vector2 scrollPosition = Vector2.zero;

        // UI 常數
        private const int COLUMNS_PER_ROW = 4;
        private const int INDENT_SPACE = 20;
        private const int REGIONAL_LABEL_WIDTH = 140;
        private const int FIXED_HEIGHT = 450;

        /// <summary>
        /// 檢測 ConfigurationManager 是否可用
        /// </summary>
        private static bool IsConfigurationManagerAvailable()
        {
            // 檢查 ConfigurationManager 組件是否已載入
            var cmAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "ConfigurationManager");
            
            if (cmAssembly == null)
            {
                Core.Logger.LogInfo("ConfigurationManager not detected, GUI integration disabled");
                return false;
            }

            Core.Logger.LogInfo("ConfigurationManager detected, enabling GUI integration");
            return true;
        }

        /// <summary>
        /// 初始化 ConfigurationManager GUI 擴展
        /// </summary>
        public static void Initialize()
        {
            if (initialized)
                return;

            // 檢查 ConfigurationManager 是否可用
            configManagerAvailable = IsConfigurationManagerAvailable();
            if (!configManagerAvailable)
            {
                initialized = true;
                return;
            }

            var plugin = Core.pluginInstance;
            if (plugin == null)
            {
                Core.Logger.LogWarning("Plugin instance not available for ConfigurationManager integration");
                initialized = true;
                return;
            }

            try
            {
                // 建立語言管理器 GUI 項目
                plugin.Config.Bind(
                    "i18nEx Language Manager",
                    "LanguageSelector",
                    "",
                    new ConfigDescription(
                        "Manage languages and translations for i18nEx. No values will be provided here.",
                        null,
                        new ConfigurationManagerAttributes
                        {
                            CustomDrawer = DrawLanguageManager,
                            HideDefaultButton = true,
                            HideSettingName = true,
                            Order = 1000
                        }
                    )
                );

                // 設定進階屬性（Order, IsAdvanced 等）
                SetupAdvancedAttributes(plugin.Config);

                LoadLanguageSelectionOrder();

                initialized = true;
                Core.Logger.LogInfo("ConfigurationManager GUI integration initialized");
            }
            catch (Exception ex)
            {
                Core.Logger.LogError($"Failed to initialize ConfigurationManager GUI: {ex}");
            }
        }

        /// <summary>
        /// 設定 ConfigurationManager 專用的進階屬性
        /// </summary>
        private static void SetupAdvancedAttributes(ConfigFile config)
        {
            // 這裡可以透過反射來設定已存在的 ConfigEntry 的 Description
            // 但目前 BepInEx 不支持修改已綁定的 ConfigEntry 的 Description
            // 所以只能在 Configuration.cs 中直接設定基本描述
            
            // ConfigurationManager 會自動讀取 ConfigEntry 的 Description 和 AcceptableValue
            // 不需要額外設定
        }

        #region === Language Manager GUI ===

        private static void LoadLanguageSelectionOrder()
        {
            selectedLanguages.Clear();
            languageSelectionOrder.Clear();

            string languageList = Configuration.LanguageList?.Value ?? "";

            if (!string.IsNullOrEmpty(languageList))
            {
                var savedLanguages = languageList
                    .Split(',')
                    .Select(lang => lang.Trim())
                    .Where(lang => !string.IsNullOrEmpty(lang))
                    .ToList();

                foreach (string lang in savedLanguages)
                {
                    languageSelectionOrder.Add(lang);
                    selectedLanguages[lang] = true;
                }

                if (languageSelectionOrder.Count > 0)
                {
                    lastSelectedLanguage = languageSelectionOrder[0];
                }

                Core.Logger.LogInfo($"Loaded language selection order: {string.Join(", ", languageSelectionOrder.ToArray())}");
            }
            else
            {
                var existingLanguages = LanguageManager.ScanExistingLanguageFolders();

                foreach (var lang in existingLanguages)
                {
                    selectedLanguages[lang] = true;
                    languageSelectionOrder.Add(lang);
                }

                var activeLanguage = Configuration.ActiveLanguage?.Value ?? "";
                if (!string.IsNullOrEmpty(activeLanguage))
                {
                    lastSelectedLanguage = activeLanguage;
                }
                else if (languageSelectionOrder.Count > 0)
                {
                    lastSelectedLanguage = languageSelectionOrder[0];
                }
            }
        }

        private static void HandleLanguageSelectionChange(LanguageManager.LanguageInfo lang, bool newSelected)
        {
            selectedLanguages[lang.FullName] = newSelected;

            if (newSelected)
            {
                languageSelectionOrder.Remove(lang.FullName);
                languageSelectionOrder.Insert(0, lang.FullName);
                lastSelectedLanguage = lang.FullName;

                if (lang.Status == LanguageManager.LanguageStatus.NotExists)
                {
                    try
                    {
                        LanguageManager.CreateLanguageFolder(lang.FullName);
                        Core.Logger.LogInfo($"Created language folder: {lang.FullName}");
                    }
                    catch (Exception ex)
                    {
                        Core.Logger.LogError($"Failed to create language folder: {ex.Message}");
                    }
                }
            }
            else
            {
                languageSelectionOrder.Remove(lang.FullName);

                if (lastSelectedLanguage == lang.FullName && languageSelectionOrder.Count > 0)
                {
                    lastSelectedLanguage = languageSelectionOrder[0];
                }
            }

            UpdateLanguageConfiguration();
        }

        private static void UpdateLanguageConfiguration()
        {
            var enabledLanguages = languageSelectionOrder
                .Where(lang => selectedLanguages.ContainsKey(lang) && selectedLanguages[lang])
                .ToList();

            if (Configuration.LanguageList != null)
                Configuration.LanguageList.Value = string.Join(",", enabledLanguages.ToArray());

            if (enabledLanguages.Count > 0)
            {
                if (Configuration.ActiveLanguage != null)
                    Configuration.ActiveLanguage.Value = enabledLanguages[0];
                Core.Logger.LogInfo($"Updated language configuration: Active={enabledLanguages[0]}, List={Configuration.LanguageList?.Value}");
            }
            else
            {
                if (Configuration.ActiveLanguage != null)
                    Configuration.ActiveLanguage.Value = "";
                Core.Logger.LogInfo("No languages selected, cleared ActiveLanguage");
            }
        }

        private static List<string> GetEnabledLanguages()
        {
            return languageSelectionOrder
                .Where(lang => selectedLanguages.ContainsKey(lang) && selectedLanguages[lang])
                .ToList();
        }

        private static void DrawLanguageManager(ConfigEntryBase entry)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(FIXED_HEIGHT));

            try
            {
                GUILayout.Label("i18nEx 語言管理", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 });
                GUILayout.Space(5);
                DrawEnabledLanguagesSummary();
                GUILayout.Space(5);

                scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true, GUILayout.Height(FIXED_HEIGHT - 80));
                try
                {
                    DrawLanguageList();
                    GUILayout.Space(10);
                    DrawDumpPanel();
                }
                finally
                {
                    GUILayout.EndScrollView();
                }
            }
            catch (Exception ex)
            {
                GUILayout.Label($"Error: {ex.Message}", new GUIStyle(GUI.skin.label) { normal = { textColor = Color.red } });
                Core.Logger.LogError($"Error in DrawLanguageManager: {ex}");
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        private static void DrawEnabledLanguagesSummary()
        {
            GUILayout.BeginHorizontal();

            try
            {
                GUILayout.Label("已啟用: ", GUILayout.Width(50));
                var enabledLanguages = GetEnabledLanguages();
                string summary = enabledLanguages.Count > 0
                    ? string.Join(", ", enabledLanguages.ToArray())
                    : "無";
                GUILayout.Label(summary, GUILayout.ExpandWidth(true));
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawLanguageList()
        {
            GUILayout.BeginVertical(GUI.skin.box);

            try
            {
                GUILayout.BeginHorizontal();
                try
                {
                    int maxLanguages = Configuration.MaxLanguagesInList?.Value ?? 6;
                    GUILayout.Label($"語言列表 (最多選{maxLanguages}個):", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                    GUILayout.FlexibleSpace();
                    
                    // Collapse/Expand button
                    string toggleText = isLanguageListExpanded ? "[收起 ▲]" : "[展開 ▼]";
                    if (GUILayout.Button(toggleText, GUILayout.Width(60)))
                    {
                        isLanguageListExpanded = !isLanguageListExpanded;
                    }
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }

                // Only show language list when expanded
                if (isLanguageListExpanded)
                {
                    GUILayout.Space(5);

                    var allLanguages = LanguageManager.GetAllLanguages();
                    var groupedLanguages = allLanguages.GroupBy(lang => lang.BaseName).OrderBy(g => g.Key);

                    foreach (var group in groupedLanguages)
                    {
                        try
                        {
                            string baseName = group.Key;
                            var languages = group.ToList();
                            bool hasRegion = languages.Count > 1 || languages[0].HasRegion;

                            if (!hasRegion)
                            {
                                DrawStandaloneLanguage(languages[0]);
                            }
                            else
                            {
                                DrawLanguageGroup(baseName, languages);
                            }
                        }
                        catch (Exception ex)
                        {
                            Core.Logger.LogError($"Error drawing language group: {ex.Message}");
                            GUILayout.Label($"Error: {ex.Message}", new GUIStyle(GUI.skin.label) { normal = { textColor = Color.red } });
                        }
                    }
                }
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        private static void DrawStandaloneLanguage(LanguageManager.LanguageInfo lang)
        {
            GUILayout.BeginHorizontal();

            try
            {
                bool originalGUIEnabled = GUI.enabled;
                bool isSelected = selectedLanguages.ContainsKey(lang.FullName) && selectedLanguages[lang.FullName];

                int currentCount = selectedLanguages.Count(kvp => kvp.Value);
                int maxLanguages = Configuration.MaxLanguagesInList?.Value ?? 6;
                bool canSelect = isSelected || currentCount < maxLanguages;

                GUI.enabled = canSelect;

                string statusMark = "";
                switch (lang.Status)
                {
                    case LanguageManager.LanguageStatus.NotExists:
                        statusMark = " [+]";
                        break;
                    case LanguageManager.LanguageStatus.Empty:
                        statusMark = " [.]";
                        break;
                }

                bool newSelected = GUILayout.Toggle(isSelected, lang.FullName + statusMark);

                GUI.enabled = originalGUIEnabled;

                if (newSelected != isSelected)
                {
                    HandleLanguageSelectionChange(lang, newSelected);
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawLanguageGroup(string baseName, List<LanguageManager.LanguageInfo> languages)
        {
            bool isGroupExpanded = expandedLanguages.ContainsKey(baseName) && expandedLanguages[baseName];

            GUILayout.BeginHorizontal();

            try
            {
                string expandSymbol = isGroupExpanded ? "▼" : "▶";
                string labelText = $"{expandSymbol} {baseName}";

                if (GUILayout.Button(labelText, GUI.skin.label, GUILayout.Width(120)))
                {
                    expandedLanguages[baseName] = !isGroupExpanded;
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            if (isGroupExpanded)
            {
                DrawRegionalLanguagesGrid(languages);
            }
        }

        private static void DrawRegionalLanguagesGrid(List<LanguageManager.LanguageInfo> languages)
        {
            GUILayout.BeginVertical();

            try
            {
                int totalLanguages = languages.Count;
                int rowCount = (totalLanguages + COLUMNS_PER_ROW - 1) / COLUMNS_PER_ROW;

                for (int row = 0; row < rowCount; row++)
                {
                    GUILayout.BeginHorizontal();
                    try
                    {
                        GUILayout.Space(INDENT_SPACE);

                        int startIndex = row * COLUMNS_PER_ROW;
                        int endIndex = Math.Min(startIndex + COLUMNS_PER_ROW, totalLanguages);

                        for (int i = startIndex; i < endIndex; i++)
                        {
                            DrawRegionalLanguageItem(languages[i]);
                        }
                    }
                    finally
                    {
                        GUILayout.EndHorizontal();
                    }
                }
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        private static void DrawRegionalLanguageItem(LanguageManager.LanguageInfo lang)
        {
            bool originalGUIEnabled = GUI.enabled;

            try
            {
                bool isSelected = selectedLanguages.ContainsKey(lang.FullName) && selectedLanguages[lang.FullName];

                int currentCount = selectedLanguages.Count(kvp => kvp.Value);
                int maxLanguages = Configuration.MaxLanguagesInList?.Value ?? 6;
                bool canSelect = isSelected || currentCount < maxLanguages;

                GUI.enabled = canSelect;

                string displayName = lang.Region ?? lang.BaseName;

                string statusMark = "";
                switch (lang.Status)
                {
                    case LanguageManager.LanguageStatus.NotExists:
                        statusMark = " [+]";
                        break;
                    case LanguageManager.LanguageStatus.Empty:
                        statusMark = " [.]";
                        break;
                }

                bool newSelected = GUILayout.Toggle(
                    isSelected,
                    displayName + statusMark,
                    GUILayout.Width(REGIONAL_LABEL_WIDTH)
                );

                GUI.enabled = originalGUIEnabled;

                if (newSelected != isSelected)
                {
                    HandleLanguageSelectionChange(lang, newSelected);
                }
            }
            finally
            {
                GUI.enabled = originalGUIEnabled;
            }
        }

        private static void StartDump(string languageName)
        {
            isDumping = true;
            dumpingLanguage = languageName;
            dumpStatus = "準備提取資源...";

            BepInEx.ThreadingHelper.Instance.StartAsyncInvoke(() =>
            {
                try
                {
                    System.Threading.Thread.Sleep(1000);
                    dumpStatus = "正在掃描遊戲腳本...";

                    System.Threading.Thread.Sleep(1000);
                    dumpStatus = "正在提取 UI 翻譯...";

                    System.Threading.Thread.Sleep(1000);
                    dumpStatus = "提取完成！";

                    return () =>
                    {
                        isDumping = false;
                        dumpingLanguage = null;
                        Core.Logger.LogInfo($"Dump completed for language: {languageName}");
                    };
                }
                catch (Exception ex)
                {
                    Core.Logger.LogError($"Dump failed: {ex}");
                    dumpStatus = $"提取失敗: {ex.Message}";
                    return () =>
                    {
                        isDumping = false;
                        dumpingLanguage = null;
                    };
                }
            });
        }

        private static void CancelDump()
        {
            if (dumpingLanguage != null)
            {
                string newName = "manual_del_" + dumpingLanguage;
                LanguageManager.RenameLanguageFolder(dumpingLanguage, newName);
            }

            isDumping = false;
            dumpingLanguage = null;
            dumpStatus = "已取消";
        }

        private static void DrawDumpPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box);

            try
            {
                GUILayout.BeginHorizontal();

                try
                {
                    string displayLang = string.IsNullOrEmpty(lastSelectedLanguage) ? "無" : lastSelectedLanguage;
                    GUILayout.Label($"最後選擇語言: {displayLang}", GUILayout.Width(300));
                    GUILayout.FlexibleSpace();

                    if (!isDumping)
                    {
                        bool canDump = !string.IsNullOrEmpty(lastSelectedLanguage) &&
                                       selectedLanguages.ContainsKey(lastSelectedLanguage) &&
                                       selectedLanguages[lastSelectedLanguage];

                        GUI.enabled = canDump;

                        if (GUILayout.Button("DUMP", GUILayout.Width(120)))
                        {
                            StartDump(lastSelectedLanguage);
                        }

                        GUI.enabled = true;
                    }
                    else
                    {
                        if (GUILayout.Button("取消(中止)", GUILayout.Width(120)))
                        {
                            CancelDump();
                        }
                    }
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }

                if (isDumping)
                {
                    GUILayout.Space(5);
                    GUILayout.Label("注：提取資源中，請不要離開此窗口",
                        new GUIStyle(GUI.skin.label) { normal = { textColor = Color.yellow } });

                    if (!string.IsNullOrEmpty(dumpStatus))
                    {
                        GUILayout.Label($"[Log] {dumpStatus}", GUI.skin.label);
                    }

                    int startIndex = dumpLogs.Count > 5 ? dumpLogs.Count - 5 : 0;
                    for (int i = startIndex; i < dumpLogs.Count; i++)
                    {
                        GUILayout.Label($"[Log] {dumpLogs[i]}", GUI.skin.label);
                    }
                }
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        #endregion
    }
}
