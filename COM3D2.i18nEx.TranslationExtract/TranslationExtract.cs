using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using I2.Loc;
using MaidStatus;
using Schedule;
using UnityEngine;
using Yotogis;
using Kasizuki;
using Teikokusou;
using PrivateMaidMode;
using SceneNPCEdit;
using TranslationExtract.DataSources;

namespace TranslationExtract
{


    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class TranslationExtract : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        public static readonly string TL_DIR = Path.Combine("BepinEx", "TranslationExtract");

        internal static string[] Datas = new string[5]
        {
                string.Empty, "Text", string.Empty, string.Empty, string.Empty
        };

        private const int WIDTH = 200;
        private const int HEIGHT = 500;
        private const int MARGIN_X = 5;
        private const int MARGIN_TOP = 20;
        private const int MARGIN_BOTTOM = 5;

        private static readonly Regex textPattern = new("text=\"(?<text>.*)\"");
        private static readonly Regex namePattern = new("name=(?<name>.*)");
        private static readonly Encoding UTF8 = new UTF8Encoding(true);

        private static readonly Dictionary<string, string> NpcNames = new();


        private readonly HashSet<string> filesToSkip = new(StringComparer.InvariantCultureIgnoreCase);

        private readonly DumpOptions options = new();

        private GUIStyle bold;
        private bool displayGui;
        private bool dumping;

        private int translatedLines;

