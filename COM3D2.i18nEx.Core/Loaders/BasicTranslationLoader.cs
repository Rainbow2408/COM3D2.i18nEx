﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using COM3D2.i18nEx.Core.Util;
using ExIni;

namespace COM3D2.i18nEx.Core.Loaders
{
    internal class BasicTranslationLoader : ITranslationLoader
    {
        private string langPath;

        public string CurrentLanguage { get; private set; }

        public void SelectLanguage(string name, string path, IniFile config)
        {
            CurrentLanguage = name;
            langPath = path;
            Core.Logger.LogInfo($"Loading language \"{CurrentLanguage}\"");
        }

        public void UnloadCurrentTranslation()
        {
            if (!string.IsNullOrEmpty(CurrentLanguage))
                Core.Logger.LogInfo($"Unloading language \"{CurrentLanguage}\"");
            CurrentLanguage = null;
            langPath = null;
        }

        public IEnumerable<string> GetScriptTranslationFileNames()
        {
            if (string.IsNullOrEmpty(langPath))
                return null;
            var scriptPath = Path.Combine(langPath, "Script");
            if (!Directory.Exists(scriptPath))
                return null;
            return Directory.GetFiles(scriptPath, "*.txt", SearchOption.AllDirectories);
        }

        public IEnumerable<string> GetTextureTranslationFileNames()
        {
            if (string.IsNullOrEmpty(langPath))
                return null;
            var texPath = Path.Combine(langPath, "Textures");
            if (!Directory.Exists(texPath))
                return null;
            return Directory.GetFiles(texPath, "*.png", SearchOption.AllDirectories);
        }

        public SortedDictionary<string, IEnumerable<string>> GetUITranslationFileNames()
        {
            if (string.IsNullOrEmpty(langPath))
                return null;
            var uiPath = Path.Combine(langPath, "UI");
            if (!Directory.Exists(uiPath))
                return null;

            var dict = new SortedDictionary<string, IEnumerable<string>>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var directory in Directory.GetDirectories(uiPath, "*", SearchOption.TopDirectoryOnly))
            {
                var dirName = directory.Splice(uiPath.Length, -1).Trim('\\', '/');
                dict.Add(dirName,
                         Directory.GetFiles(directory, "*.csv", SearchOption.AllDirectories)
                                  .Select(s => s.Splice(directory.Length + 1, -1)));
            }

            return dict;
        }

        public Stream OpenScriptTranslation(string path)
        {
            return !File.Exists(path) ? null : File.OpenRead(path);
        }

        public Stream OpenTextureTranslation(string path)
        {
            return !File.Exists(path) ? null : File.OpenRead(path);
        }

        public Stream OpenUiTranslation(string path)
        {
            if (string.IsNullOrEmpty(langPath))
                return null;
            path = Utility.CombinePaths(langPath, "UI", path);
            return !File.Exists(path) ? null : File.OpenRead(path);
        }
    }
}
