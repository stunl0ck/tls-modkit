using System;
using System.IO;
using UnityEngine;

namespace Stunl0ck.TLS.ModKit.Runtime
{
    internal static class DiskSpriteLoader
    {
        // --------------------------
        // Public entry
        // --------------------------
        // Loads all PNGs in <pluginRoot>/ModKit/Icons/*.png as Sprites.
        // Calls 'onLoaded(key, sprite)' where:
        //   - Glyph orbs: key = glyphId (e.g., "Specialist")
        //   - Item icons / hand sprites: key = exact Resources key (casing matters)
        // Returns how many were successfully handed off to 'onLoaded'.
        public static int LoadAllSpritesInto(string pluginRoot, Func<string, Sprite, bool> onLoaded)
        {
            if (string.IsNullOrWhiteSpace(pluginRoot) || onLoaded == null) return 0;

            var dir = Path.Combine(pluginRoot, "ModKit", "Icons");
            if (!Directory.Exists(dir))
            {
                return 0;
            }

            int count = 0;
            foreach (var file in Directory.EnumerateFiles(dir, "*.png", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(file);
                var nameNoExt = Path.GetFileNameWithoutExtension(file);

                try
                {
                    // Map filename → key (+ kind for logging)
                    if (!TryMapFileNameToKey(nameNoExt, out var key, out var kind))
                    {
                        Plugin.Log?.LogWarning($"[ModKit] Icons: file '{fileName}' didn't match any known pattern " +
                                               "(Glyphs_Orbs_*, *_FG, *_BG, *_Front, *_Back, UI_Icon_Items_*). Skipped.");
                        continue;
                    }

                    // Read texture
                    var bytes = File.ReadAllBytes(file);
                    var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false, true);
                    if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: true))
                    {
                        UnityEngine.Object.Destroy(tex);
                        Plugin.Log?.LogWarning($"[ModKit] Failed to decode PNG: {fileName}");
                        continue;
                    }

                    tex.filterMode = FilterMode.Point;
                    tex.wrapMode   = TextureWrapMode.Clamp;

                    // Build sprite with correct PPU/pivot based on **key**
                    var sprite = CreateSpriteForKey(tex, key);
                    sprite.name = nameNoExt;

                    // Hand off to caller (they’ll register in ItemDiskOverrides/GlyphDiskOverrides)
                    if (onLoaded(key, sprite))
                    {
                        count++;
                        var r = sprite.rect;
                        var pivotPx = new Vector2(sprite.pivot.x * r.width, sprite.pivot.y * r.height);
                        Plugin.Log?.LogInfo(
                            $"[ModKit][Icons] Register key='{key}' ← file='{fileName}' " +
                            $"{(kind != null ? $"[{kind}]" : "")} | px=({(int)r.width}x{(int)r.height}) ppu={sprite.pixelsPerUnit:0.##} pivot=({pivotPx.x:0},{pivotPx.y:0})"
                        );
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[ModKit] Error loading icon '{fileName}': {ex}");
                }
            }

            Plugin.Log?.LogInfo($"[ModKit] Loaded {count} icon(s) from {dir}");
            return count;
        }

        // --------------------------
        // Sprite creation with correct calibration
        // --------------------------
        // Decide PPU & pivot based on the resource key we’ll register under.
        private static Sprite CreateSpriteForKey(Texture2D tex, string resourceKey)
        {
            int w = tex.width, h = tex.height;

            // Defaults (UI & Glyphs)
            float ppu = 100f;
            Vector2 pivot = new Vector2(0.5f, 0.5f); // center

            // If this is a hand-gear body part, use world-space pivots/PPU.
            // Example key:
            // "View/Sprites/Units/PlayableUnits/BodyParts/Gear/Hand/<ItemId>/Generic/Weapon_<ItemId>_Front"
            // or "..._Back"
            if (!string.IsNullOrEmpty(resourceKey) &&
                resourceKey.IndexOf("/BodyParts/Gear/Hand/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ppu = 28f;

                bool isFront = resourceKey.EndsWith("_Front", StringComparison.OrdinalIgnoreCase);
                bool isBack  = resourceKey.EndsWith("_Back",  StringComparison.OrdinalIgnoreCase);

                // Defaults tuned for TLS 64x64 hand sprites (adjust if your art uses different canvas)
                // Probes you printed showed approximately:
                //   Front → (29,36), Back → (30,26) on a 64×64
                var pivotPx = isFront
                    ? new Vector2(29f, 36f)
                    : new Vector2(30f, 26f);

                // If texture isn’t 64×64, scale pivot proportionally
                if (Math.Abs(w - 64) > 0.5f || Math.Abs(h - 64) > 0.5f)
                {
                    pivotPx = new Vector2(
                        pivotPx.x * (w / 64f),
                        pivotPx.y * (h / 64f)
                    );
                }

                // Convert pixel pivot → normalized pivot
                pivot = new Vector2(pivotPx.x / w, pivotPx.y / h);
            }

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, w, h),
                pivot,
                ppu,
                0,
                SpriteMeshType.FullRect
            );

