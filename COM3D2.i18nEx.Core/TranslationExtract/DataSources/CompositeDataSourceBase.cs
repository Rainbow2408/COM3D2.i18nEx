using System.Collections.Generic;

namespace COM3D2.i18nEx.Core.TranslationExtract.DataSources
{
    /// <summary>
    /// 複合資料源 - 用於聚合多個資料源的條目
    /// </summary>
    public class CompositeDataSourceBase : ITranslationDataSource
    {
        private readonly ITranslationDataSource[] _sources;

        public string Name { get; }

        /// <summary>
        /// 建立複合資料源
        /// </summary>
        /// <param name="name">複合資料源名稱</param>
        /// <param name="sources">子資料源陣列</param>
        public CompositeDataSourceBase(string name, params ITranslationDataSource[] sources)
        {
            Name = name;
            _sources = sources;
        }

        public void Initialize()
        {
            foreach (var source in _sources)
            {
                source.Initialize();
            }
        }

        public IEnumerable<TranslationEntry> GetEntries()
        {
            foreach (var source in _sources)
            {
                foreach (var entry in source.GetEntries())
                {
                    yield return entry;
                }
            }
        }
    }
}
