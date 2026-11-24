using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using HR.BLL;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Immutable snapshot of the shared print configuration (rule 36 —
    /// configurable, never hardcoded): report logos on the right and left of
    /// the header, logo height, header/footer text, date / page-number /
    /// signature toggles. Values come from AppSettings (Print.* keys) so every
    /// screen and every report prints with one consistent header. Broken or
    /// missing image files are skipped silently (a report must never fail
    /// because of a logo); a small cache avoids re-reading image files per page.
    /// </summary>
    public class PrintProfile
    {
        /// <summary>Logos drawn at the right edge of the header, first one outermost.</summary>
        public List<Image> LogosRight;
        /// <summary>Logos drawn at the left edge of the header, first one outermost.</summary>
        public List<Image> LogosLeft;
        /// <summary>Logical height of every logo in pixels (24..120).</summary>
        public int LogoHeight;
        /// <summary>Extra centered header line (company name) under the title.</summary>
        public string HeaderText;
        /// <summary>Centered footer text; "Modern Time" when empty.</summary>
        public string FooterText;
        public bool ShowDate;
        public bool ShowPage;
        public bool ShowSignature;
        /// <summary>Label drawn over the signature line on the last page.</summary>
        public string SignatureText;

        public PrintProfile()
        {
            LogosRight = new List<Image>();
            LogosLeft = new List<Image>();
            LogoHeight = 48;
            SignatureText = "\u0627\u0644\u062a\u0648\u0642\u064a\u0639 \u0648\u0627\u0644\u0627\u0639\u062a\u0645\u0627\u062f";
        }
    }

    /// <summary>
    /// Loads and caches PrintProfiles from AppSettings. Settings keys:
    /// Print.LogoRight / Print.LogoLeft (semicolon-separated image paths),
    /// Print.LogoHeight, Print.HeaderText, Print.FooterText, Print.ShowDate,
    /// Print.ShowPage, Print.ShowSignature, Print.SignatureText.
    /// Saving is done through SettingsBLL (Create for new keys / Save for
    /// existing keys) so every change is permission-checked and audited.
    /// </summary>
    public static class PrintOptions
    {
        public const string KeyLogoRight = "Print.LogoRight";
        public const string KeyLogoLeft = "Print.LogoLeft";
        public const string KeyLogoHeight = "Print.LogoHeight";
        public const string KeyHeaderText = "Print.HeaderText";
        public const string KeyFooterText = "Print.FooterText";
        public const string KeyShowDate = "Print.ShowDate";
        public const string KeyShowPage = "Print.ShowPage";
        public const string KeyShowSignature = "Print.ShowSignature";
        public const string KeySignatureText = "Print.SignatureText";

        private static readonly Dictionary<string, Image> ImageCache =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Current configuration snapshot (settings read, images loaded best-effort).</summary>
        public static PrintProfile Snapshot()
        {
            PrintProfile profile = new PrintProfile();
            profile.LogosRight = LoadImages(SettingsBLL.GetString(KeyLogoRight, string.Empty));
            profile.LogosLeft = LoadImages(SettingsBLL.GetString(KeyLogoLeft, string.Empty));
            int height = SettingsBLL.GetInt(KeyLogoHeight, 48);
            if (height < 24 || height > 120)
            {
                height = 48;
            }
            profile.LogoHeight = height;
            profile.HeaderText = SettingsBLL.GetString(KeyHeaderText, string.Empty) ?? string.Empty;
            profile.FooterText = SettingsBLL.GetString(KeyFooterText, string.Empty) ?? string.Empty;
            profile.ShowDate = SettingsBLL.GetBool(KeyShowDate, true);
            profile.ShowPage = SettingsBLL.GetBool(KeyShowPage, true);
            profile.ShowSignature = SettingsBLL.GetBool(KeyShowSignature, false);
            string signature = SettingsBLL.GetString(KeySignatureText, string.Empty);
            profile.SignatureText = string.IsNullOrEmpty(signature)
                ? "\u0627\u0644\u062a\u0648\u0642\u064a\u0639 \u0648\u0627\u0644\u0627\u0639\u062a\u0645\u0627\u062f"
                : signature;
            return profile;
        }

        /// <summary>Drops cached images (after settings change) so files are re-read.</summary>
        public static void InvalidateCache()
        {
            lock (ImageCache)
            {
                foreach (Image image in ImageCache.Values)
                {
                    try
                    {
                        image.Dispose();
                    }
                    catch (Exception)
                    {
                        // a stale logo must never break the settings flow
                    }
                }
                ImageCache.Clear();
            }
        }

        /// <summary>Parses a semicolon-separated path list into loaded images (missing/broken files skipped). Also used by FrmPrintSettings for its live preview.</summary>
        public static List<Image> LoadImages(string settingValue)
        {
            List<Image> images = new List<Image>();
            if (string.IsNullOrEmpty(settingValue) || settingValue.Trim().Length == 0)
            {
                return images;
            }
            string[] parts = settingValue.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string path = parts[i] != null ? parts[i].Trim() : string.Empty;
                if (path.Length == 0 || !File.Exists(path))
                {
                    continue;
                }
                Image image = LoadImage(path);
                if (image != null)
                {
                    images.Add(image);
                }
            }
            return images;
        }

        /// <summary>Loads an image without locking the source file on disk.</summary>
        private static Image LoadImage(string path)
        {
            lock (ImageCache)
            {
                Image cached;
                if (ImageCache.TryGetValue(path, out cached))
                {
                    return cached;
                }
            }
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                MemoryStream stream = new MemoryStream(bytes); // kept alive by the Bitmap
                Image image = Image.FromStream(stream);
                lock (ImageCache)
                {
                    ImageCache[path] = image;
                }
                return image;
            }
            catch (Exception failure)
            {
                Logger.LogInfo("PrintOptions.LoadImage", "\u062a\u062d\u0645\u064a\u0644 \u0627\u0644\u0634\u0639\u0627\u0631 \u0641\u0634\u0644: " + path + " \u2014 " + failure.Message);
                return null;
            }
        }
    }
}
