using HarmonyLib;
using System.Xml.Linq;
using TheLastStand.Database.Meta;

namespace Stunl0ck.TLS.ModKit.Hooks
{
    // Tiny hook: when Glyph DB finishes loading, run the ModKit glyph patch pass.
    internal static class GlyphDatabaseHooks
    {
        private static bool _appliedOnce;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GlyphDatabase), nameof(GlyphDatabase.Deserialize), new[] { typeof(XContainer) })]
        private static void Postfix(GlyphDatabase __instance, XContainer container)
        {
            if (_appliedOnce) return; // avoid re-applying on scene reloads
            _appliedOnce = true;

            var defs = GlyphDatabase.GlyphDefinitions;
            if (defs == null)
            {
                Plugin.Log?.LogWarning("[ModKit] GlyphDatabase ready but GlyphDefinitions is null; skipping glyph patches.");
                return;
            }

            Plugin.Log?.LogInfo($"[ModKit] GlyphDatabase ready ({defs.Count} base glyphs). Applying ModKit glyph patches…");
            try
            {
                PatchEngine.Apply("GlyphDefinition"); // v1: Apply only glyph target
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogError($"[ModKit] Glyph patching failed: {ex}");
            }

            Plugin.Log?.LogInfo($"[ModKit] Glyph patch pass complete. Now {GlyphDatabase.GlyphDefinitions?.Count ?? 0} glyphs loaded.");
        }
    }
}
