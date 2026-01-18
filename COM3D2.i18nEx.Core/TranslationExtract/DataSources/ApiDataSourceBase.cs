using System.Collections.Generic;

namespace COM3D2.i18nEx.Core.TranslationExtract.DataSources
{
    /// <summary>
    /// 基於遊戲 API 的資料源抽象基類
    /// </summary>
    /// <typeparam name="TData">API 返回的資料類型</typeparam>
    public abstract class ApiDataSourceBase<TData> : ITranslationDataSource
    {
        /// <summary>
        /// 資料源名稱
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// 從遊戲 API 獲取原始資料清單
        /// </summary>
        protected abstract IList<TData> FetchData();

        /// <summary>
        /// 處理單一資料項目並轉換為翻譯條目
        /// </summary>
        /// <param name="item">資料項目</param>
        /// <param name="index">當前索引 (1-based)</param>
        /// <param name="total">總數量</param>
        /// <returns>翻譯條目集合</returns>
        protected abstract IEnumerable<TranslationEntry> ProcessItem(TData item, int index, int total);

        /// <summary>
        /// 初始化資料源，預設空實作
        /// </summary>
        public virtual void Initialize() { }

        /// <summary>
        /// 獲取所有翻譯條目，包含進度記錄
        /// </summary>
        public virtual IEnumerable<TranslationEntry> GetEntries()
        {
            var dataList = FetchData();
            int total = dataList.Count;
            int current = 1;

            foreach (var item in dataList)
            {
                Core.Logger.LogInfo($"[Dump{Name}] Progress [{current}/{total}]");

                foreach (var entry in ProcessItem(item, current, total))
                {
                    yield return entry;
                }

                current++;
            }
        }
    }
}
