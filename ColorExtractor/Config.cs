using System;
using System.IO;
using System.Text.Json;

namespace ColorExtractorApp
{
    internal class AppConfig
    {
        public string ESP32_IP { get; set; } = "192.168.0.189";
        public int ESP32_PORT { get; set; } = 7777;

        public int SampleStep { get; set; } = 4;            // sparse grid step for averaging
        public bool LowResourceMode { get; set; } = false;  // sample center pixel only

        public int MarginTop { get; set; } = 80;
        public int MarginBottom { get; set; } = 80;
        public int MarginLeft { get; set; } = 40;
        public int MarginRight { get; set; } = 40;

        public int TopLeds { get; set; } = 37;
        public int BottomLeds { get; set; } = 37;
        public int LeftLeds { get; set; } = 20;
        public int RightLeds { get; set; } = 21;

        public int Offset { get; set; } = 19;               // rotate logical LED start
        public bool Clockwise { get; set; } = false;         // logical direction

        public int TargetFps { get; set; } = 60;

        public bool BoostSaturation { get; set; } = true;
        public double BoostFactor { get; set; } = 1.35;
        public double BoostMinValue { get; set; } = 0.07;

        public static AppConfig LoadOrCreate(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch (Exception)
            {
                // fall through to create default
            }

            var def = new AppConfig();
            try
            {
                var json = JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, json);
            }
            catch (Exception)
            {
                // ignore write errors; continue with defaults
            }
            return def;
        }
    }
}
