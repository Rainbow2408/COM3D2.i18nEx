using System.Collections.Generic;
using BepInEx.Logging;

namespace TranslationExtract.DataSources
{
    /// <summary>
    /// 場景選擇資料源 - Scenario Events
    /// </summary>
    public class ScenarioEventDataSource : ApiDataSource<Schedule.ScenarioData>
    {
        public override string Name => "Scenario Events";

        protected override IList<Schedule.ScenarioData> FetchData()
        {
            Logger?.LogInfo("Getting scenario event data");
            return GameMain.Instance.ScenarioSelectMgr.GetAllScenarioData();
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(Schedule.ScenarioData data, int index, int total)
        {
            Logger?.LogDebug($"[{Name}] Progress [{index}/{total}] ID{data.ID}");

            // Title
            yield return new TranslationEntry
            {
                Term = data.TitleTerm,
                Desc = string.Empty,
                Original = data.Title
            };

            // Event Contents
            yield return new TranslationEntry
            {
                Term = data.EventContentTerm,
                Desc = string.Empty,
                Original = data.EventContents
            };

            // Condition texts
            if (data.ConditionText != null)
            {
                var conditionHash = new HashSet<string>();
                foreach (var text in data.ConditionText)
                {
                    if (conditionHash.Contains(text))
                        continue;

                    conditionHash.Add(text);
                    // Term logic from ScenarioData.ConditionTextTerms
                    yield return new TranslationEntry
                    {
                        Term = "SceneScenarioSelect/条件文/" + text,
                        Desc = string.Empty,
                        Original = text
                    };
                }
            }
        }
    }

    /// <summary>
    /// 蜜月事件資料源 - Honeymoon Events
    /// </summary>
    public class HoneymoonEventDataSource : ApiDataSource<Honeymoon.HoneymoonDatabase.Data>
    {
        private readonly HashSet<string> locationHash = new HashSet<string>();
        private readonly List<TranslationEntry> locationEntries = new List<TranslationEntry>();

        public override string Name => "Honeymoon Events";

        public override void Initialize()
        {
            Honeymoon.HoneymoonDatabase.CreateData();
        }

        protected override IList<Honeymoon.HoneymoonDatabase.Data> FetchData()
        {
            Logger?.LogInfo("Getting Honeymoon event data");
            return Honeymoon.HoneymoonDatabase.GetAllDatas(false);
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(Honeymoon.HoneymoonDatabase.Data data, int index, int total)
        {
            Logger?.LogDebug($"[{Name}] Progress [{index}/{total}] ID{data.id}");

            // Event name
            yield return new TranslationEntry
            {
                Term = data.eventNameTerm,
                Desc = string.Empty,
                Original = data.eventName
            };

            // Collect locations for later processing
            if (data.localtion != null && !locationHash.Contains(data.localtion.uniqueName))
            {
                locationHash.Add(data.localtion.uniqueName);
                locationEntries.Add(new TranslationEntry
                {
                    Term = data.localtion.drawNameTerm,
                    Desc = string.Empty,
                    Original = data.localtion.drawName
                });
            }
        }

        public override IEnumerable<TranslationEntry> GetEntries()
        {
            // First get all event entries
            foreach (var entry in base.GetEntries())
                yield return entry;

            // Then output unique location entries
            foreach (var entry in locationEntries)
                yield return entry;
        }
    }

    /// <summary>
    /// 私密模式事件資料源 - Private Mode Events
    /// </summary>
    public class PrivateModeEventDataSource : ApiDataSource<PrivateMaidMode.BGData>
    {
        private readonly HashSet<string> conditionHash = new HashSet<string>();
        private readonly HashSet<string> locationHash = new HashSet<string>();

        public override string Name => "Private Mode Events";

        public override void Initialize()
        {
            PrivateMaidMode.DataBase.CreateData();
        }

        protected override IList<PrivateMaidMode.BGData> FetchData()
        {
            Logger?.LogInfo("Getting Private mode event data");
            return PrivateMaidMode.DataBase.GetAllBGDatas();
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(PrivateMaidMode.BGData bg, int index, int total)
        {
            Logger?.LogDebug($"[{Name}] Progress [{index}/{total}] BG:{bg.uniqueName}");

            // Background name
            yield return new TranslationEntry
            {
                Term = bg.drawNameTerm,
                Desc = string.Empty,
                Original = bg.drawName
            };

            // Locations
            if (bg.locations != null)
            {
                foreach (var location in bg.locations)
                {
                    if (location != null && !locationHash.Contains(location.drawName))
                    {
                        locationHash.Add(location.drawName);
                        yield return new TranslationEntry
                        {
                            Term = location.drawNameTerm,
                            Desc = string.Empty,
                            Original = location.drawName
                        };
                    }
                }
            }

            // Events
            if (bg.events != null)
            {
                foreach (var evt in bg.events)
                {
                    if (evt?.eventPointList != null)
                    {
                        foreach (var pointData in evt.eventPointList)
                        {
                            if (pointData?.information != null)
                            {
                                foreach (var info in pointData.information)
                                {
                                    if (info != null)
                                    {
                                        // Event title
                                        yield return new TranslationEntry
                                        {
                                            Term = info.titleTerm,
                                            Desc = string.Empty,
                                            Original = info.title
                                        };

                                        // Conditions
                                        var condTerms = info.conditionTerms;
                                        for (int j = 0; j < info.conditions.Count; j++)
                                        {
                                            var cond = info.conditions[j];
                                            if (!string.IsNullOrEmpty(cond) && !conditionHash.Contains(cond))
                                            {
                                                conditionHash.Add(cond);
                                                yield return new TranslationEntry
                                                {
                                                    Term = condTerms[j],
                                                    Desc = string.Empty,
                                                    Original = cond
                                                };
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
