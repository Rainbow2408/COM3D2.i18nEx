using System.Collections.Generic;

namespace COM3D2.i18nEx.Core.TranslationExtract.DataSources
{
    /// <summary>
    /// Dance 舞蹈資料源
    /// </summary>
    public class DanceDataSource : ApiDataSourceBase<DanceData>
    {
        public override string Name => "Dance";

        public override void Initialize()
        {
            DanceSelect.CreateDanceData();
        }

        protected override IList<DanceData> FetchData()
        {
            return DanceSelect.GetDanceDataList();
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(DanceData data, int index, int total)
        {
            Core.Logger.LogInfo($"[{Name}] Progress [{index}/{total}] DanceData ID{data.ID}");

            // Dance title
            yield return new TranslationEntry
            {
                Term = $"SceneDanceSelect/曲名/{data.ID}",
                Original = data.title
            };

            // Dance description (optional)
            if (!string.IsNullOrEmpty(data.commentary_text))
            {
                yield return new TranslationEntry
                {
                    Term = $"SceneDanceSelect/曲説明/{data.ID}",
                    Original = data.commentary_text
                };
            }
        }
    }
}
