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
using I2.Loc;
using UnityEngine;

namespace TranslationExtract
{
    internal static class Extensions
    {
        private static string EscapeCSVItem(string str)
        {
            if (str.Contains("\n") || str.Contains("\"") || str.Contains(","))
                return $"\"{str.Replace("\"", "\"\"")}\"";
            return str;
        }

        private static IEnumerable<(T1, T2)> ZipWith<T1, T2>(this IEnumerable<T1> e1, IEnumerable<T2> e2)
        {
            if (e1 == null || e2 == null)
                yield break;
            using var enum1 = e1.GetEnumerator();
            using var enum2 = e2.GetEnumerator();

            while (enum1.MoveNext() && enum2.MoveNext())
                yield return (enum1.Current, enum2.Current);
        }

        public static bool ContainsJapaneseCharacters(this string input)
        {
			// Define the ranges for Japanese characters
            var japaneseRanges = @"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]";

            // Combine all ranges and exclude non-letter symbols and grammatical marks
            var regex = new Regex(japaneseRanges);

            return regex.IsMatch(input);
		}

		public static void WriteCSV<T>(this StreamWriter sw,
                                       string neiFile,
                                       string csvFile,
                                       Func<CsvParser, int, T> selector,
                                       Func<T, IEnumerable<string>> toString,
                                       Func<T, IEnumerable<string>> toTranslation,
                                       bool skipIfExists = false)
        {

            List<string> cleanPrefixesCache = new List<string>();
            using var f = GameUty.FileOpen(neiFile);
            using var scenarioNei = new CsvParser();
            scenarioNei.Open(f);

            for (var i = 1; i < scenarioNei.max_cell_y; i++)
            {
                if (!scenarioNei.IsCellToExistData(0, i))
                    continue;

                var item = selector(scenarioNei, i);
                var prefixes = toString(item);
                var translations = toTranslation(item);
                
                foreach (var (prefix, tl) in prefixes.ZipWith(translations))
                {
                    if (skipIfExists && ((LocalizationManager.TryGetTranslation($"{csvFile}/{prefix}", out var _)) ||
                        !tl.ContainsJapaneseCharacters() ))
                        continue;

                    if (string.IsNullOrEmpty(tl))
                        continue;

					var csvName = EscapeCSVItem(tl);
                    if (!csvName.StartsWith("\""))
                        csvName = $"\"{csvName}\"";
                    
                    var cleanPrefix = prefix.Replace("×", "_");

                    //Avoid duplicated entries
                    if (!cleanPrefixesCache.Contains(cleanPrefix))
                    {
                        sw.WriteLine($"\"{cleanPrefix}\",Text,,{csvName},");
                        cleanPrefixesCache.Add(cleanPrefix);
                    }
                }
            }
        }
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class TranslationExtract : BaseUnityPlugin
    {
        public const string TL_DIR = "COM3D2_Localisation";
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
                        Toggle("Schedule Events", ref options.dumpVIPEvents);
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
            Debug.Log("Dumping UI localisation");

            var langs = LocalizationManager.GetAllLanguages();
            Debug.Log($"Currently {langs.Count} languages are known");
            foreach (var language in langs)
                Debug.Log($"{language}");

            Debug.Log($"Currently selected language is {LocalizationManager.CurrentLanguage}");
            Debug.Log($"There are {LocalizationManager.Sources.Count} language sources");

            foreach (var languageSource in LocalizationManager.Sources)
            {
                Debug.Log(
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
                        Debug.Log($"[WARNING] Failed to extract line from \"{trimmedLine}\"");
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
            Debug.Log("Dumping game script translations...");
            Debug.Log("Getting all script files...");
            var scripts = GameUty.FileSystem.GetFileListAtExtension(".ks");
            Debug.Log($"Found {scripts.Length} scripts!");

            foreach (var scriptFile in scripts)
            {
                using var f = GameUty.FileOpen(scriptFile);
                var script = NUty.SjisToUnicode(f.ReadAll());
                Debug.Log(scriptFile);
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
            var unitPath = Path.Combine(i2Path, "zzz_scenario_events");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting scenario event data");

            var encoding = new UTF8Encoding(true);
            using var sw = new StreamWriter(Path.Combine(unitPath, "SceneScenarioSelect.csv"), false, encoding);

            sw.WriteLine("Key,Type,Desc,Japanese,English");
            sw.WriteCSV("select_scenario_data.nei", "SceneScenarioSelect",
                        (parser, i) => new
                        {
                            id = parser.GetCellAsInteger(0, i),
                            name = parser.GetCellAsString(1, i),
                            description = parser.GetCellAsString(2, i)
                        },
                        arg => new[] { $"{arg.id}/タイトル", $"{arg.id}/内容" },
                        arg => new[] { arg.name, arg.description },
                        opts.skipTranslatedItems);

            sw.Close();

            using var sw2 = new StreamWriter(Path.Combine(unitPath, "SceneScenarioSelect.csv"), true, encoding);
            sw2.WriteCSV("select_scenario_data.nei", "SceneScenarioSelect",
                        (parser, i) => new
                        {
                            condition = parser.GetCellAsString(22, i),
                        },
                        arg =>
                        {
                            var conditions = arg.condition.Split('\n');

                            return conditions.Select(c => $"条件文/{c}").ToArray();
                        },
                        arg =>
                        {
                            var conditions = arg.condition.Split('\n');

                            return conditions;
                        },
                        opts.skipTranslatedItems);
        }

        private void DumpHoneyMoonEvents(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_honeymoon_events");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting Honeymoon event data");

            var encoding = new UTF8Encoding(true);
            using var sw = new StreamWriter(Path.Combine(unitPath, "SceneHoneymoonMode.csv"), false, encoding);

            sw.WriteLine("Key,Type,Desc,Japanese,English");
            sw.WriteCSV("honeymoonmode_event_list.nei", "SceneHoneymoonMode",
                        (parser, i) => new
                        {
                            locationName = parser.GetCellAsString(1, i)
                        },
                        arg => new[] { $"場所名/{arg.locationName}" },
                        arg => new[] { arg.locationName },
                        opts.skipTranslatedItems);

            sw.WriteCSV("honeymoonmode_event_list.nei", "SceneHoneymoonMode",
                        (parser, i) => new
                        {
                            id = parser.GetCellAsString(0, i),
                            eventName = parser.GetCellAsString(4, i)
                        },
                        arg => new[] { $"イベント名/{arg.id}" },
                        arg => new[] { arg.eventName },
                        opts.skipTranslatedItems);

        }

        private void DumpPrivateModeEvents(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_privatemode_events");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting Private mode event data");

            var encoding = new UTF8Encoding(true);
            using var sw = new StreamWriter(Path.Combine(unitPath, "ScenePrivate.csv"), false, encoding);

            sw.WriteLine("Key,Type,Desc,Japanese,English");
            sw.WriteCSV("private_maidmode_eventinformation_list.nei", "ScenePrivate",
                        (parser, i) => new
                        {
                            id = parser.GetCellAsInteger(0, i),
                            eventName = parser.GetCellAsString(1, i)
                        },
                        arg => new[] { $"イベントタイトル/{arg.id}" },
                        arg => new[] { arg.eventName },
                        opts.skipTranslatedItems);

            sw.WriteCSV("private_maidmode_eventinformation_list.nei", "ScenePrivate",
                        (parser, i) => new
                        {
                            eventCondition = parser.GetCellAsString(2, i)
                        },
                        arg => new[] { $"イベントテキスト/{arg.eventCondition}" },
                        arg => new[] { arg.eventCondition },
                        opts.skipTranslatedItems);

            sw.WriteCSV("private_maidmode_group_list.nei", "ScenePrivate",
                        (parser, i) => new
                        {
                            eventLocation = parser.GetCellAsString(2, i)
                        },
                        arg => new[] { $"ロケーション名/{arg.eventLocation}" },
                        arg => new[] { arg.eventLocation },
                        opts.skipTranslatedItems);

            sw.WriteCSV("private_maidmode_group_list.nei", "ScenePrivate",
                        (parser, i) => new
                        {
                            eventBackgroung = parser.GetCellAsString(1, i)
                        },
                        arg => new[] { $"背景/{arg.eventBackgroung}" },
                        arg => new[] { arg.eventBackgroung },
                        opts.skipTranslatedItems);
        }

        private void DumpSchedule(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_schedule");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting schedule.");


            //specialKeyColumn for when we need another colum used in as the key
            void WriteSimpleData(string file, IList<(int, string)> columnIndexName, StreamWriter sw, int specialKeyColumn = -1)
            {
				sw.WriteCSV(file, "SceneDaily",
                    (parser, i) =>
                    {

                        var columnText = columnIndexName
                                         .Select(column => (column.Item2, parser.GetCellAsString(column.Item1, i)))
                                         .ToList();


                        if(specialKeyColumn != -1)
                        {
                            columnText = columnIndexName
                                             .Select(column => ($"{column.Item2}{parser.GetCellAsString(specialKeyColumn, i)}", parser.GetCellAsString(column.Item1, i)))
                                             .ToList();
                        }


                        var translationData = new
                        {
                            id = parser.GetCellAsInteger(0, i),
                            columnText = columnText
					    };

                        return translationData;
                    },
                    arg =>
                    {
                        var keys = new string[arg.columnText.Count];
                        var index = 0;
                        foreach (var columnTranslation in arg.columnText)
                        {
                            if (specialKeyColumn != -1)
                                keys[index++] = columnTranslation.Item1;
                            else
                                keys[index++] = columnTranslation.Item1 + columnTranslation.Item2;
                        }
                        return keys;
                    },
                    arg => arg.columnText.Select(r => r.Item2), 
                    opts.skipTranslatedItems);
			}



            var encoding = new UTF8Encoding(true);
            using (var sw = new StreamWriter(Path.Combine(unitPath, "SceneDaily.csv"), false, encoding))
            {
                sw.WriteLine("Key,Type,Desc,Japanese,English");

                //For translation reasons conditions will be extracted after Names and descriptions
                WriteSimpleData("schedule_work_night.nei", new[]
                {
                    //Schedule Titles
                    (1, "スケジュール/項目/"),

                }, sw);

                //This part needs a special key
                WriteSimpleData("schedule_work_night.nei", new[]
                {
                    //Schedule descriptions
                    (7, $"スケジュール/説明/"),

                }, sw, 1);

                WriteSimpleData("schedule_work_night.nei", new[]
                {
                    //Schedule conditions
                    (12, "スケジュール/条件文/"),
                    (13, "スケジュール/条件文/"),
                    (14, "スケジュール/条件文/"),
                    (15, "スケジュール/条件文/"),
                    (16, "スケジュール/条件文/"),
                    (17, "スケジュール/条件文/"),
                    (18, "スケジュール/条件文/"),
                    (19, "スケジュール/条件文/"),
                    (20, "スケジュール/条件文/"),
                    //(24,"実行条件：メイドクラス（取得している）"),
                    //(25,"実行条件：所持性癖")

                }, sw);


                WriteSimpleData("schedule_work_noon.nei", new[]
                {
                    //Schedule Training
                    (1, "スケジュール/項目/")

                }, sw);

                WriteSimpleData("schedule_work_easyyotogi.nei", new[]
{
                    //Schedule Easy yotogi Titles (training books)
                    (1, "スケジュール/項目/")

                }, sw);

                WriteSimpleData("schedule_work_easyyotogi.nei", new[]
{
                    //Schedule Easy yotogi Descriptions (training books)
                    (5, "スケジュール/説明/")

                }, sw, 1);

                WriteSimpleData("schedule_work_night_category_list.nei", new[]
                {
                    //Schedule Categories
                    (1, "スケジュール/カテゴリー/")

                }, sw);
            }
        }

        //Old Schedule method
        private void DumpVIPEvents(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_vip_event");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting VIP event names");

            var encoding = new UTF8Encoding(true);
            using var sw = new StreamWriter(Path.Combine(unitPath, "SceneDaily.csv"), false, encoding);

            sw.WriteLine("Key,Type,Desc,Japanese,English");
            sw.WriteCSV("schedule_work_night.nei", "SceneDaily", (parser, i) => new
            {
                vipName = parser.GetCellAsString(1, i),
                vipDescription = parser.GetCellAsString(7, i)
            },
                        arg => new[] { $"スケジュール/項目/{arg.vipName}", $"スケジュール/説明/{arg.vipDescription}" },
                        arg => new[] { arg.vipName, arg.vipDescription },
                        opts.skipTranslatedItems);
        }

        private void DumpItemNames(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_item_names");
            Directory.CreateDirectory(unitPath);

            var encoding = new UTF8Encoding(true);
            Debug.Log("Getting all .menu files (this might take a moment)...");
            var menus = GameUty.FileSystem.GetFileListAtExtension(".menu");

            Debug.Log($"Found {menus.Length} menus!");

            var swDict = new Dictionary<string, StreamWriter>();

            foreach (var menu in menus)
            {
                using var f = GameUty.FileOpen(menu);
                using var br = new BinaryReader(new MemoryStream(f.ReadAll()));
                Debug.Log(menu);

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
            var unitPath = Path.Combine(i2Path, "zzz_maid_status");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting Maid's Status");

            void WriteSimpleData(string file, string prefix, StreamWriter sw, int dataCol = 2, int idCol = 1)
            {
                sw.WriteCSV(file, "MaidStatus", (parser, i) => new
                            {
                                uniqueName = parser.GetCellAsString(idCol, i),
                                displayName = parser.GetCellAsString(dataCol, i)
                            },
                            arg => new[] { $"{prefix}/{arg.uniqueName}" },
                            arg => new[] { arg.displayName },
                            opts.skipTranslatedItems);
            }

            var encoding = new UTF8Encoding(true);
            using (var sw = new StreamWriter(Path.Combine(unitPath, "MaidStatus.csv"), false, encoding))
            {
                sw.WriteLine("Key,Type,Desc,Japanese,English");

                WriteSimpleData("maid_status_personal_list.nei", "性格タイプ", sw);

                WriteSimpleData("maid_status_yotogiclass_list.nei", "夜伽クラス", sw);
                WriteSimpleData("maid_status_yotogiclass_list.nei", "夜伽クラス", sw);

                WriteSimpleData("maid_status_jobclass_list.nei", "ジョブクラス", sw);
                WriteSimpleData("maid_status_jobclass_list.nei", "ジョブクラス/説明", sw, 4);

                WriteSimpleData("maid_status_title_list.nei", "ステータス称号", sw, 0, 0);

                WriteSimpleData("maid_status_feature_list.nei", "特徴タイプ", sw, 1);
            }
        }

        private void DumpYotogiData(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_yotogi");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting yotogi skills and commands");

            var encoding = new UTF8Encoding(true);

            //Yotogi Skills
            using (var sw = new StreamWriter(Path.Combine(unitPath, "YotogiSkillName.csv"), false, encoding))
            {
                sw.WriteLine("Key,Type,Desc,Japanese,English");
                sw.WriteCSV("yotogi_skill_list.nei", "YotogiSkillName",
                            (parser, i) => new
                            {
                                skillName = parser.GetCellAsString(4, i)
                            },
                            arg => new[] { arg.skillName },
                            arg => new[] { arg.skillName },
                            opts.skipTranslatedItems);
            }

            //Yotogi Commands
            var commandNames = new HashSet<string>();
            using (var sw = new StreamWriter(Path.Combine(unitPath, "YotogiSkillCommand.csv"), false, encoding))
            {
                using var f = GameUty.FileOpen("yotogi_skill_command_data.nei");
                using var scenarioNei = new CsvParser();
                sw.WriteLine("Key,Type,Desc,Japanese,English");
                scenarioNei.Open(f);

                for (var i = 0; i < scenarioNei.max_cell_y; i++)
                {
                    if (!scenarioNei.IsCellToExistData(2, i))
                    {
                        i += 2;
                        continue;
                    }

                    var commandName = scenarioNei.GetCellAsString(2, i);

                    if (opts.skipTranslatedItems &&
                        LocalizationManager.TryGetTranslation($"YotogiSkillCommand/{commandName}", out var _))
                        continue;
                    

                    if (commandNames.Contains(commandName))
                        continue;

                    commandNames.Add(commandName);

                    var csvName = EscapeCSVItem(commandName);
                    sw.WriteLine($"{csvName},Text,,{csvName},");
                }
            }

            //Yotogi rooms names
            using (var sw = new StreamWriter(Path.Combine(unitPath, "SceneYotogi.csv"), false, encoding))
            {
                sw.WriteLine("Key,Type,Desc,Japanese,English");
                sw.WriteCSV("yotogi_stage_list.nei", "SceneYotogi",
                            (parser, i) => new
                            {
                                stageName = parser.GetCellAsString(2, i)
                            },
                            arg => new[] { $"背景タイプ/{arg.stageName}" },
                            arg => new[] { arg.stageName },
                            opts.skipTranslatedItems);
            }
        }        

        private void DumpTrophy(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_trophy");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting Trophy data");

            var encoding = new UTF8Encoding(true);
            using var sw = new StreamWriter(Path.Combine(unitPath, "SceneTrophy.csv"), false, encoding);

            sw.WriteLine("Key,Type,Desc,Japanese,English");
            sw.WriteCSV("trophy_list.nei", "SceneTrophy",
                        (parser, i) => new
                        {
                            id = parser.GetCellAsInteger(0, i),
                            name = parser.GetCellAsString(2, i),
                            description = parser.GetCellAsString(8, i)
                        },
                        arg => new[] { $"{arg.id}/トロフィー名", $"{arg.id}/説明" },
                        arg => new[] { arg.name, arg.description },
                        opts.skipTranslatedItems);
        }

        private void DumpNPC(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_npc_edit");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting NPC data");

            var encoding = new UTF8Encoding(true);

            //NPC Maids
            using var sw = new StreamWriter(Path.Combine(unitPath, "SceneNPCEdit.csv"), false, encoding);

            sw.WriteLine("Key,Type,Desc,Japanese,English");
            sw.WriteCSV("npcedit_list.nei", "SceneNPCEdit",
                        (parser, i) => new
                        {
                            id = parser.GetCellAsInteger(0, i),
                            name = parser.GetCellAsString(1, i),
                            description = parser.GetCellAsString(6, i)
                        },
                        arg => new[] { $"{arg.id}/苗字", $"{arg.id}/名前", $"{arg.id}/説明" },
                        arg => new[] { arg.name, arg.name, arg.description },
                        opts.skipTranslatedItems);



            //Extra Maids
            using var sw2 = new StreamWriter(Path.Combine(unitPath, "SubMaid.csv"), false, encoding);

            sw2.WriteLine("Key,Type,Desc,Japanese,English");
            sw2.WriteCSV("maid_status_submaid_list.nei", "SubMaid",
                        (parser, i) => new
                        {

                            name = parser.GetCellAsString(1, i),
                            char_type = parser.GetCellAsString(11, i),
                            stat_stype = parser.GetCellAsString(12, i)
                        },
                        arg => new[] { $"{arg.name}/性格", $"{arg.name}/状態" },
                        arg => new[] { arg.char_type, arg.stat_stype },
                        opts.skipTranslatedItems);
        }

        private void DumpGuest(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_guest_mode");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting NPC data");

            var encoding = new UTF8Encoding(true);

            //Guests profiles
            using var sw = new StreamWriter(Path.Combine(unitPath, "SceneKasizukiMainMenu.csv"), false, encoding);

            sw.WriteLine("Key,Type,Desc,Japanese,English");
            sw.WriteCSV("kasizuki_man_list.nei", "SceneKasizukiMainMenu",
                        (parser, i) => new
                        {
                            name = parser.GetCellAsString(1, i),
                            displayedName = parser.GetCellAsString(2,i),
                            profile = parser.GetCellAsString(4, i),
                            prefered_play = parser.GetCellAsString(5, i)
                        },
                        arg => new[] { $"男名/{arg.name}", $"男プロフ/{arg.name}", $"男好プレイ/{arg.name}" },
                        arg => new[] { arg.displayedName, arg.profile, arg.prefered_play },
                        opts.skipTranslatedItems);

            sw.Dispose();

            //guest mode Rooms
            using var sw2 = new StreamWriter(Path.Combine(unitPath, "SceneKasizukiMainMenu.csv"), true, encoding);
            sw2.WriteLine("----------------ROOMS----------------,,,,");
            sw2.WriteCSV("kasizuki_room_list.nei", "SceneKasizukiMainMenu",
                        (parser, i) => new
                        {
                            //roomName = parser.GetCellAsString(4, i),
                            roomDisplayedName = parser.GetCellAsString(5, i),
                            roomDescription = parser.GetCellAsString(7, i)
                        },
                        arg => new[] { $"部屋名/{arg.roomDisplayedName}", $"部屋説明/{arg.roomDisplayedName}" },
                        arg => new[] { arg.roomDisplayedName, arg.roomDescription },
                        opts.skipTranslatedItems);

            //guest mode Scenarios
            sw2.WriteLine("----------------SCENARIOS----------------,,,,");
            sw2.WriteCSV("kasizuki_play_list.nei", "SceneKasizukiMainMenu",
                        (parser, i) => new
                        {
                            id = parser.GetCellAsString(0, i),
                            scenarioTitle = parser.GetCellAsString(3, i),
                            scenarioDescription = parser.GetCellAsString(4, i)
                        },
                        arg => new[] { $"プレイタイトル/{arg.id}", $"プレイ内容/{arg.id}" },
                        arg => new[] { arg.scenarioTitle, arg.scenarioDescription },
                        opts.skipTranslatedItems);

            //guest mode Scenarios conditions
            sw2.WriteLine("----------------CONDITIONS----------------,,,,");
            sw2.WriteCSV("kasizuki_play_list.nei", "SceneKasizukiMainMenu",
            (parser, i) => new
            {
                condition1 = parser.GetCellAsString(5, i),
                condition2 = parser.GetCellAsString(6, i),
                condition3 = parser.GetCellAsString(7, i),
                condition4 = parser.GetCellAsString(8, i),
                condition5 = parser.GetCellAsString(9, i),
                condition6 = parser.GetCellAsString(10, i)
            },
            arg => new[] { $"プレイ条件/{arg.condition1}", $"プレイ条件/{arg.condition2}", $"プレイ条件/{arg.condition3}", $"プレイ条件/{arg.condition4}", $"プレイ条件/{arg.condition5}", $"プレイ条件/{arg.condition6}"},
            arg => new[] { arg.condition1, arg.condition2, arg.condition3, arg.condition4, arg.condition5, arg.condition6, },
            opts.skipTranslatedItems);
        }

        private void DumpDance(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_dance");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting Dance data");

            var encoding = new UTF8Encoding(true);
            using var sw = new StreamWriter(Path.Combine(unitPath, "SceneDanceSelect.csv"), false, encoding);

            sw.WriteLine("Key,Type,Desc,Japanese,English");
            sw.WriteCSV("dance_setting.nei", "SceneDanceSelect",
                        (parser, i) => new
                        {
                            id = parser.GetCellAsInteger(0, i),
                            danceTitle = parser.GetCellAsString(1, i),
                            danceDescription = parser.GetCellAsString(6, i)
                        },
                        arg => new[] { $"曲名/{arg.id}", $"曲説明/{arg.id}" },
                        arg => new[] { arg.danceTitle, arg.danceDescription },
                        opts.skipTranslatedItems);
        }

        private void DumpMansion(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_mansion_dlc");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting mansion mode data");

            var encoding = new UTF8Encoding(true);
            using var sw = new StreamWriter(Path.Combine(unitPath, "SceneTeikokusou.csv"), false, encoding);

            sw.WriteLine("Key,Type,Desc,Japanese,English");
            sw.WriteCSV("teikokusoumode_playmode_list.nei", "SceneTeikokusou",
                        (parser, i) => new
                        {
                            id = parser.GetCellAsInteger(0, i),
                            roomName = parser.GetCellAsString(1, i),
                            guestName = parser.GetCellAsString(2, i),
                            roomDescription = parser.GetCellAsString(9, i)
                        },
                        arg => new[] { $"部屋名/{arg.id}", $"部屋ゲスト名/{arg.id}", $"部屋プロフィールコメント/{arg.id}" },
                        arg => new[] { arg.roomName, arg.guestName, arg.roomDescription },
                        opts.skipTranslatedItems);
        }

        private void DumpMemory(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_memory");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting Memory data");

            var encoding = new UTF8Encoding(true);
            using var sw = new StreamWriter(Path.Combine(unitPath, "SceneFreeModeSelect.csv"), false, encoding);

            sw.WriteLine("Key,Type,Desc,Japanese,English");

            //Story
            sw.WriteCSV("recollection_story.nei", "SceneFreeModeSelect",
                        (parser, i) => new
                        {
                            storyTitle = parser.GetCellAsString(1, i),
                            storyDescription = parser.GetCellAsString (5, i),
                        },
                        arg => new[] { $"タイトル/{arg.storyTitle}", $"説明/{arg.storyTitle}" },
                        arg => new[] { arg.storyTitle, arg.storyDescription },
                        opts.skipTranslatedItems);

            //Daily Events
            sw.WriteCSV("recollection_normal2.nei", "SceneFreeModeSelect",
                        (parser, i) => new
                        {
                            eventTitle = parser.GetCellAsString(1, i),
                            eventDescription = parser.GetCellAsString(5, i),
                        },
                        arg => new[] { $"タイトル/{arg.eventTitle}", $"説明/{arg.eventTitle}" },
                        arg => new[] { arg.eventTitle, arg.eventDescription },
                        opts.skipTranslatedItems);

            //Requirements
            sw.WriteCSV("recollection_story.nei", "SceneFreeModeSelect",
                       (parser, i) => new
                       {
                           storyConditions = parser.GetCellAsString(6, i)
                       },
                       arg => new[] { $"条件文/{arg.storyConditions}" },
                       arg => new[] { arg.storyConditions },
                       opts.skipTranslatedItems);

            sw.WriteCSV("recollection_normal2.nei", "SceneFreeModeSelect",
                       (parser, i) => new
                       {
                           eventConditions = parser.GetCellAsString(6, i)
                       },
                       arg => new[] { $"条件文/{arg.eventConditions}" },
                       arg => new[] { arg.eventConditions },
                       opts.skipTranslatedItems);

            //Empire Life Mode
            sw.WriteCSV("recollection_life_mode.nei", "SceneFreeModeSelect",
                       (parser, i) => new
                       {
                           lifeTitle = parser.GetCellAsString(2, i),
                           lifeDescription = parser.GetCellAsString(4, i)
                       },
                       arg => new[] { $"タイトル/{arg.lifeTitle}", $"説明/{arg.lifeDescription}" },
                       arg => new[] { arg.lifeTitle, arg.lifeDescription },
                       opts.skipTranslatedItems);


            sw.WriteCSV("recollection_life_mode.nei", "SceneFreeModeSelect",
                       (parser, i) => new
                       {
                           lifeConditions1 = parser.GetCellAsString(5, i),
                           lifeConditions2 = parser.GetCellAsString(6, i),
                           lifeConditions3 = parser.GetCellAsString(7, i),
                       },
                       arg => new[] { $"条件文/{arg.lifeConditions1}", $"条件文/{arg.lifeConditions2}", $"条件文/{arg.lifeConditions3}" },
                       arg => new[] { arg.lifeConditions1, arg.lifeConditions2, arg.lifeConditions3 },
                       opts.skipTranslatedItems);
        }

        private string EscapeCSVItem(string str)
        {
            if (str.Contains("\n") || str.Contains("\"") || str.Contains(","))
                return $"\"{str.Replace("\"", "\"\"")}\"";
            return str;
        }

        private void Dump(DumpOptions opts)
        {
            Debug.Log("Dumping game localisation files! Please be patient!");

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

            if (opts.dumpVIPEvents)
            {
                DumpSchedule(opts);
                //DumpVIPEvents(opts); //Old Method
            }

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
                Debug.Log($"Dumped {translatedLines} lines");
            Debug.Log($"Done! Dumped translations are located in {TL_DIR}. You can now close the game!");
            Debug.Log("IMPORTANT: Delete this plugin (TranslationExtract.dll) if you want to play the game normally!");
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
            public bool dumpVIPEvents;
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
                dumpVIPEvents = other.dumpVIPEvents;
                dumpYotogis = other.dumpYotogis;
                dumpMaidStatus = other.dumpMaidStatus;
                dumpEvents = other.dumpEvents;
                dumpTrophy = other.dumpTrophy;
                dumpSchedule = other.dumpSchedule;
                dumpNPC = other.dumpNPC;
                dumpGuest = other.dumpGuest;
                dumpDance = other.dumpDance;
                dumpMansion = other.dumpMansion;
                skipTranslatedItems = other.skipTranslatedItems;
            }
        }
    }
}
