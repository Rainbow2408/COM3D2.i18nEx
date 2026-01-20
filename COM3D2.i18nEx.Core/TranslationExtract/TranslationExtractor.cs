using COM3D2.i18nEx.Core.TranslationExtract.DataSources;
using COM3D2.i18nEx.Core.Util;
using I2.Loc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace COM3D2.i18nEx.Core.TranslationExtract
{
    /// <summary>
    /// 翻譯資源提取器主類
    /// </summary>
    public static class TranslationExtractor
    {
        private static readonly Encoding UTF8 = new UTF8Encoding(true);
        private static readonly Regex textPattern = new("text=\"(?<text>.*)\"");
        private static readonly Regex namePattern = new("name=(?<name>.*)");
        
        private static readonly Dictionary<string, string> NpcNames = new Dictionary<string, string>();
        private static readonly HashSet<string> filesToSkip = new(StringComparer.InvariantCultureIgnoreCase);
        
        private static int translatedLines;

        // 取消機制
        private static volatile bool _cancelRequested;
        private static string _currentOutputPath;
        
        // 保護鎖機制
        private static readonly object _extractionLock = new object();
        private static volatile bool _isExtracting;
        
        /// <summary>
        /// 檢查是否正在提取中
        /// </summary>
        public static bool IsExtracting => _isExtracting;
        
        /// <summary>
        /// 檢查是否應該取消當前操作
        /// </summary>
        private static bool IsCancelled()
        {
            return _cancelRequested;
        }

        /// <summary>
        /// 提取翻譯資源到指定語言資料夾
        /// </summary>
        /// <param name="languageName">目標語言名稱（如 "English", "Chinese (Traditional)"）</param>
        /// <param name="options">提取選項</param>
        /// <param name="progressCallback">進度回調（接收狀態訊息）</param>
        /// <param name="completionCallback">完成回調（success, message）</param>
        public static void ExtractToLanguage(
            string languageName,
            ExtractOptions options,
            Action<string> progressCallback,
            Action<bool, string> completionCallback)
        {
            // 獲取鎖，確保同時只有一個提取在執行
            lock (_extractionLock)
            {
                // 如果已經有提取在執行，報告錯誤
                if (_isExtracting)
                {
                    completionCallback?.Invoke(false, "已有提取作業在執行中，請等待完成或取消後再試");
                    return;
                }
                _isExtracting = true;
            }
            try
            {
                // 重置取消狀態
                _cancelRequested = false;

                // 確定輸出路徑
                string outputPath = GetOutputPath(languageName);
                _currentOutputPath = outputPath;
                Core.Logger.LogInfo($"Starting extraction to: {outputPath}");

                progressCallback?.Invoke("初始化提取器...");
                translatedLines = 0;

                if (!IsCancelled() && options.DumpUI)
                {
                    progressCallback?.Invoke("正在提取 UI 翻譯...");
                    ExtractUI(outputPath, languageName);
                }

                if (!IsCancelled() && options.DumpScripts)
                {
                    progressCallback?.Invoke("正在提取遊戲腳本...");
                    ExtractScripts(outputPath, progressCallback);
                }

                if (!IsCancelled() && options.DumpItemNames)
                {
                    progressCallback?.Invoke("正在提取物品名稱...");
                    ExtractItemNames(outputPath, options);
                }

                // DataSource based extractions
                if (!IsCancelled() && options.DumpMaidStatus)
                {
                    progressCallback?.Invoke("正在提取女僕狀態...");
                    ExtractMaidStatus(outputPath, languageName, options);
                }

                if (!IsCancelled() && options.DumpYotogi)
                {
                    progressCallback?.Invoke("正在提取夜伽資料...");
                    ExtractYotogi(outputPath, languageName, options);
                }

                if (!IsCancelled() && options.DumpEvents)
                {
                    progressCallback?.Invoke("正在提取劇情事件...");
                    ExtractEvents(outputPath, languageName, options);
                }

                if (!IsCancelled() && options.DumpSchedule)
                {
                    progressCallback?.Invoke("正在提取行程資料...");
                    ExtractSchedule(outputPath, languageName, options);
                }

                if (!IsCancelled() && options.DumpTrophy)
                {
                    progressCallback?.Invoke("正在提取稱號資料...");
                    ExtractTrophy(outputPath, languageName, options);
                }

                if (!IsCancelled() && options.DumpNPC)
                {
                    progressCallback?.Invoke("正在提取 NPC 資料...");
                    ExtractNPC(outputPath, languageName, options);
                }

                if (!IsCancelled() && options.DumpGuest)
                {
                    progressCallback?.Invoke("正在提取訪客模式資料...");
                    ExtractGuest(outputPath, languageName, options);
                }

                if (!IsCancelled() && options.DumpDance)
                {
                    progressCallback?.Invoke("正在提取舞蹈資料...");
                    ExtractDance(outputPath, languageName, options);
                }

                if (!IsCancelled() && options.DumpMansion)
                {
                    progressCallback?.Invoke("正在提取豪宅資料...");
                    ExtractMansion(outputPath, languageName, options);
                }

                if (!IsCancelled() && options.DumpMemory)
                {
                    progressCallback?.Invoke("正在提取回憶資料...");
                    ExtractMemory(outputPath, languageName, options);
                }

                // 最終檢查是否被取消
                if (IsCancelled())
                {
                    HandleCancellation(outputPath);
                    completionCallback?.Invoke(false, "提取中斷！");
                    return;
                }

                Core.Logger.LogInfo($"Extraction completed! Dumped {translatedLines} script lines");
                Core.Logger.LogInfo($"Output location: {outputPath}");
                completionCallback?.Invoke(true, "提取完成！");
            }
            catch (Exception ex)
            {
                Core.Logger.LogError($"Extraction failed: {ex}");
                completionCallback?.Invoke(false, ex.Message);
            }
            finally
            {
                // 釋放鎖
                _isExtracting = false;
            }
        }
        
        /// <summary>
        /// 請求取消當前的提取操作
        /// </summary>
        public static void RequestCancel()
        {
            if (_currentOutputPath != null)
            {
                _cancelRequested = true;
                Core.Logger.LogInfo("Extraction cancellation requested");
            }
        }
        
        /// <summary>
        /// 處理取消操作，重新命名輸出資料夾
        /// </summary>
        private static void HandleCancellation(string outputPath)
        {
            Core.Logger.LogInfo($"Extraction cancelled, renaming output folder...");
            
            try
            {
                if (Directory.Exists(outputPath))
                {
                    var dirInfo = new DirectoryInfo(outputPath);
                    var parentDir = dirInfo.Parent?.FullName ?? Paths.TranslationsRoot;
                    var newName = "cancelled_" + dirInfo.Name;
                    var newPath = Path.Combine(parentDir, newName);
                    
                    // 避免重複命名
                    if (Directory.Exists(newPath))
                    {
                        Directory.Delete(newPath, true);
                    }
                    
                    Directory.Move(outputPath, newPath);
                    Core.Logger.LogInfo($"Renamed cancelled output to: {newPath}");
                }
            }
            catch (Exception ex)
            {
                Core.Logger.LogWarning($"Failed to rename cancelled output folder: {ex.Message}");
            }
            finally
            {
                _currentOutputPath = null;
                _cancelRequested = false;
            }
        }

        /// <summary>
        /// 取得輸出路徑
        /// </summary>
        private static string GetOutputPath(string languageName)
        {
            var status = LanguageManager.GetLanguageStatus(languageName);

            // 輸出到 dumped 資料夾
            var timestamp = DateTime.Now.ToString("HHmmss");
            return Path.Combine(Paths.TranslationsRoot, $"{languageName}_dumped_{timestamp}");
        }

        #region UI Extraction

        private static void ExtractUI(string outputPath, string targetLanguage)
        {
            Core.Logger.LogInfo("Dumping UI localisation");

            var langs = LocalizationManager.GetAllLanguages();
            Core.Logger.LogInfo($"Currently {langs.Count} languages are known");

            Core.Logger.LogInfo($"Currently selected language is {LocalizationManager.CurrentLanguage}");
            Core.Logger.LogInfo($"There are {LocalizationManager.Sources.Count} language sources");

            var i2Path = Path.Combine(outputPath, "UI");

            foreach (var languageSource in LocalizationManager.Sources)
            {
                if (IsCancelled()) return;

                Core.Logger.LogInfo(
                    $"Dumping {languageSource.name} with languages: {string.Join(",", languageSource.mLanguages.Select(d => d.Name).ToArray())}");
                DumpI2Translations(languageSource, i2Path, targetLanguage);
            }
        }

        private static void DumpI2Translations(LanguageSource src, string i2Path, string targetLanguage)
        {
            var sourcePath = Path.Combine(i2Path, src.name);
            if (!Directory.Exists(sourcePath))
                Directory.CreateDirectory(sourcePath);

            var categories = src.GetCategories(true);
            foreach (var category in categories)
            {
                var path = Path.Combine(sourcePath, $"{category}.csv");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                
                // 取得原始 CSV 並過濾欄位
                var originalCsv = src.Export_CSV(category);
                var filteredCsv = FilterCsvColumns(originalCsv, targetLanguage);
                File.WriteAllText(path, filteredCsv, UTF8);
            }
        }

        /// <summary>
        /// 過濾 CSV 欄位，只保留 Key, Type, Desc, Japanese, 目標語言
        /// </summary>
        private static string FilterCsvColumns(string csvContent, string targetLanguage)
        {
            return CsvHelper.FilterColumns(csvContent, targetLanguage);
        }

        #endregion

        #region Script Extraction

        private static void ExtractScripts(string outputPath, Action<string> progressCallback)
        {
            Core.Logger.LogInfo("Dumping game script translations...");
            Core.Logger.LogInfo("Getting all script files...");
            var scripts = GameUty.FileSystem.GetFileListAtExtension(".ks");
            Core.Logger.LogInfo($"Found {scripts.Length} scripts!");

            int count = 0;
            int total = scripts.Length;

            foreach (var scriptFile in scripts)
            {
                if (IsCancelled()) return;

                count++;
                if (count % 100 == 0)
                {
                    progressCallback?.Invoke($"正在提取腳本... ({count}/{total})");
                }

                using var f = GameUty.FileOpen(scriptFile);
                var script = NUty.SjisToUnicode(f.ReadAll());
                Core.Logger.LogInfo(scriptFile);
                ExtractTranslations(outputPath, scriptFile, script);
            }

            var tlDir = Path.Combine(outputPath, "Script");
            Directory.CreateDirectory(tlDir);
            var namesFile = Path.Combine(tlDir, "__npc_names.txt");
            File.WriteAllLines(namesFile, NpcNames.Select(n => $"{n.Key}\t{n.Value}").ToArray(), UTF8);
            NpcNames.Clear();
            filesToSkip.Clear();
        }

        private static void ExtractTranslations(string outputPath, string fileName, string script)
        {
            var tlDir = Path.Combine(outputPath, "Script");
            var dir = Path.Combine(tlDir, Path.GetDirectoryName(fileName));
            var name = Path.GetFileNameWithoutExtension(fileName);

            if (filesToSkip.Contains(name))
                return;

            Directory.CreateDirectory(dir);

            var lineList = new HashSet<string>();
            var lines = script.Split('\n');

            var sb = new StringBuilder();
            var captureTalk = false;
            var captureSubtitlePlay = false;
            SubtitleData subData = null;

            var captureSubtitlesList = new List<SubtitleData>();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.Length == 0)
                    continue;

                if (trimmedLine.StartsWith("@LoadSubtitleFile", StringComparison.InvariantCultureIgnoreCase))
                {
                    var sub = ParseTag(trimmedLine.Substring("@LoadSubtitleFile".Length));
                    if (sub.TryGetValue("file", out var subFileName))
                    {
                        filesToSkip.Add(subFileName);

                        try
                        {
                            using var f = GameUty.FileOpen($"{subFileName}.ks");
                            var parseTalk = false;
                            string[] talkTiming = null;
                            var subSb = new StringBuilder();
                            foreach (var subLine in NUty.SjisToUnicode(f.ReadAll()).Split('\n').Select(s => s.Trim())
                                                        .Where(s => s.Length != 0))
                                if (subLine.StartsWith("@talk", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    talkTiming = subLine.Substring("@talk".Length).Trim('[', ']', ' ').Split('-');
                                    parseTalk = true;
                                }
                                else if (subLine.StartsWith("@hitret", StringComparison.InvariantCultureIgnoreCase) &&
                                         parseTalk)
                                {
                                    parseTalk = false;
                                    var startTime = int.Parse(talkTiming[0]);
                                    var endTime = int.Parse(talkTiming[1]);
                                    var parts = SplitTranslation(subSb.ToString());
                                    captureSubtitlesList.Add(new SubtitleData
                                    {
                                        original = parts.Key,
                                        translation = parts.Value,
                                        startTime = startTime,
                                        displayTime = endTime - startTime
                                    });
                                    subSb.Length = 0;
                                    talkTiming = null;
                                }
                                else
                                {
                                    subSb.Append(subLine);
                                }
                        }
                        catch (Exception ex)
                        {
                            Core.Logger.LogWarning($"Failed to load subtitle file {subFileName}: {ex.Message}");
                        }
                    }
                }
                else if (trimmedLine.StartsWith("@SubtitleDisplayForPlayVoice", StringComparison.InvariantCultureIgnoreCase))
                {
                    captureSubtitlePlay = true;
                    var sub = ParseTag(trimmedLine.Substring("@SubtitleDisplayForPlayVoice".Length));
                    if (sub.TryGetValue("text", out var textValue))
                    {
                        var text = SplitTranslation(textValue);
                        subData = new SubtitleData
                        {
                            addDisplayTime = int.Parse(GetOrDefault(sub, "addtime", "0")),
                            displayTime = int.Parse(GetOrDefault(sub, "wait", "-1")),
                            original = text.Key,
                            translation = text.Value,
                            isCasino = sub.ContainsKey("mode_c")
                        };
                    }
                }
                else if (trimmedLine.StartsWith("@PlayVoice", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (captureSubtitlePlay && subData != null)
                    {
                        captureSubtitlePlay = false;
                        var data = ParseTag(trimmedLine.Substring("@PlayVoice".Length));
                        if (!data.TryGetValue("voice", out var voiceName))
                        {
                            subData = null;
                            continue;
                        }

                        subData.voice = voiceName;
                        lineList.Add($"@VoiceSubtitle{JsonUtility.ToJson(subData, false)}");
                        subData = null;
                    }
                    else if (captureSubtitlesList.Count > 0)
                    {
                        var subTl = captureSubtitlesList[0];
                        captureSubtitlesList.RemoveAt(0);

                        var data = ParseTag(trimmedLine.Substring("@PlayVoice".Length));
                        if (!data.TryGetValue("voice", out var voiceName))
                            continue;

                        subTl.voice = voiceName;
                        lineList.Add($"@VoiceSubtitle{JsonUtility.ToJson(subTl, false)}");
                    }
                }
                else if (trimmedLine.StartsWith("@talk", StringComparison.InvariantCultureIgnoreCase))
                {
                    captureTalk = true;
                    var match = namePattern.Match(trimmedLine);
                    if (match.Success)
                    {
                        var m = match.Groups["name"];
                        var parts = SplitTranslation(m.Value.Trim('\"'));
                        if (parts.Key.StartsWith("[HF", StringComparison.InvariantCulture) ||
                            parts.Key.StartsWith("[SF", StringComparison.InvariantCulture))
                            continue;
                        NpcNames[parts.Key] = parts.Value;
                    }
                }
                else if (captureTalk)
                {
                    if (trimmedLine.StartsWith("@", StringComparison.InvariantCultureIgnoreCase))
                    {
                        captureTalk = false;
                        var parts = SplitTranslation(sb.ToString());
                        sb.Length = 0;
                        lineList.Add($"{parts.Key}\t{parts.Value}");
                        continue;
                    }

                    sb.Append(trimmedLine);
                }
                else if (trimmedLine.StartsWith("@ChoicesSet", StringComparison.InvariantCultureIgnoreCase))
                {
                    var match = textPattern.Match(trimmedLine);
                    if (!match.Success)
                    {
                        Core.Logger.LogWarning($"[WARNING] Failed to extract line from \"{trimmedLine}\"");
                        continue;
                    }

                    var m = match.Groups["text"];
                    var parts = SplitTranslation(m.Value);
                    lineList.Add($"{parts.Key}\t{parts.Value}");
                }
            }

            if (lineList.Count != 0)
            {
                File.WriteAllLines(Path.Combine(dir, $"{name}.txt"), lineList.ToArray(), UTF8);
                translatedLines += lineList.Count;
            }
        }

        private static KeyValuePair<string, string> SplitTranslation(string txt)
        {
            int pos;
            if ((pos = txt.IndexOf("<e>", StringComparison.InvariantCultureIgnoreCase)) > 0)
            {
                translatedLines++;
                var orig = txt.Substring(0, pos).Trim();
                var tl = txt.Substring(pos + 3).Replace("…", "...").Trim();
                return new KeyValuePair<string, string>(orig, tl);
            }

            return new KeyValuePair<string, string>(txt.Trim(), string.Empty);
        }

        private static Dictionary<string, string> ParseTag(string line)
        {
            var result = new Dictionary<string, string>();
            var valueSb = new StringBuilder();
            var keySb = new StringBuilder();
            var captureValue = false;
            var quoted = false;
            var escapeNext = false;

            foreach (var c in line)
                if (captureValue)
                {
                    if (valueSb.Length == 0 && c == '"')
                    {
                        quoted = true;
                        continue;
                    }

                    if (escapeNext)
                    {
                        escapeNext = false;
                        valueSb.Append(c);
                        continue;
                    }

                    if (c == '\\')
                        escapeNext = true;

                    if (!quoted && char.IsWhiteSpace(c) || quoted && !escapeNext && c == '"')
                    {
                        quoted = false;
                        result[keySb.ToString()] = valueSb.ToString();
                        keySb.Length = 0;
                        valueSb.Length = 0;
                        captureValue = false;
                        continue;
                    }

                    valueSb.Append(c);
                }
                else
                {
                    if (keySb.Length == 0 && char.IsWhiteSpace(c))
                        continue;

                    if (char.IsWhiteSpace(c) && keySb.Length != 0)
                    {
                        result[keySb.ToString()] = "true";
                        keySb.Length = 0;
                        continue;
                    }

                    if (c == '=')
                    {
                        captureValue = true;
                        continue;
                    }

                    keySb.Append(c);
                }

            if (keySb.Length != 0)
                result[keySb.ToString()] = valueSb.Length == 0 ? "true" : valueSb.ToString();

            return result;
        }

        private static T GetOrDefault<T>(Dictionary<string, T> dic, string key, T def)
        {
            return dic.TryGetValue(key, out var val) ? val : def;
        }

        #endregion

        #region Item Names Extraction

        private static void ExtractItemNames(string outputPath, ExtractOptions opts)
        {
            var i2Path = Path.Combine(outputPath, "UI");
            var unitPath = Path.Combine(i2Path, "ItemNames");
            Directory.CreateDirectory(unitPath);

            var encoding = new UTF8Encoding(true);
            Core.Logger.LogInfo("Getting all .menu files (this might take a moment)...");
            var menus = GameUty.FileSystem.GetFileListAtExtension(".menu");

            Core.Logger.LogInfo($"Found {menus.Length} menus!");

            var swDict = new Dictionary<string, StreamWriter>();

            try
			{
                foreach (var menu in menus)
                {
                    try
                    {
                        if (IsCancelled()) return;

                        using var f = GameUty.FileOpen(menu);
                        using var br = new BinaryReader(new MemoryStream(f.ReadAll()));
                        Core.Logger.LogInfo(menu);
			    
                        br.ReadString();
                        br.ReadInt32();
                        br.ReadString();
                        var filename = Path.GetFileNameWithoutExtension(menu);
                        var name = br.ReadString();
                        var category = br.ReadString().ToLowerInvariant();
                        var info = br.ReadString();
			    
                        if (!swDict.TryGetValue(category, out var sw))
                        {
                            swDict[category] =
                                sw = new StreamWriter(Path.Combine(unitPath, $"{category}.csv"), false, encoding);
                            sw.WriteLine("Key,Type,Desc,Japanese,English");
                        }
			    
                        if (opts.SkipTranslatedItems &&
                            LocalizationManager.TryGetTranslation($"{category}/{filename}|name", out var _))
                            continue;
			    
                        sw.WriteLine($"{filename}|name,Text,,{EscapeCSVItem(name)},{EscapeCSVItem(name)}");
                        sw.WriteLine($"{filename}|info,Text,,{EscapeCSVItem(info)},{EscapeCSVItem(info)}");
                    }
                    catch (Exception ex)
                    {
                        Core.Logger.LogWarning($"Failed to process menu {menu}: {ex.Message}");
                    }
                }
            }
            finally
            {
                foreach (var keyValuePair in swDict)
                    keyValuePair.Value.Dispose();
			}
        }

        private static string EscapeCSVItem(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            if (str.Contains("\n") || str.Contains("\"") || str.Contains(","))
                return $"\"{str.Replace("\"", "\"\"")}\"";
            return str;
        }

        #endregion

        #region DataSource Extraction Methods

        private static void ExtractDataSource(string outputPath, string languageName, ITranslationDataSource source, ExtractOptions opts)
        {
            var unitPath = Path.Combine(Path.Combine(outputPath, "UI"), "Dynamic");
            using var sw = new CsvWriter(languageName);

            Core.Logger.LogInfo($"Getting {source.Name} data");
            source.Initialize();

            foreach (var entry in source.GetEntries())
            {
                sw.Write(unitPath, entry, opts.SkipTranslatedItems);
            }
            sw.Flush();
        }

        private static void ExtractMaidStatus(string outputPath, string languageName, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("Maid Status",
                new PersonalDataSource(),
                new YotogiClassDataSource(),
                new JobClassDataSource(),
                new TitleDataSource(),
                new FeatureDataSource());
            ExtractDataSource(outputPath, languageName, source, opts);
        }

        private static void ExtractYotogi(string outputPath, string languageName, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("Yotogi",
                new YotogiSkillDataSource(),
                new YotogiCommandDataSource());
            ExtractDataSource(outputPath, languageName, source, opts);
        }

        private static void ExtractEvents(string outputPath, string languageName, ExtractOptions opts)
        {
            ExtractDataSource(outputPath, languageName, new ScenarioEventDataSource(), opts);
            ExtractDataSource(outputPath, languageName, new HoneymoonEventDataSource(), opts);
            ExtractDataSource(outputPath, languageName, new PrivateModeEventDataSource(), opts);
        }

        private static void ExtractSchedule(string outputPath, string languageName, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("Schedule",
                new ScheduleTrainingDataSource(),
                new ScheduleYotogiDataSource(),
                new ScheduleCategoryDataSource());
            ExtractDataSource(outputPath, languageName, source, opts);
        }

        private static void ExtractTrophy(string outputPath, string languageName, ExtractOptions opts)
        {
            ExtractDataSource(outputPath, languageName, new TrophyDataSource(), opts);
        }

        private static void ExtractNPC(string outputPath, string languageName, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("NPC",
                new NPCDataSource(),
                new SubMaidDataSource());
            ExtractDataSource(outputPath, languageName, source, opts);
        }

        private static void ExtractGuest(string outputPath, string languageName, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("Guest",
                new GuestManDataSource(),
                new GuestPlayDataSource(),
                new GuestRoomDataSource());
            ExtractDataSource(outputPath, languageName, source, opts);
        }

        private static void ExtractDance(string outputPath, string languageName, ExtractOptions opts)
        {
            ExtractDataSource(outputPath, languageName, new DanceDataSource(), opts);
        }

        private static void ExtractMansion(string outputPath, string languageName, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("Mansion",
                new MansionRoomDataSource(),
                new MansionEventDataSource());
            ExtractDataSource(outputPath, languageName, source, opts);
        }

        private static void ExtractMemory(string outputPath, string languageName, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("Memory",
                new MemoryStoryDataSource(),
                new MemoryDailyDataSource(),
                new EmpireLifeModeDataSource());
            ExtractDataSource(outputPath, languageName, source, opts);
        }

        #endregion

        #region Helper Classes

        [Serializable]
        private class SubtitleData
        {
            public int addDisplayTime;
            public int displayTime = -1;
            public bool isCasino;
            public string original = string.Empty;
            public int startTime;
            public string translation = string.Empty;
            public string voice = string.Empty;
        }

        #endregion
    }
}
