using I2.Loc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace COM3D2.i18nEx.Core.TranslationExtract
{
    /// <summary>
    /// CSV 寫入器，用於生成翻譯 CSV 檔案
    /// </summary>
    internal class CsvWriter : IDisposable
    {
        private bool disposed = false;
        private HashSet<string> TermNames;
        private string Term;
        private int pos;
        private bool initialized;
        private StreamWriter Sw;
        private StringBuilder stringBuilder;
        private readonly string Header = "Key,Type,Desc,Japanese,English";

        private static string EscapeCSVItem(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            if (str.Contains("\n") || str.Contains("\"") || str.Contains(","))
                return $"\"{str.Replace("\"", "\"\"")}\"";
            return str;
        }

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

            pos = datas[0].IndexOf('/');
            if (!initialized)
            {
                if (pos < 0)
                    throw new ArgumentException("Cannot find Term separator '/'");

                Term = datas[0].Substring(0, pos);
                Directory.CreateDirectory(unitPath);
                Sw = new StreamWriter(Path.Combine(unitPath, $"{Term}.csv"), false, new UTF8Encoding(true));
                Sw.WriteLine(Header);
                TermNames = new HashSet<string>();
                stringBuilder = new StringBuilder();
                initialized = true;
            }

            if (Term != datas[0].Substring(0, pos))
                throw new ArgumentException($"Different Term: expected '{Term}', got '{datas[0].Substring(0, pos)}'");

            if (TermNames.Contains(datas[0]) || (skipIfExists && LocalizationManager.TryGetTranslation(datas[0], out var _)))
                return;

            stringBuilder.Append(datas[0]).Append(',')
                         .Append(datas[1]).Append(',')
                         .Append(datas[2]).Append(',')
                         .Append(EscapeCSVItem(datas[3])).Append(',')
                         .Append(EscapeCSVItem(datas[4])).AppendLine();

            TermNames.Add(datas[0]);
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
        /// 刷新並寫入 CSV 內容
        /// </summary>
        internal void Flush()
        {
            if (!initialized || Sw == null)
                return;

            var lines = stringBuilder.ToString().Split('\n');

#if COM3D25
            stringBuilder.Clear();
#else
            stringBuilder.Length = 0;
#endif

            foreach (var line in lines)
            {
                var str = (line.IndexOf('/') == pos) ? line.Substring(pos + 1) : line;
                stringBuilder.Append(str).Append('\n');
            }
            Sw.Write(stringBuilder.ToString());
            Sw.Flush();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            Flush();

            if (Sw != null)
            {
                Sw.Close();
                Sw.Dispose();
                Sw = null;
            }

            TermNames = null;
            Term = null;
            initialized = false;
            stringBuilder = null;
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
