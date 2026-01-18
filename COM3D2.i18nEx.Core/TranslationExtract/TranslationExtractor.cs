using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using COM3D2.i18nEx.Core.TranslationExtract.DataSources;
using COM3D2.i18nEx.Core.Util;
using I2.Loc;
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
        private static readonly HashSet<string> filesToSkip = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        
        private static int translatedLines;

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
            try
            {
                // 確定輸出路徑
                string outputPath = GetOutputPath(languageName);
                Core.Logger.LogInfo($"Starting extraction to: {outputPath}");

                progressCallback?.Invoke("初始化提取器...");
                translatedLines = 0;

                if (options.DumpUI)
                {
                    progressCallback?.Invoke("正在提取 UI 翻譯...");
                    ExtractUI(outputPath);
                }

                if (options.DumpScripts)
                {
                    progressCallback?.Invoke("正在提取遊戲腳本...");
                    ExtractScripts(outputPath, progressCallback);
                }

                if (options.DumpItemNames)
                {
                    progressCallback?.Invoke("正在提取物品名稱...");
                    ExtractItemNames(outputPath, options);
                }

                // DataSource based extractions
                if (options.DumpMaidStatus)
                {
                    progressCallback?.Invoke("正在提取女僕狀態...");
                    ExtractMaidStatus(outputPath, options);
                }

                if (options.DumpYotogi)
                {
                    progressCallback?.Invoke("正在提取夜伽資料...");
                    ExtractYotogi(outputPath, options);
                }

                if (options.DumpEvents)
                {
                    progressCallback?.Invoke("正在提取劇情事件...");
                    ExtractEvents(outputPath, options);
                }

                if (options.DumpSchedule)
                {
                    progressCallback?.Invoke("正在提取行程資料...");
                    ExtractSchedule(outputPath, options);
                }

                if (options.DumpTrophy)
                {
                    progressCallback?.Invoke("正在提取稱號資料...");
                    ExtractTrophy(outputPath, options);
                }

                if (options.DumpNPC)
                {
                    progressCallback?.Invoke("正在提取 NPC 資料...");
                    ExtractNPC(outputPath, options);
                }

                if (options.DumpGuest)
                {
                    progressCallback?.Invoke("正在提取訪客模式資料...");
                    ExtractGuest(outputPath, options);
                }

                if (options.DumpDance)
                {
                    progressCallback?.Invoke("正在提取舞蹈資料...");
                    ExtractDance(outputPath, options);
                }

                if (options.DumpMansion)
                {
                    progressCallback?.Invoke("正在提取豪宅資料...");
                    ExtractMansion(outputPath, options);
                }

                if (options.DumpMemory)
                {
                    progressCallback?.Invoke("正在提取回憶資料...");
                    ExtractMemory(outputPath, options);
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
        }

        /// <summary>
        /// 取得輸出路徑
        /// </summary>
        private static string GetOutputPath(string languageName)
        {
            var status = LanguageManager.GetLanguageStatus(languageName);

            // 如果語言已存在且有內容，輸出到 _dumped 資料夾
            if (status == LanguageManager.LanguageStatus.Exists)
            {
                return Path.Combine(Paths.TranslationsRoot, $"{languageName}_dumped");
            }

            // 否則直接輸出到語言資料夾
            return Path.Combine(Paths.TranslationsRoot, languageName);
        }

        #region UI Extraction

        private static void ExtractUI(string outputPath)
        {
            Core.Logger.LogInfo("Dumping UI localisation");

            var langs = LocalizationManager.GetAllLanguages();
            Core.Logger.LogInfo($"Currently {langs.Count} languages are known");

            Core.Logger.LogInfo($"Currently selected language is {LocalizationManager.CurrentLanguage}");
            Core.Logger.LogInfo($"There are {LocalizationManager.Sources.Count} language sources");

            var i2Path = Path.Combine(outputPath, "UI");

            foreach (var languageSource in LocalizationManager.Sources)
            {
                Core.Logger.LogInfo(
                    $"Dumping {languageSource.name} with languages: {string.Join(",", languageSource.mLanguages.Select(d => d.Name).ToArray())}");
                DumpI2Translations(languageSource, i2Path);
            }
        }

        private static void DumpI2Translations(LanguageSource src, string i2Path)
        {
            var sourcePath = Path.Combine(i2Path, src.name);
            if (!Directory.Exists(sourcePath))
                Directory.CreateDirectory(sourcePath);

            var categories = src.GetCategories(true);
            foreach (var category in categories)
            {
                var path = Path.Combine(sourcePath, $"{category}.csv");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, src.Export_CSV(category), UTF8);
            }
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

            foreach (var menu in menus)
            {
                try
                {
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

            foreach (var keyValuePair in swDict)
                keyValuePair.Value.Dispose();
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

        private static void ExtractDataSource(string outputPath, ITranslationDataSource source, ExtractOptions opts)
        {
            var unitPath = Path.Combine(Path.Combine(outputPath, "UI"), "Dynamic");
            using var sw = new CsvWriter();

            Core.Logger.LogInfo($"Getting {source.Name} data");
            source.Initialize();

            foreach (var entry in source.GetEntries())
            {
                sw.Write(unitPath, entry, opts.SkipTranslatedItems);
            }
            sw.Flush();
        }

        private static void ExtractMaidStatus(string outputPath, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("Maid Status",
                new PersonalDataSource(),
                new YotogiClassDataSource(),
                new JobClassDataSource(),
                new TitleDataSource(),
                new FeatureDataSource());
            ExtractDataSource(outputPath, source, opts);
        }

        private static void ExtractYotogi(string outputPath, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("Yotogi",
                new YotogiSkillDataSource(),
                new YotogiCommandDataSource());
            ExtractDataSource(outputPath, source, opts);
        }

        private static void ExtractEvents(string outputPath, ExtractOptions opts)
        {
            ExtractDataSource(outputPath, new ScenarioEventDataSource(), opts);
            ExtractDataSource(outputPath, new HoneymoonEventDataSource(), opts);
            ExtractDataSource(outputPath, new PrivateModeEventDataSource(), opts);
        }

        private static void ExtractSchedule(string outputPath, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("Schedule",
                new ScheduleTrainingDataSource(),
                new ScheduleYotogiDataSource(),
                new ScheduleCategoryDataSource());
            ExtractDataSource(outputPath, source, opts);
        }

        private static void ExtractTrophy(string outputPath, ExtractOptions opts)
        {
            ExtractDataSource(outputPath, new TrophyDataSource(), opts);
        }

        private static void ExtractNPC(string outputPath, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("NPC",
                new NPCDataSource(),
                new SubMaidDataSource());
            ExtractDataSource(outputPath, source, opts);
        }

        private static void ExtractGuest(string outputPath, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("Guest",
                new GuestManDataSource(),
                new GuestPlayDataSource(),
                new GuestRoomDataSource());
            ExtractDataSource(outputPath, source, opts);
        }

        private static void ExtractDance(string outputPath, ExtractOptions opts)
        {
            ExtractDataSource(outputPath, new DanceDataSource(), opts);
        }

        private static void ExtractMansion(string outputPath, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("Mansion",
                new MansionRoomDataSource(),
                new MansionEventDataSource());
            ExtractDataSource(outputPath, source, opts);
        }

        private static void ExtractMemory(string outputPath, ExtractOptions opts)
        {
            var source = new CompositeDataSourceBase("Memory",
                new MemoryStoryDataSource(),
                new MemoryDailyDataSource(),
                new EmpireLifeModeDataSource());
            ExtractDataSource(outputPath, source, opts);
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
