using System.Text.RegularExpressions;

namespace COM3D2.i18nEx.Core.Util
{
    public static class NoLocalize
    {
        // Provide a public property to get the non-localization prefix
        public static string NoLocalizePrefix => "\u180e";

        private static readonly Regex hasNoLocalizePrefix = new($"^{NoLocalizePrefix}", RegexOptions.Compiled);

        // Provide a public method to add prefix
        public static string MarkAsNoLocalize(string text)
        {
            if (string.IsNullOrEmpty(text) || hasNoLocalizePrefix.IsMatch(text))
                return text;

            return NoLocalizePrefix + text;
        }

        // Provide a public method to check if text is marked as non-localizable
        public static bool IsMarkedAsNoLocalize(string text)
        {
            return !string.IsNullOrEmpty(text) && hasNoLocalizePrefix.IsMatch(text);
        }

        public static string RemoveNoLocalizePrefix(string text)
        {
            if (IsMarkedAsNoLocalize(text))
                return hasNoLocalizePrefix.Replace(text, "");

            return text;
        }
    }
}