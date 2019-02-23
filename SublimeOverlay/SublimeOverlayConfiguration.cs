using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SublimeOverlay
{
    internal class SublimeOverlayConfiguration
    {
        public Color CloseControlColor { get; }
        public Color MaximizeControlColor { get; }
        public Color MinimizeControlColor { get; }
        public Color TitleBarColor { get; }

        public string SublimeExePath { get; }

        /// <summary>
        /// Loads a configuration file
        /// </summary>
        /// <returns>The configuration</returns>
        public static SublimeOverlayConfiguration Load()
        {
            var closeControlColor = Color.Red;
            var maximizeControlColor = Color.White;
            var minimizeControlColor = Color.White;
            var titleBarColor = Color.CornflowerBlue;

            var sublimeExePath = string.Empty;

            var settingsPath = Directory.GetCurrentDirectory() + @"\settings.config";

            if (File.Exists(settingsPath))
            {
                using (var reader = new StreamReader(settingsPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] option = line.Split('=');
                        if (option.Length < 2)
                        {
                            continue;
                        }

                        string name = option[0].Trim();
                        string value = option[1].Trim();

                        switch (name.ToLower())
                        {
                            case "titlebar.closecontrol.color":
                                closeControlColor = SublimeOverlayConfiguration.ParseColor(value);
                                break;
                            case "titlebar.maximizecontrol.color":
                                maximizeControlColor = SublimeOverlayConfiguration.ParseColor(value);
                                break;
                            case "titlebar.minimizecontrol.color":
                                minimizeControlColor = SublimeOverlayConfiguration.ParseColor(value);
                                break;
                            case "titlebar.color":
                                titleBarColor = SublimeOverlayConfiguration.ParseColor(value);
                                break;

                            case "sublimeexepath":
                                sublimeExePath = value;
                                break;

                            default:
                                break;
                        }
                    }
                }
            }

            return new SublimeOverlayConfiguration(
                closeControlColor,
                maximizeControlColor,
                minimizeControlColor,
                titleBarColor,
                sublimeExePath);
        }

        /// <summary>
        /// Parses a string in the format 'r g b' (ex. '127 50 234') and converts it to a <see cref="System.Drawing.Color"/>
        /// </summary>
        /// <param name="color">String representation of the color</param>
        /// <returns>Parsed color</returns>
        private static Color ParseColor(string color)
        {
            string[] values = color.Split(' ');
            if (values.Length < 3) return Color.White;


            bool result = int.TryParse(values[0], out int r);
            result = int.TryParse(values[1], out int g) && result;
            result = int.TryParse(values[2], out int b) && result;

            return result ? Color.FromArgb(r, g, b) : Color.White;
        }

        private SublimeOverlayConfiguration(
            Color closeControlColor,
            Color maximizeControlColor,
            Color minimizeControlColor,
            Color titleBarColor,
            string sublimeExePath)
        {
            this.CloseControlColor = closeControlColor;
            this.MaximizeControlColor = maximizeControlColor;
            this.MinimizeControlColor = minimizeControlColor;
            this.TitleBarColor = titleBarColor;
            this.SublimeExePath = sublimeExePath;
        }
    }
}
