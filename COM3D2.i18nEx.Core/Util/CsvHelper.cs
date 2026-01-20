using System;
using System.Collections.Generic;
using System.Text;

namespace COM3D2.i18nEx.Core.Util
{
    /// <summary>
    /// CSV 處理工具類
    /// 統一處理 CSV 轉義、解析和過濾邏輯
    /// </summary>
    public static class CsvHelper
    {
        /// <summary>
        /// 轉義 CSV 欄位
        /// 處理逗號、換行符和引號
        /// </summary>
        public static string Escape(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            if (str.Contains(",") || str.Contains("\"") || str.Contains("\n") || str.Contains("\r"))
                return $"\"{str.Replace("\"", "\"\"")}\"";
            return str;
        }

        /// <summary>
        /// 解析 CSV 行（處理引號內的逗號）
        /// </summary>
        public static List<string> ParseLine(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(line))
                return result;

            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // 轉義的引號
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
#if COM3D25
                    current.Clear();
#else
                    current.Length = 0;
#endif
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result;
        }

        /// <summary>
        /// 過濾 CSV 欄位，只保留 Key, Type, Desc, Japanese, 目標語言
        /// </summary>
        public static string FilterColumns(string csvContent, string targetLanguage)
        {
            var lines = csvContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length == 0)
                return csvContent;

            var result = new StringBuilder();

            // 解析標題行找出欄位索引
            var headerFields = ParseLine(lines[0]);
            int keyIndex = -1, typeIndex = -1, descIndex = -1, japaneseIndex = -1, targetIndex = -1;

            for (int i = 0; i < headerFields.Count; i++)
            {
                var field = headerFields[i].Trim();
                if (field.Equals("Key", StringComparison.OrdinalIgnoreCase))
                    keyIndex = i;
                else if (field.Equals("Type", StringComparison.OrdinalIgnoreCase))
                    typeIndex = i;
                else if (field.Equals("Desc", StringComparison.OrdinalIgnoreCase))
                    descIndex = i;
                else if (field.Equals("Japanese", StringComparison.OrdinalIgnoreCase))
                    japaneseIndex = i;
                else if (field.Equals(targetLanguage, StringComparison.OrdinalIgnoreCase))
                    targetIndex = i;
            }

            // 如果找不到目標語言欄位，返回原始內容
            if (targetIndex == -1)
            {
                Core.Logger.LogWarning($"Target language '{targetLanguage}' not found in CSV, returning original");
                return csvContent;
            }

            // 寫入新標題
            result.AppendLine($"Key,Type,Desc,Japanese,{targetLanguage}");

            // 處理每一行資料
            for (int lineIdx = 1; lineIdx < lines.Length; lineIdx++)
            {
#if COM3D25
                if (string.IsNullOrWhiteSpace(lines[lineIdx]))
#else
                if (string.IsNullOrEmpty(lines[lineIdx]?.Trim()))
#endif
                    continue;

                var fields = ParseLine(lines[lineIdx]);
                int maxIndex = Math.Max(Math.Max(Math.Max(keyIndex, typeIndex), Math.Max(descIndex, japaneseIndex)), targetIndex);
                if (fields.Count <= maxIndex)
                    continue;

                var key = keyIndex >= 0 && keyIndex < fields.Count ? fields[keyIndex] : "";
                var type = typeIndex >= 0 && typeIndex < fields.Count ? fields[typeIndex] : "";
                var desc = descIndex >= 0 && descIndex < fields.Count ? fields[descIndex] : "";
                var japanese = japaneseIndex >= 0 && japaneseIndex < fields.Count ? fields[japaneseIndex] : "";
                var target = targetIndex >= 0 && targetIndex < fields.Count ? fields[targetIndex] : "";

                result.AppendLine($"{Escape(key)},{Escape(type)},{Escape(desc)},{Escape(japanese)},{Escape(target)}");
            }

            return result.ToString();
        }
    }
}
