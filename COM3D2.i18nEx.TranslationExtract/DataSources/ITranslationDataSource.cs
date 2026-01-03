using System.Collections.Generic;

namespace TranslationExtract.DataSources
{
    /// <summary>
    /// 翻譯資料源介面
    /// </summary>
    public interface ITranslationDataSource
    {
        /// <summary>
        /// 資料源名稱 (例如 "Trophy", "Dance")
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 初始化資料源 (例如呼叫 CreateData() 等 API)
        /// </summary>
        void Initialize();

        /// <summary>
        /// 獲取所有翻譯條目
        /// </summary>
        IEnumerable<TranslationEntry> GetEntries();
    }
}
