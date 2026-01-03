using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using MaidStatus;

namespace TranslationExtract.DataSources
{
    /// <summary>
    /// 女僕性格資料源 - Maid Personality
    /// </summary>
    public class PersonalDataSource : ApiDataSource<Personal.Data>
    {
        public override string Name => "Maid Personality";

        protected override IList<Personal.Data> FetchData()
        {
            Logger?.LogInfo("Getting personality data");
            return Personal.GetAllDatas(false);
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(Personal.Data data, int index, int total)
        {
            Logger?.LogDebug($"[{Name}] Progress [{index}/{total}] Personal ID{data.id}");

            yield return new TranslationEntry
            {
                Term = data.termName,
                Desc = "Personal",
                Original = data.drawName
            };
        }
    }

    /// <summary>
    /// 夜伽職業資料源 - Yotogi Class
    /// </summary>
    public class YotogiClassDataSource : ApiDataSource<YotogiClass.Data>
    {
        public override string Name => "Yotogi Class";

        protected override IList<YotogiClass.Data> FetchData()
        {
            Logger?.LogInfo("Getting yotogi class data");
            return YotogiClass.GetAllDatas(false);
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(YotogiClass.Data data, int index, int total)
        {
            Logger?.LogDebug($"[{Name}] Progress [{index}/{total}] YotogiClass ID{data.id}");

            // Class name
            yield return new TranslationEntry
            {
                Term = data.termName,
                Desc = "YotogiClass",
                Original = data.drawName
            };

            // Explanatory text
            yield return new TranslationEntry
            {
                Term = data.termExplanatoryText,
                Desc = "YotogiClass",
                Original = data.explanatoryText
            };
        }
    }

    /// <summary>
    /// 女僕職業資料源 - Job Class
    /// </summary>
    public class JobClassDataSource : ApiDataSource<JobClass.Data>
    {
        public override string Name => "Job Class";

        protected override IList<JobClass.Data> FetchData()
        {
            Logger?.LogInfo("Getting job class data");
            return JobClass.GetAllDatas(false);
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(JobClass.Data data, int index, int total)
        {
            Logger?.LogDebug($"[{Name}] Progress [{index}/{total}] JobClass ID{data.id}");

            // Class name
            yield return new TranslationEntry
            {
                Term = data.termName,
                Desc = "JobClass",
                Original = data.drawName
            };

            // Explanatory text
            yield return new TranslationEntry
            {
                Term = data.termExplanatoryText,
                Desc = "JobClass",
                Original = data.explanatoryText
            };
        }
    }

    /// <summary>
    /// 稱號資料源 - Title
    /// </summary>
    public class TitleDataSource : ITranslationDataSource
    {
        public ManualLogSource Logger { get; set; }
        public string Name => "Title";

        public void Initialize()
        {
            Title.CreateData();
        }

        public IEnumerable<TranslationEntry> GetEntries()
        {
            Logger?.LogInfo("Getting title data");

            var title_data_list_ = Traverse.CreateWithType("MaidStatus.Title")
                .Field("checkAchivementList")
                .GetValue<List<KeyValuePair<string, ParametersPack>>>();

            int i = 1, i_total = title_data_list_.Count;
            foreach (var data in title_data_list_)
            {
                Logger?.LogDebug($"[{Name}] Progress [{i}/{i_total}] Title");

                // Term logic from MaidStatus.Status.conditionTermText
                yield return new TranslationEntry
                {
                    Term = "MaidStatus/ステータス称号/" + data.Key,
                    Desc = "Title",
                    Original = data.Key
                };
                i++;
            }
        }
    }

    /// <summary>
    /// 特徴資料源 - Feature
    /// </summary>
    public class FeatureDataSource : ApiDataSource<Feature.Data>
    {
        public override string Name => "Feature";

        protected override IList<Feature.Data> FetchData()
        {
            Logger?.LogInfo("Getting feature data");
            return Feature.GetAllDatas(false);
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(Feature.Data data, int index, int total)
        {
            Logger?.LogDebug($"[{Name}] Progress [{index}/{total}] Feature ID{data.id}");

            yield return new TranslationEntry
            {
                Term = data.termName,
                Desc = "Feature",
                Original = data.drawName
            };
        }
    }
}
