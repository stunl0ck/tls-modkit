using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using TheLastStand.Definition.Meta.Glyphs;
using TheLastStand.Framework;
using Stunl0ck.TLS.ModKit.Runtime;

namespace Stunl0ck.TLS.ModKit.Hooks
{
    /// <summary>
    /// Postfix for GetGlyphIcon(GlyphDefinition):
    /// 1) If a disk-loaded sprite exists for this glyph id, return it.
    /// 2) Else, if an IconOverride path is registered, try loading that sprite.
    /// 3) Else, keep the game’s original result.
    /// Assumes any disk PNGs are authored on a 64×64 canvas with padding baked in.
    /// </summary>
    [HarmonyPatch]
    internal static class GetGlyphIconHook
    {
        // We don't assume the declaring type; find any static Sprite GetGlyphIcon(GlyphDefinition)
        static IEnumerable<MethodBase> TargetMethods()
        {
            var asm = typeof(GlyphDefinition).Assembly; // TheLastStand.dll
            foreach (var t in asm.GetTypes())
            {
                MethodInfo m = null;
                try
                {
                    m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                         .FirstOrDefault(mi =>
                             mi.Name == "GetGlyphIcon" &&
                             mi.ReturnType == typeof(Sprite) &&
                             mi.GetParameters().Length == 1 &&
                             mi.GetParameters()[0].ParameterType == typeof(GlyphDefinition));
                }
                catch
                {
                    // ignore reflection/type load issues and keep scanning
                }

                if (m != null)
                    yield return m;
            }
        }

        static void Postfix(GlyphDefinition glyphDefinition, ref Sprite __result)
        {
            if (glyphDefinition == null)
                return;

            var id = glyphDefinition.Id;

            // 1) Disk-loaded sprite wins (placed under ModKit/Icons, loaded at plugin start)
            if (GlyphDiskOverrides.TryGet(id, out var diskSprite) && diskSprite)
            {
                __result = diskSprite;
                return;
            }

            // 2) Optional: string-path override to a Resources key (must already match orb sizing)
            // e.g. <IconOverride Path=view\sprites\ui\meta\glyphs\orbs\Glyphs_Orbs_Easy_WealthyHaven"/>
            //  works with Assets\Resources\, Assets\Resources\views, etc.
            if (GlyphIconOverrides.TryGet(id, out var rawPath) && !string.IsNullOrWhiteSpace(rawPath))
            {
                var normalized = GlyphIconOverrides.Normalize(rawPath);
                var sprite = ResourcePooler.LoadOnce<Sprite>(normalized, true);
                if (sprite)
                {
                    __result = sprite;
                    return;
                }

                Plugin.Log?.LogWarning($"[ModKit] IconOverride path '{rawPath}' failed to load for glyph '{id}'. Using original.");
            }

            // 3) Otherwise, leave the game’s original __result unchanged.
        }
    }
}
