// TODO: Fix for multi-language support

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

namespace TranslationExtract
{
    [BepInPlugin("horse.coder.com3d2.tlextract", "Translation Extractor", PluginInfo.PLUGIN_VERSION)]
    public class TranslationExtract : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        public static readonly string TL_DIR = Path.Combine("BepinEx", PluginInfo.PLUGIN_NAME);

        internal static readonly string[] readonly_Datas = new string[8]
        {
                string.Empty, "Text", "Vanilla", "Personal", "YotogiClass", "JobClass", "Title", "Feature"
        };
        internal static string[] Datas = new string[5]
        {
                readonly_Datas[0], readonly_Datas[1], readonly_Datas[0], readonly_Datas[0], readonly_Datas[0]
        };

        private const int WIDTH = 200;
        private const int HEIGHT = 400;
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

                        GUILayout.Label("Advanced dumps");
                        Toggle(".menu item names", ref options.dumpItemNames);
                        Toggle("VIP event names", ref options.dumpVIPEvents);
                        Toggle("Yotogi skills", ref options.dumpYotogis);
                        Toggle("Maid stats", ref options.dumpPersonalies);
                        Toggle("Event names", ref options.dumpEvents);

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
            // original text is before <> tag and translated text is after
            // accept tags: [English,<e>], [TraditionalChinese,<tc>], [SimplifiedChinese,<sc>]
            // more information can be found at Scourt.Loc.LocalizationManager.ScriptTranslationMark
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
                else if (trimmedLine.StartsWith("@SubtitleDisplayForPlayVoice",
                                                StringComparison.InvariantCultureIgnoreCase))
                {
                    captureSubtitlePlay = true;
                    var sub = ParseTag(trimmedLine.Substring("@SubtitleDisplayForPlayVoice".Length));
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

        private void DumpScenarioEvents(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "Dynamic");
            using var swScenarioEvents = new CsvData();
            HashSet<string> ConditionHash = new();
            List<string> ConditionTexts = new();

            Logger.LogInfo("Getting scenario event data");
            var scenario_data_list_ = GameMain.Instance.ScenarioSelectMgr.GetAllScenarioData();

            int i = 1, i_total = scenario_data_list_.Length;
            foreach (var data in scenario_data_list_)
            {
                Logger.LogDebug($"[DumpScenarioEvents] Progress [{i}/{i_total}] ID{data.ID}");
                Datas[0] = data.TitleTerm;
                Datas[2] = readonly_Datas[0];
                Datas[3] = data.Title;
                Datas[4] = Datas[3];
                swScenarioEvents.Writer(unitPath, Datas, opts.skipTranslatedItems);
                Datas[0] = data.EventContentTerm;
                Datas[2] = readonly_Datas[0];
                Datas[3] = data.EventContents;
                Datas[4] = Datas[3];
                swScenarioEvents.Writer(unitPath, Datas, opts.skipTranslatedItems);
                foreach (var text in data.ConditionText)
                {
                    if (ConditionHash.Contains(text))
                        continue;
                    ConditionTexts.Add(text);
                    ConditionHash.Add(text);
                }
                i++;
            }
            foreach(var text in ConditionTexts)
            {
                //Term logic from ScenarioData.ConditionTextTerms
                Datas[0] = "SceneScenarioSelect/条件文/" + text;
                Datas[2] = readonly_Datas[0];
                Datas[3] = text;
                Datas[4] = Datas[3];
                swScenarioEvents.Writer(unitPath, Datas, opts.skipTranslatedItems);
            }
            swScenarioEvents.WriteCSV();
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


        private void DumpMaidStatus(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "Dynamic");
            using var swMaidStatus = new CsvData();

            Logger.LogInfo("Getting personality datas");
            //maid_status_personal_*.nei
            var personal_data_list_ = Personal.GetAllDatas(false);
            int i = 1, i_total = personal_data_list_.Count;
            foreach (var data in personal_data_list_)
            {
                Logger.LogDebug($"[DumpMaidStatus] Progress [{i}/{i_total}] Personal ID{data.id}");
                Datas[0] = data.termName;
                Datas[2] = readonly_Datas[3];
                Datas[3] = data.drawName;
                Datas[4] = Datas[3];
                swMaidStatus.Writer(unitPath, Datas, opts.skipTranslatedItems);
                i++;
            }
            //maid_status_yotogiclass_*.nei
            var yotogiclass_data_list_ = YotogiClass.GetAllDatas(false);
            i = 1;
            i_total = yotogiclass_data_list_.Count;
            foreach (var data in yotogiclass_data_list_)
            {
                Logger.LogDebug($"[DumpMaidStatus] Progress [{i}/{i_total}] YotogiClass ID{data.id}");
                Datas[0] = data.termName;
                Datas[2] = readonly_Datas[4];
                Datas[3] = data.drawName;
                Datas[4] = Datas[3];
                swMaidStatus.Writer(unitPath, Datas, opts.skipTranslatedItems);
                Datas[0] = data.termExplanatoryText;
                Datas[2] = readonly_Datas[4];
                Datas[3] = data.explanatoryText;
                Datas[4] = Datas[3];
                swMaidStatus.Writer(unitPath, Datas, opts.skipTranslatedItems);
                i++;
            }
            //maid_status_jobclass_*.nei
            var jobclass_data_list_ = JobClass.GetAllDatas(false);
            i = 1;
            i_total = jobclass_data_list_.Count;
            foreach (var data in jobclass_data_list_)
            {
                Logger.LogDebug($"[DumpMaidStatus] Progress [{i}/{i_total}] JobClass ID{data.id}");
                Datas[0] = data.termName;
                Datas[2] = readonly_Datas[5];
                Datas[3] = data.drawName;
                Datas[4] = Datas[3];
                swMaidStatus.Writer(unitPath, Datas, opts.skipTranslatedItems);
                Datas[0] = data.termExplanatoryText;
                Datas[2] = readonly_Datas[5];
                Datas[3] = data.explanatoryText;
                Datas[4] = Datas[3];
                swMaidStatus.Writer(unitPath, Datas, opts.skipTranslatedItems);
                i++;
            }
            //maid_status_title_list.nei
            Title.CreateData();
            var title_data_list_ = Traverse.CreateWithType("MaidStatus.Title").Field("checkAchivementList").GetValue<List<KeyValuePair<string, ParametersPack>>>();
            i = 1;
            i_total = title_data_list_.Count;
            foreach (var data in title_data_list_)
            {
                //Term logic from MaidStatus.Status.conditionTermText
                Logger.LogDebug($"[DumpMaidStatus] Progress [{i}/{i_total}] Title");
                Datas[0] = "MaidStatus/ステータス称号/" + data.Key;
                Datas[2] = readonly_Datas[6];
                Datas[3] = data.Key;
                Datas[4] = Datas[3];
                swMaidStatus.Writer(unitPath, Datas, opts.skipTranslatedItems);
                i++;
            }
            //maid_status_feature_*.nei
            var feature_data_list_ = Feature.GetAllDatas(false);
            i = 1;
            i_total = feature_data_list_.Count;
            foreach (var data in feature_data_list_)
            {
                Logger.LogDebug($"[DumpMaidStatus] Progress [{i}/{i_total}] Feature ID{data.id}");
                Datas[0] = data.termName;
                Datas[2] = readonly_Datas[7];
                Datas[3] = data.drawName;
                Datas[4] = Datas[3];
                swMaidStatus.Writer(unitPath, Datas, opts.skipTranslatedItems);
                i++;
            }
            swMaidStatus.WriteCSV();
        }

