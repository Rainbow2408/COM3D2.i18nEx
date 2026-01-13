using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using I2.Loc;
using UnityEngine;
using Object = UnityEngine.Object;

namespace COM3D2.i18nEx.Core.Util
{
    public static class Utility
    {
        private static HashSet<string> outputtedLogs;
        private static MethodInfo _languageMatchesFilterMethod;
        public static bool IsNullOrWhiteSpace(this string str)
        {
            return str == null || str.All(char.IsWhiteSpace);
        }

        public static string ToLF(this string val)
        {
            var sb = new StringBuilder(val.Length);

            foreach (var c in val)
                if (c != '\r')
                    sb.Append(c);

            return sb.ToString();
        }

        public static string CombinePaths(string part1, string part2)
        {
            return Path.Combine(part1, part2);
        }

        public static string CombinePaths(params string[] parts)
        {
            if (parts.Length == 0)
                return null;
            if (parts.Length == 1)
                return parts[0];

            var result = parts[0];

            for (var i = 1; i < parts.Length; i++)
                result = Path.Combine(result, parts[i]);

            return result;
        }

        public static byte[] TexToPng(Texture2D tex)
        {
            if (tex.format == TextureFormat.DXT1 || tex.format == TextureFormat.DXT5)
                return DuplicateTextureToPng(tex);
            try
            {
                return tex.EncodeToPNG();
            }
            catch (Exception)
            {
                return DuplicateTextureToPng(tex);
            }
        }

        private static byte[] DuplicateTextureToPng(Texture2D tex)
        {
            var dup = Duplicate(tex);
            var res = dup.EncodeToPNG();
            Object.Destroy(dup);
            return res;
        }

        private static Texture2D Duplicate(Texture texture)
        {
            var render = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default,
                                                    RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, render);
            var previous = RenderTexture.active;
            RenderTexture.active = render;
            var result = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false);
            result.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(render);
            return result;
        }

        public static bool CheckLanguageName(string langName, out string f_lang)
        {
            if (langName == "loaders")
            {
                f_lang = null;
                return false;
            }

            f_lang = ReFormatLanguageName(langName, out string message);

            if (string.IsNullOrEmpty(f_lang))
            {
                LogOnce($"Skipping loading \"{langName}\" folder. {message}");
                return false;
            }

            if (langName != f_lang)
            {
                LogOnce($"Skipping loading \"{langName}\" folder. The language has been matched, but doesn't comply with the naming rules, please rename it to \"{f_lang}\"");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 記錄日誌訊息，確保相同的訊息只記錄一次
        /// </summary>
        /// <param name="log">日誌訊息</param>
        /// <param name="isWarning">是否為警告等級（否則為 Info 等級）</param>
        private static void LogOnce(string log, bool isWarning = true)
        {
            outputtedLogs ??= new();
            if (outputtedLogs.Contains(log))
                return;

            if (isWarning)
                Core.Logger.LogWarning(log);
            else
                Core.Logger.LogInfo(log);

            outputtedLogs.Add(log);
        }

        public static string ReFormatLanguageName(string languageName, out string message, bool useParentheses = true, bool log = false)
        {
            message = "";
            var value = ReFormatLanguageName2(languageName, ref message, useParentheses);
            if (log && !string.IsNullOrEmpty(message)) Core.Logger.LogWarning(message);
            return value;
        }

        private static string ReFormatLanguageName2(string languageName, ref string message, bool useParentheses = true)
        {
            if (string.IsNullOrEmpty(languageName))
            {
                message = "Language name is empty or null!";
                return null;
            }

            // 直接從字典中查找匹配的語言名稱，參考 GoogleLanguages.GetLanguageCode 方法
            string[] filters = languageName.ToLowerInvariant().Split(" /(),".ToCharArray());
            string result = GoogleLanguages.mLanguageDef.Keys
                .FirstOrDefault(key => LanguageMatchesFilter(key, filters));

            if (string.IsNullOrEmpty(result))
            {
                // 嘗試從 Product.Language 枚舉轉換
                foreach (Product.Language enumValue in Enum.GetValues(typeof(Product.Language)))
                {
                    if (languageName == Product.EnumConvert.GetString(enumValue))
                    {
                        return ReFormatLanguageName2(Product.EnumConvert.ToI2LocalizeLanguageName(enumValue), ref message, useParentheses);
                    }
                }
                message = $"Language \"{languageName}\" does not match the language code!";
                return null;
            }

            // 直接使用找到的 Key 進行格式化，參考 GoogleLanguages.GetLanguageName 方法
            if (!useParentheses)
                return result;

            int slashIndex = result.IndexOf('/');
            if (slashIndex > 0)
                return result.Substring(0, slashIndex) + " (" + result.Substring(slashIndex + 1) + ")";

            return result;
        }

        /// <summary>
        /// 呼叫 GoogleLanguages.LanguageMatchesFilter 私有方法
        /// </summary>
        private static bool LanguageMatchesFilter(string language, string[] filters)
        {
            _languageMatchesFilterMethod ??= AccessTools.Method(typeof(GoogleLanguages), "LanguageMatchesFilter");
            return (bool)_languageMatchesFilterMethod.Invoke(null, new object[] { language, filters });
        }
    }
}
