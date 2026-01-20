using COM3D2.i18nEx.Core.Util;
using I2.Loc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace COM3D2.i18nEx.Core.TranslationExtract
{
    /// <summary>
    /// CSV 寫入器，用於生成翻譯 CSV 檔案
    /// 支援多個 Term 前綴，每個前綴獨立輸出為一個 CSV 檔案
    /// </summary>
    internal class CsvWriter : IDisposable
    {
        private bool disposed = false;
        private readonly string targetLanguage;
        private readonly string Header;

        /// <summary>
        /// 建構函數
        /// </summary>
        /// <param name="targetLanguage">目標語言名稱，用於 CSV 標題</param>
        public CsvWriter(string targetLanguage = "English")
        {
            this.targetLanguage = targetLanguage;
            this.Header = $"Key,Type,Desc,Japanese,{targetLanguage}";
        }
        // 每個 Term 前綴的寫入器資料
        private class TermWriter
        {
            public StreamWriter Sw;
            public StringBuilder StringBuilder = new StringBuilder();
            public HashSet<string> TermNames = new HashSet<string>();
            public int Pos;
        }
        
        private readonly Dictionary<string, TermWriter> writers = new Dictionary<string, TermWriter>();
        private string unitPath;



        /// <summary>
        /// 寫入一筆翻譯資料
        /// </summary>
        /// <param name="unitPath">輸出資料夾路徑</param>
        /// <param name="datas">資料陣列 [Term, Type, Desc, Japanese, English]</param>
        /// <param name="skipIfExists">若已存在翻譯則跳過</param>
        internal void Write(string unitPath, string[] datas, bool skipIfExists = false)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(CsvWriter));
            if (datas.Length != 5)
                throw new ArgumentException("datas.Length != 5");

            this.unitPath = unitPath;
            
            int pos = datas[0].IndexOf('/');
            if (pos < 0)
                throw new ArgumentException("Cannot find Term separator '/'");

            string term = datas[0].Substring(0, pos);
            
            // 取得或建立此 Term 的寫入器
            if (!writers.TryGetValue(term, out var writer))
            {
                Directory.CreateDirectory(unitPath);
                writer = new TermWriter
                {
                    Pos = pos,
                    Sw = new StreamWriter(Path.Combine(unitPath, $"{term}.csv"), false, new UTF8Encoding(true))
                };
                writer.Sw.WriteLine(Header);
                writers[term] = writer;
            }

            if (writer.TermNames.Contains(datas[0]) || (skipIfExists && LocalizationManager.TryGetTranslation(datas[0], out var _)))
                return;

            writer.StringBuilder.Append(datas[0]).Append(',')
                         .Append(datas[1]).Append(',')
                         .Append(datas[2]).Append(',')
                         .Append(CsvHelper.Escape(datas[3])).Append(',')
                         .Append(CsvHelper.Escape(datas[4])).AppendLine();

            writer.TermNames.Add(datas[0]);
        }

        /// <summary>
        /// 寫入一筆 TranslationEntry
        /// </summary>
        internal void Write(string unitPath, TranslationEntry entry, bool skipIfExists = false)
        {
            var datas = new string[]
            {
                entry.Term,
                entry.Type,
                entry.Desc ?? string.Empty,
                entry.Original,
                entry.GetTranslation()
            };
            Write(unitPath, datas, skipIfExists);
        }

        /// <summary>
        /// 刷新並寫入所有 CSV 內容
        /// </summary>
        internal void Flush()
        {
            foreach (var kvp in writers)
            {
                var writer = kvp.Value;
                if (writer.Sw == null || writer.StringBuilder.Length == 0)
                    continue;

                var lines = writer.StringBuilder.ToString().Split('\n');

                // 處理輸出內容（移除 Term 前綴）
                var output = new System.Text.StringBuilder();
                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line))
                        continue;
                    var str = (line.IndexOf('/') == writer.Pos) ? line.Substring(writer.Pos + 1) : line;
                    output.Append(str).Append('\n');
                }
                writer.Sw.Write(output.ToString());
                writer.Sw.Flush();

                // 清空 StringBuilder，防止二次 Flush 導致重複輸出
#if COM3D25
                writer.StringBuilder.Clear();
#else
                writer.StringBuilder.Length = 0;
#endif
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            Flush();

            foreach (var kvp in writers)
            {
                var writer = kvp.Value;
                if (writer.Sw != null)
                {
                    writer.Sw.Close();
                    writer.Sw.Dispose();
                    writer.Sw = null;
                }
                writer.TermNames = null;
                writer.StringBuilder = null;
            }
            writers.Clear();

            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
