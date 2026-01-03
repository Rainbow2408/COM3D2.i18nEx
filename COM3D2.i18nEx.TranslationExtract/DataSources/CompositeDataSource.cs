using System.Collections.Generic;
using BepInEx.Logging;

namespace TranslationExtract.DataSources
{
    /// <summary>
    /// 複合資料源 - 用於聚合多個資料源的條目
    /// </summary>
    public class CompositeDataSource : ITranslationDataSource
    {
        private readonly ITranslationDataSource[] _sources;

        public ManualLogSource Logger { get; set; }
        public string Name { get; }

        /// <summary>
        /// 建立複合資料源
        /// </summary>
        /// <param name="name">複合資料源名稱</param>
        /// <param name="sources">子資料源陣列</param>
        public CompositeDataSource(string name, params ITranslationDataSource[] sources)
        {
            Name = name;
            _sources = sources;
        }

        /// <summary>
        /// 設定所有子資料源的 Logger
        /// </summary>
        public void SetLoggerForAllSources(ManualLogSource logger)
        {
            Logger = logger;
            foreach (var source in _sources)
            {
                SetLoggerRecursive(source, logger);
            }
        }

        private static void SetLoggerRecursive(ITranslationDataSource source, ManualLogSource logger)
        {
            // 嘗試設定 Logger 屬性 (使用動態類型檢查)
            var loggerProp = source.GetType().GetProperty("Logger");
            if (loggerProp != null && loggerProp.CanWrite)
            {
                loggerProp.SetValue(source, logger, null);
            }

            // 遞迴處理複合資料源
            if (source is CompositeDataSource composite)
            {
                foreach (var child in composite._sources)
                {
                    SetLoggerRecursive(child, logger);
                }
            }
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