            var kind = ClassifyKey(resourceKey);
            Plugin.Log?.LogInfo($"[ModKit][Create] '{resourceKey}' → px=({w}x{h}) ppu={ppu} pivot=({pivot.x * w:0},{pivot.y * h:0}) [{kind}]");
            return sprite;
        }

        // --------------------------
        // Filename → key mapping
        // --------------------------
        private static bool TryMapFileNameToKey(string name, out string key, out string kind)
        {
            key = null;
            kind = null;

            // 1) Glyph orbs: Glyphs_Orbs_<GlyphId>.png → glyph id
            if (name.StartsWith("Glyphs_Orbs_", StringComparison.OrdinalIgnoreCase))
            {
                var glyphId = name.Substring("Glyphs_Orbs_".Length);
                if (!string.IsNullOrWhiteSpace(glyphId))
                {
                    key  = glyphId;      // glyph pipeline uses id, not a Resources path
                    kind = "GlyphOrb";
                    return true;
                }
                return false;
            }

            // 2) Explicit UI prefix: UI_Icon_Items_<ItemId>_FG / _BG
            if (name.StartsWith("UI_Icon_Items_", StringComparison.Ordinal))
            {
                if (name.EndsWith("_FG", StringComparison.Ordinal))
                {
                    var itemId = name.Substring("UI_Icon_Items_".Length, name.Length - "UI_Icon_Items_".Length - 3);
                    key  = $"View/Sprites/UI/Items/Icons/Foreground/{name}";
                    kind = $"UI FG ({itemId})";
                    return true;
                }
                if (name.EndsWith("_BG", StringComparison.Ordinal))
                {
                    var itemId = name.Substring("UI_Icon_Items_".Length, name.Length - "UI_Icon_Items_".Length - 3);
                    key  = $"View/Sprites/UI/Items/Icons/Background/{name}";
                    kind = $"UI BG ({itemId})";
                    return true;
                }
            }

            // 3) Suffix-based: *_FG / *_BG / *_Front / *_Back
            if (name.EndsWith("_FG", StringComparison.Ordinal))
            {
                var itemId = name.Substring(0, name.Length - 3);
                key  = $"View/Sprites/UI/Items/Icons/Foreground/UI_Icon_Items_{itemId}_FG";
                kind = $"UI FG ({itemId})";
                return true;
            }
            if (name.EndsWith("_BG", StringComparison.Ordinal))
            {
                var itemId = name.Substring(0, name.Length - 3);
                key  = $"View/Sprites/UI/Items/Icons/Background/UI_Icon_Items_{itemId}_BG";
                kind = $"UI BG ({itemId})";
                return true;
            }
            if (name.EndsWith("_Front", StringComparison.Ordinal))
            {
                var itemId = name.Substring(0, name.Length - "_Front".Length);
                key  = $"View/Sprites/Units/PlayableUnits/BodyParts/Gear/Hand/{itemId}/Generic/Weapon_{itemId}_Front";
                kind = $"Hand Front ({itemId})";
                return true;
            }
            if (name.EndsWith("_Back", StringComparison.Ordinal))
            {
                var itemId = name.Substring(0, name.Length - "_Back".Length);
                key  = $"View/Sprites/Units/PlayableUnits/BodyParts/Gear/Hand/{itemId}/Generic/Weapon_{itemId}_Back";
                kind = $"Hand Back ({itemId})";
                return true;
            }

            return false;
        }

        // Small human-readable tag for the Create log
        private static string ClassifyKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Unknown";
            if (!key.StartsWith("View/", StringComparison.OrdinalIgnoreCase)) return "Glyph";
            if (key.IndexOf("/BodyParts/Gear/Hand/", StringComparison.OrdinalIgnoreCase) >= 0)
                return key.EndsWith("_Front", StringComparison.OrdinalIgnoreCase) ? "Hand Front" : "Hand Back";
            if (key.IndexOf("/Icons/Foreground/", StringComparison.OrdinalIgnoreCase) >= 0) return "UI FG";
            if (key.IndexOf("/Icons/Background/", StringComparison.OrdinalIgnoreCase) >= 0) return "UI BG";
            return "Resources";
        }
    }
}
