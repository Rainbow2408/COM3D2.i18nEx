using I2.Loc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace TranslationExtract
{
    internal class CsvData : IDisposable
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
            if (str.Contains("\n") || str.Contains("\"") || str.Contains(","))
                return $"\"{str.Replace("\"", "\"\"")}\"";
            return str;
        }
        internal void Writer(string unitPath, string[] Datas, bool skipIfExists = false)
        {
            if (disposed)
                throw new ArgumentException("This Class already Disposed!");
            if (Datas.Length != 5)
                throw new ArgumentException("Datas.Length != 5");
            pos = Datas[0].IndexOf('/');
            if (!initialized)
            {
                if (pos < 0)
                    throw new ArgumentException("Can not found Term");

                Term = Datas[0].Substring(0, pos);
                Directory.CreateDirectory(unitPath);
                Sw = new StreamWriter(Path.Combine(unitPath, $"{Term}.csv"), false, new UTF8Encoding(true));
                Sw.WriteLine(Header);
                TermNames = new HashSet<string>();
                stringBuilder = new StringBuilder();

                // Clear the StringBuilder before using it
                stringBuilder.Remove(0, stringBuilder.Length);

                initialized = true;
            }
            if (Term != Datas[0].Substring(0, pos))
                throw new ArgumentException("Different Term");
            if (TermNames.Contains(Datas[0]) || (skipIfExists && LocalizationManager.TryGetTranslation(Datas[0], out var _)))
                return;

            stringBuilder.Append(Datas[0]).Append(',').Append(Datas[1]).Append(',').Append(Datas[2]).Append(',').Append(EscapeCSVItem(Datas[3])).Append(',').Append(EscapeCSVItem(Datas[4])).AppendLine();

            TermNames.Add(Datas[0]);
        }
        internal void WriteCSV()
        {
            var lines = stringBuilder.ToString().Split('\n');
            stringBuilder.Remove(0, stringBuilder.Length);
            foreach (var line in lines)
            {
                var str = (line.IndexOf('/') == pos) ? line.Substring(pos + 1) : line;
                stringBuilder.Append(str).Append('\n');
            }
            Sw.WriteLine(stringBuilder.ToString());
            Sw.Flush();
            new WaitForSeconds(.1f);
        }
        public void Dispose()
        {
            if (disposed)
                return;
            // Clean up managed resources
            if (Sw != null)
            {
                Sw.Close();
                new WaitForSeconds(.1f);
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
