using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;

namespace GhostMacro
{
    /// <summary>Ayarlar (%APPDATA%\GhostMacro\settings.xml) ve makro dosyaları (.gmacro, XML).</summary>
    internal static class Store
    {
        public static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GhostMacro");

        static string SettingsPath => Path.Combine(Dir, "settings.xml");

        public static AppSettings LoadSettings()
        {
            AppSettings s = null;
            try
            {
                if (File.Exists(SettingsPath))
                    using (var f = File.OpenRead(SettingsPath))
                        s = (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(f);
            }
            catch { }
            if (s == null) s = new AppSettings();
            if (s.Lang != "tr" && s.Lang != "en")
                s.Lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? "tr" : "en";
            if (s.Steps == null) s.Steps = new List<Step>();
            return s;
        }

        public static void SaveSettings(AppSettings s)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string tmp = SettingsPath + ".tmp";
                using (var f = File.Create(tmp))
                    new XmlSerializer(typeof(AppSettings)).Serialize(f, s);
                if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
                File.Move(tmp, SettingsPath);
            }
            catch { }
        }

        public static List<Step> LoadMacro(string path)
        {
            using (var f = File.OpenRead(path))
            {
                var m = (MacroFile)new XmlSerializer(typeof(MacroFile)).Deserialize(f);
                return m.Steps ?? new List<Step>();
            }
        }

        public static void SaveMacro(string path, List<Step> steps)
        {
            using (var f = File.Create(path))
                new XmlSerializer(typeof(MacroFile)).Serialize(f, new MacroFile { Steps = steps });
        }
    }
}
