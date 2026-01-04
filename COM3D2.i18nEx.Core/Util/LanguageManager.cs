using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using I2.Loc;

namespace COM3D2.i18nEx.Core.Util
{
    /// <summary>
    /// 語言管理器，處理語言資料夾的創建、檢測和管理
    /// </summary>
    internal static class LanguageManager
    {
        /// <summary>
        /// 語言狀態
        /// </summary>
        public enum LanguageStatus
        {
            /// <summary>資料夾不存在</summary>
            NotExists,
            /// <summary>資料夾存在但為空</summary>
            Empty,
            /// <summary>資料夾存在且有內容</summary>
            Exists
        }

        /// <summary>
        /// 語言信息
        /// </summary>
        public class LanguageInfo
        {
            /// <summary>完整語言名稱（如 "Chinese (Traditional)"）</summary>
            public string FullName { get; set; }

            /// <summary>基礎語言名稱（如 "Chinese"）</summary>
            public string BaseName { get; set; }

            /// <summary>區域名稱（如 "Traditional"），無分區則為 null</summary>
            public string Region { get; set; }

            /// <summary>是否有分區</summary>
            public bool HasRegion => !string.IsNullOrEmpty(Region);

            /// <summary>語言代碼（如 "zh-TW"）</summary>
            public string Code { get; set; }

            /// <summary>資料夾路徑</summary>
            public string FolderPath { get; set; }

            /// <summary>當前狀態</summary>
            public LanguageStatus Status { get; set; }
        }

        /// <summary>
        /// 取得所有可用語言（從 GoogleLanguages）
        /// </summary>
        public static List<LanguageInfo> GetAllLanguages()
        {
            var languages = new List<LanguageInfo>();

            if (GoogleLanguages.mLanguageDef == null)
                return languages;

            foreach (var kvp in GoogleLanguages.mLanguageDef)
            {
                string languageName = kvp.Key;
                string code = kvp.Value.Code;

                // 解析語言名稱（例如 "Chinese/Traditional" -> BaseName: "Chinese", Region: "Traditional"）
                string baseName;
                string region = null;

                int slashIndex = languageName.IndexOf('/');
                if (slashIndex > 0)
                {
                    baseName = languageName.Substring(0, slashIndex);
                    region = languageName.Substring(slashIndex + 1);
                }
                else
                {
                    baseName = languageName;
                }

                // 格式化為標準名稱（如 "Chinese (Traditional)"）
                string fullName = region != null ? $"{baseName} ({region})" : baseName;

                var info = new LanguageInfo
                {
                    FullName = fullName,
                    BaseName = baseName,
                    Region = region,
                    Code = code,
                    FolderPath = GetLanguageFolderPath(fullName),
                    Status = GetLanguageStatus(fullName)
                };

                languages.Add(info);
            }

            return languages;
        }

        /// <summary>
        /// 取得特定基礎語言的所有分區
        /// </summary>
        public static List<LanguageInfo> GetLanguageRegions(string baseName)
        {
            var allLanguages = GetAllLanguages();
            return allLanguages.Where(lang => lang.BaseName == baseName).ToList();
        }

        /// <summary>
        /// 檢查語言是否有分區
        /// </summary>
        public static bool HasRegions(string baseName)
        {
            var regions = GetLanguageRegions(baseName);
            return regions.Count > 1 || (regions.Count == 1 && regions[0].HasRegion);
        }

        /// <summary>
        /// 取得語言資料夾路徑
        /// </summary>
        public static string GetLanguageFolderPath(string languageName)
        {
            return Path.Combine(Paths.TranslationsRoot, languageName);
        }

        /// <summary>
        /// 取得語言狀態
        /// </summary>
        public static LanguageStatus GetLanguageStatus(string languageName)
        {
            string folderPath = GetLanguageFolderPath(languageName);

            if (!Directory.Exists(folderPath))
                return LanguageStatus.NotExists;

            // 檢查是否為空資料夾（不包含任何文件或子資料夾）
            bool hasFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories).Length > 0;

            return hasFiles ? LanguageStatus.Exists : LanguageStatus.Empty;
        }

        /// <summary>
        /// 創建語言資料夾
        /// </summary>
        public static bool CreateLanguageFolder(string languageName)
        {
            try
            {
                string folderPath = GetLanguageFolderPath(languageName);

                if (Directory.Exists(folderPath))
                    return true;

                Directory.CreateDirectory(folderPath);

                // 創建子資料夾結構
                Directory.CreateDirectory(Path.Combine(folderPath, "Script"));
                Directory.CreateDirectory(Path.Combine(folderPath, "Textures"));
                Directory.CreateDirectory(Path.Combine(folderPath, "UI"));

                Core.Logger.LogInfo($"Created language folder: {languageName}");
                return true;
            }
            catch (Exception ex)
            {
                Core.Logger.LogError($"Failed to create language folder '{languageName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 檢查語言資料夾是否為空
        /// </summary>
        public static bool IsLanguageFolderEmpty(string languageName)
        {
            return GetLanguageStatus(languageName) == LanguageStatus.Empty;
        }

        /// <summary>
        /// 重命名語言資料夾（用於標記為手動刪除）
        /// </summary>
        public static bool RenameLanguageFolder(string languageName, string newName)
        {
            try
            {
                string oldPath = GetLanguageFolderPath(languageName);
                string newPath = GetLanguageFolderPath(newName);

                if (!Directory.Exists(oldPath))
                    return false;

                if (Directory.Exists(newPath))
                {
                    Core.Logger.LogWarning($"Cannot rename: folder '{newName}' already exists");
                    return false;
                }

                Directory.Move(oldPath, newPath);
                Core.Logger.LogInfo($"Renamed language folder: {languageName} -> {newName}");
                return true;
            }
            catch (Exception ex)
            {
                Core.Logger.LogError($"Failed to rename language folder '{languageName}' to '{newName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 取得所有已存在的語言（包含空和非空）
        /// </summary>
        public static List<LanguageInfo> GetExistingLanguages()
        {
            var allLanguages = GetAllLanguages();
            return allLanguages.Where(lang => lang.Status != LanguageStatus.NotExists).ToList();
        }

        /// <summary>
        /// 從資料夾掃描已存在的語言（包括非標準語言）
        /// </summary>
        public static List<string> ScanExistingLanguageFolders()
        {
            var languages = new List<string>();

            if (!Directory.Exists(Paths.TranslationsRoot))
                return languages;

            var directories = Directory.GetDirectories(Paths.TranslationsRoot, "*", SearchOption.TopDirectoryOnly);

            foreach (var dir in directories)
            {
                string langName = Path.GetFileName(dir);

                // 排除以 manual_del_ 開頭的資料夾
                if (langName.StartsWith("manual_del_"))
                    continue;

                languages.Add(langName);
            }

            return languages;
        }

        /// <summary>
        /// 驗證語言名稱是否有效（使用 Utility.CheckLanguageName）
        /// </summary>
        public static bool ValidateLanguageName(string languageName, out string formattedName)
        {
            return Utility.CheckLanguageName(languageName, out formattedName);
        }

        /// <summary>
        /// 格式化語言名稱（使用 Utility.ReFormatLanguageName）
        /// </summary>
        public static string FormatLanguageName(string languageName)
        {
            return Utility.ReFormatLanguageName(languageName, out _, log: false);
        }
    }
}
