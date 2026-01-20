using System;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx; // Paths.PluginPath
using UnityEngine;
using Stunl0ck.TLS.ModKit.Runtime;

namespace Stunl0ck.TLS.ModKit
{
    public partial class Plugin
    {
        /// <summary>
        /// Find <plugins>/<each_mod>/ModKit/Icons/*.png and register with ItemDiskOverrides (UI/Hand)
        /// or GlyphDiskOverrides (glyphs). Logs what it did.
        /// </summary>
        private static int LoadIconsFromAllMods()
        {
            int found = 0;
            var pluginsRoot = Paths.PluginPath;

            if (string.IsNullOrWhiteSpace(pluginsRoot) || !Directory.Exists(pluginsRoot))
            {
                Log?.LogInfo("[ModKit] Plugins path not found; skipping disk icon scan.");
                return 0;
            }

            // Filename patterns (no extension)
            var rxUi   = new Regex(@"^UI_Icon_Items_(?<id>[^_]+)_(?<kind>FG|BG)$",
                                   RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            var rxHand = new Regex(@"^Weapon_(?<id>.+)_(?<which>Front|Back)$",
                                   RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            var rxGlyph = new Regex(@"^Glyphs_Orbs_(?<gid>.+)$",
                                    RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            int LoadFrom(string root)
            {
                return DiskSpriteLoader.LoadAllSpritesInto(
                    root,
                    (nameOrId, sprite) =>
                    {
                        var fileName = sprite?.name ?? nameOrId ?? string.Empty;

                        // 1) Already a full Resources key? (advanced use)
                        if (fileName.StartsWith("View/", StringComparison.Ordinal))
                        {
                            ItemDiskOverrides.Set(fileName, sprite);
                            Log?.LogInfo($"[ModKit] (Direct) Item sprite registered → key='{fileName}' (from '{root}')");
                            return true;
                        }

                        // 2) UI icon: UI_Icon_Items_<ItemId>_(FG|BG)
                        var mUi = rxUi.Match(fileName);
                        if (mUi.Success)
                        {
                            var itemId = mUi.Groups["id"].Value;
                            var kind = mUi.Groups["kind"].Value.ToUpperInvariant(); // FG/BG
                            var folder = (kind == "FG") ? "Foreground" : "Background";
                            var key = $"View/Sprites/UI/Items/Icons/{folder}/UI_Icon_Items_{itemId}_{kind}";
                            ItemDiskOverrides.Set(key, sprite);
                            Log?.LogInfo($"[ModKit] (UI)    Item sprite registered → key='{key}' (id='{itemId}', kind={kind})");
                            return true;
                        }

                        // 3) Hand sprite: Weapon_<ItemId>_(Front|Back)
                        var mHand = rxHand.Match(fileName);
                        if (mHand.Success)
                        {
                            var itemId = mHand.Groups["id"].Value;
                            var which = mHand.Groups["which"].Value; // Front/Back
                            var key = $"View/Sprites/Units/PlayableUnits/BodyParts/Gear/Hand/{itemId}/Generic/Weapon_{itemId}_{which}";
                            ItemDiskOverrides.Set(key, sprite);
                            Log?.LogInfo($"[ModKit] (Hand)  Item sprite registered → key='{key}' (id='{itemId}', which={which})");
                            return true;
                        }

                        // 4) Glyphs (Glyphs_Orbs_<Id>.png)
                        var g = rxGlyph.Match(fileName);
                        if (g.Success)
                        {
                            var gid = g.Groups["gid"].Value;
                            GlyphDiskOverrides.Set(gid, sprite);
                            Log?.LogInfo($"[ModKit] (Glyph)  Disk icon registered → glyph='{gid}' (from '{root}')");
                            return true;
                        }

                        // 5) Fallback: treat as glyph id
                        GlyphDiskOverrides.Set(fileName, sprite);
                        Log?.LogWarning($"[ModKit] (Unknown) Registered as GLYPH id='{fileName}'. " +
                                        $"If meant for items, rename to UI_Icon_Items_<Id>_(FG|BG).png or Weapon_<Id>_(Front|Back).png");
                        return true;
                    }
                );
            }

            try
            {
                foreach (var modDir in Directory.EnumerateDirectories(pluginsRoot))
                    found += LoadFrom(modDir);
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[ModKit] Error scanning per-mod directories: {ex.Message}");
            }

            if (found == 0)
            {
                Log?.LogInfo(
                    $"[ModKit] No disk icons found. Put files under '<mod>/ModKit/Icons/*.png'. Examples:\n" +
                    $"  UI FG:   UI_Icon_Items_LightningBlade_FG.png\n" +
                    $"  UI BG:   UI_Icon_Items_LightningBlade_BG.png\n" +
                    $"  Front:   Weapon_LightningBlade_Front.png\n" +
                    $"  Back:    Weapon_LightningBlade_Back.png\n" +
                    $"  Glyph:   Glyphs_Orbs_Specialist.png"
                );
            }

            return found;
        }
    }
}
