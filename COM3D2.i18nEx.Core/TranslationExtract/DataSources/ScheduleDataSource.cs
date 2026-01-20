using System.Collections.Generic;
using Schedule;

namespace COM3D2.i18nEx.Core.TranslationExtract.DataSources
{
    /// <summary>
    /// 行程訓練資料源 - Schedule Training Data
    /// </summary>
    public class ScheduleTrainingDataSource : ApiDataSourceBase<ScheduleCSVData.Training>
    {
        private readonly HashSet<string> nameHash = new();

        public override string Name => "Schedule Training";

        protected override IList<ScheduleCSVData.Training> FetchData()
        {
            Core.Logger.LogInfo($"[{Name}] Getting datas via API");
            var trainingData = ScheduleCSVData.TrainingData;
            var result = new List<ScheduleCSVData.Training>(trainingData.Count);
            foreach (var kvp in trainingData)
            {
                result.Add(kvp.Value);
            }
            return result;
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(ScheduleCSVData.Training data, int index, int total)
        {
            Core.Logger.LogInfo($"[{Name}] Progress [{index}/{total}] ID:{data.id}");
            // 項目名稱
            if (!string.IsNullOrEmpty(data.name) && !nameHash.Contains(data.name))
            {
                nameHash.Add(data.name);
                yield return new TranslationEntry
                {
                    Term = $"SceneDaily/スケジュール/項目/{data.name}",
                    Desc = string.Empty,
                    Original = data.name
                };
            }
        }
    }

    /// <summary>
    /// 行程夜伽資料源 - Schedule Yotogi Data
    /// </summary>
    public class ScheduleYotogiDataSource : ApiDataSourceBase<ScheduleCSVData.Yotogi>
    {
        private readonly HashSet<string> nameHash = new();
        private readonly HashSet<string> conditionHash = new();

        public override string Name => "Schedule Yotogi";

        protected override IList<ScheduleCSVData.Yotogi> FetchData()
        {
            Core.Logger.LogInfo($"[{Name}] Getting data via API");
            var yotogiData = ScheduleCSVData.YotogiData;
            var result = new List<ScheduleCSVData.Yotogi>(yotogiData.Count);
            foreach (var kvp in yotogiData)
            {
                result.Add(kvp.Value);
            }
            return result;
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(ScheduleCSVData.Yotogi data, int index, int total)
        {
            Core.Logger.LogInfo($"[{Name}] Progress [{index}/{total}] ID:{data.id}");
            // 項目名稱
            if (!string.IsNullOrEmpty(data.name) && !nameHash.Contains(data.name))
            {
                nameHash.Add(data.name);
                yield return new TranslationEntry
                {
                    Term = $"SceneDaily/スケジュール/項目/{data.name}",
                    Desc = string.Empty,
                    Original = data.name
                };
            }

            // 說明
            if (!string.IsNullOrEmpty(data.information))
            {
                yield return new TranslationEntry
                {
                    Term = $"SceneDaily/スケジュール/説明/{data.name}",
                    Desc = string.Empty,
                    Original = data.information
                };
            }

            // 條件列表
            if (data.condInfo != null)
            {
                foreach (var cond in data.condInfo)
                {
                    if (!string.IsNullOrEmpty(cond) && !conditionHash.Contains(cond))
                    {
                        conditionHash.Add(cond);
                        yield return new TranslationEntry
                        {
                            Term = $"SceneDaily/スケジュール/条件文/{cond}",
                            Desc = string.Empty,
                            Original = cond
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// 行程類別名稱資料源 - Schedule Category Names
    /// </summary>
    public class ScheduleCategoryDataSource : ITranslationDataSource
    {
        public string Name => "Schedule Categories";

        public void Initialize()
        {
            // No initialization needed
        }

        public IEnumerable<TranslationEntry> GetEntries()
        {
            Core.Logger.LogInfo($"[{Name}] Getting names via API");

            if (ScheduleCSVData.TaskCategoryNameMap != null)
            {
                int index = 1, total = ScheduleCSVData.TaskCategoryNameMap.Count;
                foreach (var kvp in ScheduleCSVData.TaskCategoryNameMap)
                {
                    Core.Logger.LogInfo($"[{Name}] Progress [{index}/{total}] name ID:{kvp.Key}");
                    if (!string.IsNullOrEmpty(kvp.Value))
                    {
                        yield return new TranslationEntry
                        {
                            Term = $"SceneDaily/スケジュール/カテゴリー/{kvp.Value}",
                            Desc = string.Empty,
                            Original = kvp.Value
                        };
                    }
                    index++;
                }
            }
        }
    }
}
