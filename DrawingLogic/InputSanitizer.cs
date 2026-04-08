using System.Text.RegularExpressions;

namespace AetherBlackbox.DrawingLogic
{
    public static class InputSanitizer
    {
        // A regex to match most C0 and C1 control characters, plus DEL, but keep common whitespace like tab, LF, CR.
        // \u0000-\u0008 (NULL to BS)
        // \u000B-\u000C (VT, FF)
        // \u000E-\u001F (SO to US)
        // \u007F (DEL)
        // \u0080-\u009F (C1 control characters)
        private static readonly Regex ControlCharRegex = new Regex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]", RegexOptions.Compiled);

        public static string Sanitize(string inputText)
        {
            if (string.IsNullOrEmpty(inputText))
            {
                Service.PluginLog?.Debug("[InputSanitizer] Sanitize: Input is null or empty, returning empty string.");
                return string.Empty;
            }

            string originalTextForLog = inputText.Length > 30 ? inputText.Substring(0, 30) + "..." : inputText;
            string sanitizedText = inputText.Replace("%%", "% %"); 
            


            if (inputText != sanitizedText)
            {
                Service.PluginLog?.Debug($"[InputSanitizer] Sanitize: Text was modified. Original: \"{originalTextForLog}\", Sanitized: \"{(sanitizedText.Length > 30 ? sanitizedText.Substring(0, 30) + "..." : sanitizedText)}\"");
            }
            else
            {
                // Service.PluginLog?.Debug($"[InputSanitizer] Sanitize: Text unchanged: \"{originalTextForLog}\"");
            }
            return sanitizedText;
        }
    }
}
