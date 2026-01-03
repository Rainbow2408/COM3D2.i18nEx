namespace TranslationExtract.DataSources
{
    /// <summary>
    /// 封裝單一翻譯條目的資料結構
    /// </summary>
    public class TranslationEntry
    {
        /// <summary>
        /// I2 Localization Term 鍵值 (例如 "SceneTrophy/称号/trophy_001")
        /// </summary>
        public string Term { get; set; }

        /// <summary>
        /// 條目類型，通常為 "Text"
        /// </summary>
        public string Type { get; set; } = "Text";

        /// <summary>
        /// 描述/註解
        /// </summary>
        public string Desc { get; set; } = string.Empty;

        /// <summary>
        /// 原文 (日文)
        /// </summary>
        public string Original { get; set; }

        /// <summary>
        /// 翻譯文本，預設與原文相同
        /// </summary>
        public string Translation { get; set; }

        /// <summary>
        /// 取得有效的翻譯文本（若未設定則返回原文）
        /// </summary>
        public string GetTranslation() => Translation ?? Original;
    }
}
