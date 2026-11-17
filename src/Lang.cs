namespace GhostMacro
{
    /// <summary>Basit iki dilli metin seçici (Türkçe / English).</summary>
    internal static class L
    {
        public static bool En;
        public static string T(string tr, string en) => En ? en : tr;
    }
}
