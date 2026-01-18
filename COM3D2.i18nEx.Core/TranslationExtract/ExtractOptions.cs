namespace COM3D2.i18nEx.Core.TranslationExtract
{
    /// <summary>
    /// 翻譯提取選項
    /// </summary>
    public class ExtractOptions
    {
        // 基本選項
        public bool DumpUI { get; set; } = true;
        public bool DumpScripts { get; set; } = true;

        // 動態資料
        public bool DumpItemNames { get; set; } = false;
        public bool DumpEvents { get; set; } = false;
        public bool DumpSchedule { get; set; } = false;
        public bool DumpMaidStatus { get; set; } = false;
        public bool DumpYotogi { get; set; } = false;
        public bool DumpTrophy { get; set; } = false;
        public bool DumpNPC { get; set; } = false;
        public bool DumpGuest { get; set; } = false;
        public bool DumpDance { get; set; } = false;
        public bool DumpMansion { get; set; } = false;
        public bool DumpMemory { get; set; } = false;

        // 其他選項
        public bool SkipTranslatedItems { get; set; } = false;

        /// <summary>
        /// 建立預設全開選項
        /// </summary>
        public static ExtractOptions CreateFullExtract()
        {
            return new ExtractOptions
            {
                DumpUI = true,
                DumpScripts = true,
                DumpItemNames = true,
                DumpEvents = true,
                DumpSchedule = true,
                DumpMaidStatus = true,
                DumpYotogi = true,
                DumpTrophy = true,
                DumpNPC = true,
                DumpGuest = true,
                DumpDance = true,
                DumpMansion = true,
                DumpMemory = true,
                SkipTranslatedItems = false
            };
        }
    }
}
