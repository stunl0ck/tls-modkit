using System;
using System.IO;
using UnityEngine;

namespace Stunl0ck.TLS.ModKit.Runtime
{
    internal static class DiskSpriteLoader
    {
        // Loads all PNGs in <pluginRoot>/ModKit/Icons/*.png as Sprites.
        // Returns how many were loaded.
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
                try
                {
                    var bytes = File.ReadAllBytes(file);
                    var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false, true);
                    if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: true))
                    {
                        UnityEngine.Object.Destroy(tex);
                        Plugin.Log?.LogWarning($"[ModKit] Failed to decode PNG: {Path.GetFileName(file)}");
                        continue;
                    }

                    tex.filterMode = FilterMode.Point;
                    tex.wrapMode   = TextureWrapMode.Clamp;

                    // Expect 64x64 canvas (your template). If not, we still create a sprite; UI will scale it.
                    var w = tex.width;
                    var h = tex.height;

                    var sprite = Sprite.Create(
                        tex,
                        new Rect(0, 0, w, h),
                        new Vector2(0.5f, 0.5f), // pivot center
                        100f,                    // PPU to match vanilla
                        0,
                        SpriteMeshType.FullRect
                    );
                    sprite.name = Path.GetFileNameWithoutExtension(file);

                    // Name convention: Glyphs_Orbs_<Id>.png  -> glyph Id = <Id>
                    var name = sprite.name;
                    var id = name.StartsWith("Glyphs_Orbs_", StringComparison.OrdinalIgnoreCase)
                        ? name.Substring("Glyphs_Orbs_".Length)
                        : name; // fallback: whole name is Id

                    if (onLoaded(id, sprite)) count++;
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[ModKit] Error loading icon '{file}': {ex}");
                }
            }

            Plugin.Log?.LogInfo($"[ModKit] Loaded {count} icon(s) from {dir}");
            return count;
        }
    }
}
