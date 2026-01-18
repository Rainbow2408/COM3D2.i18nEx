using System.Collections.Generic;

namespace COM3D2.i18nEx.Core.TranslationExtract.DataSources
{
    /// <summary>
    /// Trophy 獎盃資料源
    /// </summary>
    public class TrophyDataSource : ApiDataSourceBase<Trophy.Data>
    {
        public override string Name => "Trophy";

        protected override IList<Trophy.Data> FetchData()
        {
            return Trophy.GetAllDatas(false);
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(Trophy.Data data, int index, int total)
        {
            // Trophy name
            yield return new TranslationEntry
            {
                Term = data.nameTerm,
                Original = data.name
            };

            // Trophy description (optional)
            if (!string.IsNullOrEmpty(data.infoText))
            {
                yield return new TranslationEntry
                {
                    Term = data.infoTextTerm,
                    Original = data.infoText
                };
            }
        }
    }
}