        private void DumpYotogiData(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "Dynamic");
            using var swName = new CsvData();
            using var swCommand = new CsvData();

            Logger.LogInfo("Getting yotogi skills and commands");
            var skill_data_id_list_ = Skill.skill_data_list;

            using AFileBase afileBase = GameUty.FileSystem.FileOpen("yotogi_skill_command_data.nei");
            using CsvParser csvParser = new();
            csvParser.Open(afileBase);
            using AFileBase afileBase2 = GameUty.FileSystem.FileOpen("yotogi_skill_command_status.nei");
            using CsvParser csvParser2 = new();
            csvParser2.Open(afileBase2);

            int num = 0, num2 = 0, num3 = 0, i = 1, i_total = skill_data_id_list_.Length;
            Skill.Data.Command.Data.Basic data;
            foreach (var sk0 in skill_data_id_list_)
            {
                int j = 1, j_total = sk0.Values.Count;
                foreach (var sk1 in sk0.Values)
                {
                    Logger.LogDebug($"[DumpYotogiSkills] Progress [{i}/{i_total}][{j}/{j_total}] ID{sk1.id}");

                    Datas[0] = sk1.termName;
                    Datas[2] = readonly_Datas[0];
                    Datas[3] = sk1.name;
                    Datas[4] = Datas[3];
                    swName.Writer(unitPath, Datas, opts.skipTranslatedItems);

                    //Get data method logic from Skill.Data.Command(sk1, csvParser, csvParser2);
                    num = sk1.command_basic_cell_x + 1;
                    num2 = sk1.command_basic_cell_y + 1;
                    num3 = 0;
                    while (csvParser.IsCellToExistData(num + 1, num2 + num3))
                        num3++;
                    if (!(0 < num3))
                    {
                        Logger.LogError(csvParser.GetCellAsString(num - 1, num2 - 1) + "のコマンド設定は不正です");
                        continue;
                    }
                    for (int k = 0; k < num3; k++)
                    {
                        data = new Skill.Data.Command.Data.Basic(sk1, csvParser, sk1.command_basic_cell_x + 1, sk1.command_basic_cell_y + k + 1);
                        Datas[0] = data.termName;
                        Datas[2] = readonly_Datas[0];
                        Datas[3] = data.name;
                        Datas[4] = Datas[3];
                        swCommand.Writer(unitPath, Datas, opts.skipTranslatedItems);
                        data = null;
                    }
                    GC.Collect();
                    j++;
                }
                i++;
            }
            swName.WriteCSV();
            swCommand.WriteCSV();
        }

        private void DumpVIPEvents(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "Dynamic");
            using var swYotogi = new CsvData();

            Logger.LogInfo("Getting VIP event names");
            var schedule_data_list_ = ScheduleCSVData.YotogiData;

            int i = 1, i_total = schedule_data_list_.Count;
            foreach (var sd0 in schedule_data_list_)
            {
                var sd1 = sd0.Value;
                if (sd1.yotogiType == ScheduleCSVData.YotogiType.Vip)
                {
                    Logger.LogDebug($"[DumpVIPEvents] Progress [{i}/{i_total}] ScheduleCSVData.YotogiType.Vip ID{sd1.id}");
                    //Term logic from FreeModeItemVip.titleTerm and textTerm;
                    var type = sd1.yotogiType.ToString();
                    Datas[0] = "SceneDaily/スケジュール/項目/" + sd1.name.Replace("×", "_");
                    Datas[2] = readonly_Datas[0];
                    Datas[3] = sd1.name;
                    Datas[4] = Datas[3];
                    swYotogi.Writer(unitPath, Datas, opts.skipTranslatedItems);
                    Datas[0] = "SceneDaily/スケジュール/説明/" + sd1.information.Replace("×", "_");
                    Datas[2] = readonly_Datas[0];
                    Datas[3] = sd1.information;
                    Datas[4] = Datas[3];
                    swYotogi.Writer(unitPath, Datas, opts.skipTranslatedItems);
                }
                else Logger.LogDebug($"[DumpVIPEvents] Skip [{i}/{i_total}] ScheduleCSVData.YotogiType.{sd1.yotogiType} ID{sd1.id}");
                i++;
            }
            swYotogi.WriteCSV();
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

            if (opts.dumpEvents)
                DumpScenarioEvents(opts);

            if (opts.dumpPersonalies)
                DumpMaidStatus(opts);

            if (opts.dumpYotogis)
                DumpYotogiData(opts);

            if (opts.dumpVIPEvents)
                DumpVIPEvents(opts);

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
            public bool dumpPersonalies;
            public bool dumpScripts = true;
            public bool dumpUITranslations = true;
            public bool dumpVIPEvents;
            public bool dumpYotogis;
            public bool skipTranslatedItems;
            public DumpOptions() { }

            public DumpOptions(DumpOptions other)
            {
                dumpScripts = other.dumpScripts;
                dumpUITranslations = other.dumpUITranslations;
                dumpItemNames = other.dumpItemNames;
                dumpVIPEvents = other.dumpVIPEvents;
                dumpYotogis = other.dumpYotogis;
                dumpPersonalies = other.dumpPersonalies;
                dumpEvents = other.dumpEvents;
                skipTranslatedItems = other.skipTranslatedItems;
            }
        }
    }
}