        private void Awake()
        {
            DontDestroyOnLoad(this);
            Logger = base.Logger;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.D))
                displayGui = !displayGui;
        }

        private void OnGUI()
        {
            if (!displayGui)
                return;
            if (bold == null)
                bold = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };


            void Toggle(string text, ref bool toggle)
            {
                toggle = GUILayout.Toggle(toggle, text);
            }

            void Window(int id)
            {
                GUILayout.BeginArea(new Rect(MARGIN_X, MARGIN_TOP, WIDTH - MARGIN_X * 2,
                                             HEIGHT                      - MARGIN_TOP - MARGIN_BOTTOM));
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.Label("Refer to the README on how to use the tool!\n\n", bold);
                        GUILayout.Label("Base dumps");
                        Toggle("Story scripts", ref options.dumpScripts);
                        Toggle("UI translations", ref options.dumpUITranslations);

                        GUILayout.Label("Advanced Dumps");
                        Toggle("Scenario Events", ref options.dumpEvents);
                        Toggle("Schedule Events", ref options.dumpSchedule);
                        Toggle("Yotogi", ref options.dumpYotogis);
                        Toggle("Maid Status", ref options.dumpMaidStatus);
                        Toggle("Trophy", ref options.dumpTrophy);
                        Toggle("NPC", ref options.dumpNPC);
                        Toggle("Guest Mode", ref options.dumpGuest);
                        Toggle("Dances", ref options.dumpDance);
                        Toggle("Mansion", ref options.dumpMansion);
                        Toggle(".menu", ref options.dumpItemNames);

                        GUILayout.Label("Other");
                        Toggle("Skip translated items", ref options.skipTranslatedItems);

                        GUI.enabled = !dumping;
                        if (GUILayout.Button("Dump!"))
                        {
                            dumping = true;
                            StartCoroutine(DumpGame());
                        }

                        GUI.enabled = true;
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndArea();
            }

            GUI.Window(6969, new Rect(Screen.width - WIDTH, (Screen.height - HEIGHT) / 2f, WIDTH, HEIGHT), Window,
                       "TranslationExtract");
        }

        private static void DumpI2Translations(LanguageSource src)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
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

        private IEnumerator DumpGame()
        {
            var opts = new DumpOptions(options);
            yield return null;
            Dump(opts);
            dumping = false;
        }

        private void DumpUI()
        {
            Logger.LogInfo("Dumping UI localisation");

            var langs = LocalizationManager.GetAllLanguages();
            Logger.LogInfo($"Currently {langs.Count} languages are known");
            foreach (var language in langs)
                Logger.LogInfo($"{language}");

            Logger.LogInfo($"Currently selected language is {LocalizationManager.CurrentLanguage}");
            Logger.LogInfo($"There are {LocalizationManager.Sources.Count} language sources");

            foreach (var languageSource in LocalizationManager.Sources)
            {
                Logger.LogInfo(
                          $"Dumping {languageSource.name} with languages: {string.Join(",", languageSource.mLanguages.Select(d => d.Name).ToArray())}. GSheets: {languageSource.HasGoogleSpreadsheet()}");
                DumpI2Translations(languageSource);
            }
        }

        private KeyValuePair<string, string> SplitTranslation(string txt)
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

        private void ExtractTranslations(string fileName, string script)
        {
            var tlDir = Path.Combine(TL_DIR, "Script");
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
                    var subFileName = sub["file"];

                    filesToSkip.Add(subFileName);

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
                else if (trimmedLine.StartsWith("@SubtitleDisplayForPlayVoice", StringComparison.InvariantCultureIgnoreCase))
                {
                    captureSubtitlePlay = true;
                    var sub = ParseTag(trimmedLine.Substring("Capture".Length));
                    var text = SplitTranslation(sub["text"]);
                    subData = new SubtitleData
                    {
                        addDisplayTime = int.Parse(GetOrDefault(sub, "addtime", "0")),
                        displayTime = int.Parse(GetOrDefault(sub, "wait", "-1")),
                        original = text.Key,
                        translation = text.Value,
                        isCasino = sub.ContainsKey("mode_c")
                    };
                }
                else if (trimmedLine.StartsWith("@PlayVoice", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (captureSubtitlePlay)
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
                        Logger.LogWarning($"[WARNING] Failed to extract line from \"{trimmedLine}\"");
                        continue;
                    }

                    var m = match.Groups["text"];
                    var parts = SplitTranslation(m.Value);
                    lineList.Add($"{parts.Key}\t{parts.Value}");
                }
            }

            if (lineList.Count != 0)
                File.WriteAllLines(Path.Combine(dir, $"{name}.txt"), lineList.ToArray(), UTF8);
        }

        private void DumpScripts()
        {
            Logger.LogInfo("Dumping game script translations...");
            Logger.LogInfo("Getting all script files...");
            var scripts = GameUty.FileSystem.GetFileListAtExtension(".ks");
            Logger.LogInfo($"Found {scripts.Length} scripts!");

            foreach (var scriptFile in scripts)
            {
                using var f = GameUty.FileOpen(scriptFile);
                var script = NUty.SjisToUnicode(f.ReadAll());
                Logger.LogDebug(scriptFile);
                ExtractTranslations(scriptFile, script);
            }

            var tlDir = Path.Combine(TL_DIR, "Script");
            var namesFile = Path.Combine(tlDir, "__npc_names.txt");
            File.WriteAllLines(namesFile, NpcNames.Select(n => $"{n.Key}\t{n.Value}").ToArray(), UTF8);
            NpcNames.Clear();
            filesToSkip.Clear();
        }

        private void DumpItemNames(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "ItemNames");
            Directory.CreateDirectory(unitPath);

            var encoding = new UTF8Encoding(true);
            Logger.LogInfo("Getting all .menu files (this might take a moment)...");
            var menus = GameUty.FileSystem.GetFileListAtExtension(".menu");

            Logger.LogInfo($"Found {menus.Length} menus!");

            var swDict = new Dictionary<string, StreamWriter>();

            foreach (var menu in menus)
            {
                using var f = GameUty.FileOpen(menu);
                using var br = new BinaryReader(new MemoryStream(f.ReadAll()));
                Logger.LogDebug(menu);

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

                if (opts.skipTranslatedItems &&
                    LocalizationManager.TryGetTranslation($"{category}/{filename}|name", out var _))
                    continue;
                sw.WriteLine($"{filename}|name,Text,,{EscapeCSVItem(name)},{EscapeCSVItem(name)}");
                sw.WriteLine($"{filename}|info,Text,,{EscapeCSVItem(info)},{EscapeCSVItem(info)}");
            }

            foreach (var keyValuePair in swDict)
                keyValuePair.Value.Dispose();
        }


        /// <summary>
        /// 使用統一架構的資料源導出方法
        /// </summary>
        private void DumpDataSource(ITranslationDataSource source, DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "Dynamic");
            using var sw = new CsvData();

            Logger.LogInfo($"Getting {source.Name} data");
            source.Initialize();

            foreach (var entry in source.GetEntries())
            {
                Datas[0] = entry.Term;
                Datas[2] = entry.Desc ?? string.Empty;
                Datas[3] = entry.Original;
                Datas[4] = entry.GetTranslation();
                sw.Writer(unitPath, Datas, opts.skipTranslatedItems);
            }
            sw.WriteCSV();
        }

        private void DumpScenarioEvents(DumpOptions opts)
        {
            var source = new ScenarioEventDataSource { Logger = Logger };
            DumpDataSource(source, opts);
        }

        private void DumpHoneyMoonEvents(DumpOptions opts)
        {
            var source = new HoneymoonEventDataSource { Logger = Logger };
            DumpDataSource(source, opts);
        }

        private void DumpPrivateModeEvents(DumpOptions opts)
        {
            var source = new PrivateModeEventDataSource { Logger = Logger };
            DumpDataSource(source, opts);
        }


        private void DumpSchedule(DumpOptions opts)
        {
            var source = new CompositeDataSource("Schedule",
                new ScheduleTrainingDataSource(),
                new ScheduleYotogiDataSource(),
                new ScheduleCategoryDataSource());
            source.SetLoggerForAllSources(Logger);
            DumpDataSource(source, opts);
        }

        private void DumpMaidStatus(DumpOptions opts)
        {
            var source = new CompositeDataSource("Maid Status",
                new PersonalDataSource(),
                new YotogiClassDataSource(),
                new JobClassDataSource(),
                new TitleDataSource(),
                new FeatureDataSource());
            source.SetLoggerForAllSources(Logger);
            DumpDataSource(source, opts);
        }

        private void DumpYotogiData(DumpOptions opts)
        {
            var source = new CompositeDataSource("Yotogi",
                new YotogiSkillDataSource(),
                new YotogiCommandDataSource());
            source.SetLoggerForAllSources(Logger);
            DumpDataSource(source, opts);
        }

        private void DumpTrophy(DumpOptions opts)
        {
            var source = new TrophyDataSource { Logger = Logger };
            DumpDataSource(source, opts);
        }


        private void DumpNPC(DumpOptions opts)
        {
            var source = new CompositeDataSource("NPC",
                new NPCDataSource(),
                new SubMaidDataSource());
            source.SetLoggerForAllSources(Logger);
            DumpDataSource(source, opts);
        }

        private void DumpGuest(DumpOptions opts)
        {
            var source = new CompositeDataSource("Guest",
                new GuestManDataSource(),
                new GuestPlayDataSource(),
                new GuestRoomDataSource());
            source.SetLoggerForAllSources(Logger);
            DumpDataSource(source, opts);
        }


        private void DumpDance(DumpOptions opts)
        {
            var source = new DanceDataSource { Logger = Logger };
            DumpDataSource(source, opts);
        }


        private void DumpMansion(DumpOptions opts)
        {
            var source = new CompositeDataSource("Mansion",
                new MansionRoomDataSource(),
                new MansionEventDataSource());
            source.SetLoggerForAllSources(Logger);
            DumpDataSource(source, opts);
        }


        private void DumpMemory(DumpOptions opts)
        {
            var source = new CompositeDataSource("Memory",
                new MemoryStoryDataSource(),
                new MemoryDailyDataSource(),
                new EmpireLifeModeDataSource());
            source.SetLoggerForAllSources(Logger);
            DumpDataSource(source, opts);
        }

        private string EscapeCSVItem(string str)
        {
            if (str.Contains("\n") || str.Contains("\"") || str.Contains(","))
                return $"\"{str.Replace("\"", "\"\"")}\"";
            return str;
        }

        private void Dump(DumpOptions opts)
        {
            Logger.LogInfo("Dumping game localisation files! Please be patient!");

            if (opts.dumpUITranslations)
                DumpUI();
            
            if (opts.dumpScripts)
                DumpScripts();

            if (opts.dumpItemNames)
                DumpItemNames(opts);

            if (opts.dumpMaidStatus)
                DumpMaidStatus(opts);

            if (opts.dumpYotogis)
                DumpYotogiData(opts);

            if (opts.dumpEvents)
            {
                DumpScenarioEvents(opts);
                DumpHoneyMoonEvents(opts);
                DumpPrivateModeEvents(opts);
                DumpMemory(opts);
            }

            if (opts.dumpSchedule)
                DumpSchedule(opts);

            if (opts.dumpTrophy)
                DumpTrophy(opts);

            if (options.dumpNPC)
                DumpNPC(opts);

            if (options.dumpGuest)
                DumpGuest(opts);

            if (options.dumpDance)
                DumpDance(opts);

            if (options.dumpMansion) 
                DumpMansion(opts);

            if (opts.dumpScripts)
                Logger.LogInfo($"Dumped {translatedLines} lines");
            Logger.LogInfo($"Done! Dumped translations are located in {TL_DIR}. You can now close the game!");
            Logger.LogInfo("IMPORTANT: Delete this plugin (TranslationExtract.dll) if you want to play the game normally!");
        }

        [Serializable]
        internal class SubtitleData
        {
            public int addDisplayTime;
            public int displayTime = -1;
            public bool isCasino;
            public string original = string.Empty;
            public int startTime;
            public string translation = string.Empty;
            public string voice = string.Empty;
        }

        private class DumpOptions
        {
            public bool dumpEvents;
            public bool dumpItemNames;
            public bool dumpMaidStatus;
            public bool dumpScripts = true;
            public bool dumpUITranslations = true;
            public bool dumpYotogis;
            public bool dumpTrophy;
            public bool dumpSchedule;
            public bool dumpNPC;
            public bool dumpGuest;
            public bool dumpDance;
            public bool dumpMansion;
            public bool skipTranslatedItems;
            public DumpOptions() { }

            public DumpOptions(DumpOptions other)
            {
                dumpScripts = other.dumpScripts;
                dumpUITranslations = other.dumpUITranslations;
                dumpItemNames = other.dumpItemNames;
                dumpSchedule = other.dumpSchedule;
                dumpYotogis = other.dumpYotogis;
                dumpMaidStatus = other.dumpMaidStatus;
                dumpEvents = other.dumpEvents;
                dumpTrophy = other.dumpTrophy;
                dumpNPC = other.dumpNPC;
                dumpGuest = other.dumpGuest;
                dumpDance = other.dumpDance;
                dumpMansion = other.dumpMansion;
                skipTranslatedItems = other.skipTranslatedItems;
            }
        }
    }
}
