using System.Collections.Generic;
using Teikokusou;

namespace COM3D2.i18nEx.Core.TranslationExtract.DataSources
{
    /// <summary>
    /// 回憶模式 Story 事件資料源 - Memory Story Events
    /// </summary>
    public class MemoryStoryDataSource : ITranslationDataSource
    {
        private readonly HashSet<string> conditionHash = new();
        private readonly HashSet<string> titleHash = new();

        public string Name => "Memory Story Events";

        public void Initialize()
        {
            // No initialization needed
        }

        public IEnumerable<TranslationEntry> GetEntries()
        {
            Core.Logger.LogInfo("Processing Story events...");
            var storyList = FreeModeItemEveryday.CreateItemEverydayList(
                FreeModeItemEveryday.ScnearioType.Story);

            int i = 1, i_total = storyList.Count;
            foreach (var item in storyList)
            {
                Core.Logger.LogInfo($"[{Name}] Progress [{i}/{i_total}]");

                // Title
                if (!string.IsNullOrEmpty(item.title) && !titleHash.Contains(item.title))
                {
                    titleHash.Add(item.title);
                    yield return new TranslationEntry
                    {
                        Term = item.titleTerm,
                        Desc = string.Empty,
                        Original = item.title
                    };
                }

                // Description
                if (!string.IsNullOrEmpty(item.text))
                {
                    yield return new TranslationEntry
                    {
                        Term = item.textTerm,
                        Desc = string.Empty,
                        Original = item.text
                    };
                }

                // Conditions
                var condTerms = item.condition_text_terms;
                var conds = item.condition_texts;
                if (conds != null && condTerms != null)
                {
                    for (int j = 0; j < conds.Length && j < condTerms.Length; j++)
                    {
                        if (!string.IsNullOrEmpty(conds[j]) && !conditionHash.Contains(conds[j]))
                        {
                            conditionHash.Add(conds[j]);
                            yield return new TranslationEntry
                            {
                                Term = condTerms[j],
                                Desc = string.Empty,
                                Original = conds[j]
                            };
                        }
                    }
                }
                i++;
            }
        }
    }

    /// <summary>
    /// 回憶模式 Daily 事件資料源 - Memory Daily Events
    /// </summary>
    public class MemoryDailyDataSource : ITranslationDataSource
    {
        private readonly HashSet<string> conditionHash = new HashSet<string>();
        private readonly HashSet<string> titleHash = new HashSet<string>();

        public string Name => "Memory Daily Events";

        public void Initialize()
        {
            // No initialization needed
        }

        public IEnumerable<TranslationEntry> GetEntries()
        {
            Core.Logger.LogInfo("Processing Daily events...");
            var dailyList = FreeModeItemEveryday.CreateItemEverydayList(
                FreeModeItemEveryday.ScnearioType.Nitijyou);

            int i = 1, i_total = dailyList.Count;
            foreach (var item in dailyList)
            {
                Core.Logger.LogInfo($"[{Name}] Progress [{i}/{i_total}]");

                // Title
                if (!string.IsNullOrEmpty(item.title) && !titleHash.Contains(item.title))
                {
                    titleHash.Add(item.title);
                    yield return new TranslationEntry
                    {
                        Term = item.titleTerm,
                        Desc = string.Empty,
                        Original = item.title
                    };
                }

                // Description
                if (!string.IsNullOrEmpty(item.text))
                {
                    yield return new TranslationEntry
                    {
                        Term = item.textTerm,
                        Desc = string.Empty,
                        Original = item.text
                    };
                }

                // Conditions
                var condTerms = item.condition_text_terms;
                var conds = item.condition_texts;
                if (conds != null && condTerms != null)
                {
                    for (int j = 0; j < conds.Length && j < condTerms.Length; j++)
                    {
                        if (!string.IsNullOrEmpty(conds[j]) && !conditionHash.Contains(conds[j]))
                        {
                            conditionHash.Add(conds[j]);
                            yield return new TranslationEntry
                            {
                                Term = condTerms[j],
                                Desc = string.Empty,
                                Original = conds[j]
                            };
                        }
                    }
                }
                i++;
            }
        }
    }

    /// <summary>
    /// 帝國生活模式資料源 - Empire Life Mode Data
    /// </summary>
    public class EmpireLifeModeDataSource : ApiDataSourceBase<EmpireLifeModeData.Data>
    {
        private readonly HashSet<string> conditionHash = new HashSet<string>();

        public override string Name => "Empire Life Mode";

        public override void Initialize()
        {
            EmpireLifeModeData.CreateData();
        }

        protected override IList<EmpireLifeModeData.Data> FetchData()
        {
            Core.Logger.LogInfo("Processing Empire Life Mode events...");
            return EmpireLifeModeData.GetAllDatas(false);
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(EmpireLifeModeData.Data data, int index, int total)
        {
            Core.Logger.LogInfo($"[{Name}] Progress [{index}/{total}] EmpireLifeModeData ID{data.ID}");

            // Title - using unique name as key
            if (!string.IsNullOrEmpty(data.strUniqueName))
            {
                yield return new TranslationEntry
                {
                    Term = $"SceneFreeModeSelect/タイトル/{data.strUniqueName}",
                    Desc = string.Empty,
                    Original = data.strUniqueName
                };
            }

            // Description - using scenario file name as description
            if (!string.IsNullOrEmpty(data.rawDataScenarioFileName))
            {
                yield return new TranslationEntry
                {
                    Term = $"SceneFreeModeSelect/説明/{data.rawDataScenarioFileName}",
                    Desc = string.Empty,
                    Original = data.rawDataScenarioFileName
                };
            }

            // Conditions - facility names etc.
            if (data.dataFacilityUniqueNameArray != null)
            {
                foreach (var facility in data.dataFacilityUniqueNameArray)
                {
                    if (!string.IsNullOrEmpty(facility) && !conditionHash.Contains(facility))
                    {
                        conditionHash.Add(facility);
                        yield return new TranslationEntry
                        {
                            Term = $"SceneFreeModeSelect/条件文/{facility}",
                            Desc = string.Empty,
                            Original = facility
                        };
                    }
                }
            }
        }
    }
}
