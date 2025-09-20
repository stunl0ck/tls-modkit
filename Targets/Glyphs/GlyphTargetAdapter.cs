using Stunl0ck.TLS.ModKit.DSL;
using Stunl0ck.TLS.ModKit.Runtime;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using TheLastStand.Database.Meta;
using TheLastStand.Definition.Meta.Glyphs;

namespace Stunl0ck.TLS.ModKit.Targets.Glyphs
{
    internal sealed class GlyphTargetAdapter : ITargetAdapter
    {
        public string TargetId => "GlyphDefinition";
        public string DataFolderName => "Glyphs";
        string iconOverridePath = null;

        public void ApplyAdd(XElement definitionElement, string sourceFile, bool replace)
        {
            if (definitionElement == null)
            {
                Plugin.Log?.LogWarning($"[ModKit][Glyphs] {sourceFile}: <Definition> was null.");
                return;
            }

            GlyphDefinition parsed;
            try
            {
                ResolveMcmTokens(definitionElement);
                // Capture <IconOverride Path="..."/> before constructing the native object.
                iconOverridePath = TryReadIconOverridePath(definitionElement);

                // Use the game’s real parser (constructor deserializes).
                parsed = new GlyphDefinition(definitionElement, null);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ModKit][Glyphs] {sourceFile}: failed to parse GlyphDefinition XML: {ex}");
                return;
            }

            var id = parsed?.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                Plugin.Log?.LogWarning($"[ModKit][Glyphs] {sourceFile}: parsed glyph has no Id; skipping.");
                return;
            }

            var map = GlyphDatabase.GlyphDefinitions;
            if (map == null)
            {
                Plugin.Log?.LogWarning($"[ModKit][Glyphs] {sourceFile}: GlyphDefinitions map is null; skipping.");
                return;
            }

            if (map.ContainsKey(id))
            {
                if (!replace)
                {
                    Plugin.Log?.LogWarning($"[ModKit][Glyphs] {sourceFile}: Id '{id}' already exists. Use action='replace' to override.");
                    return;
                }
                map[id] = parsed;
                Plugin.Log?.LogInfo($"[ModKit][Glyphs] Replaced glyph '{id}'.");
            }
            else
            {
                map.Add(id, parsed);
                Plugin.Log?.LogInfo($"[ModKit][Glyphs] Added glyph '{id}'.");
            }

            // Update runtime icon override registry based on presence of <IconOverride>.
            if (!string.IsNullOrWhiteSpace(iconOverridePath))
            {
                GlyphIconOverrides.Set(id, iconOverridePath);
                Plugin.Log?.LogInfo($"[ModKit][Glyphs] IconOverride set for '{id}' -> '{iconOverridePath}'.");
            }
            else
            {
                // If author omitted IconOverride in add/replace, clear any previous override.
                if (GlyphIconOverrides.Remove(id))
                    Plugin.Log?.LogInfo($"[ModKit][Glyphs] IconOverride cleared for '{id}'.");
            }

        }

        public void ApplyEdit(string id, IReadOnlyList<PatchOperation> operations, string sourceFile)
        {
            // v1 stub: log and skip. Implement once DSL ops are ready.
            Plugin.Log?.LogWarning($"[ModKit][Glyphs] {sourceFile}: 'edit' for '{id}' not implemented yet (skipping).");
        }

        public void ApplyRemove(string id, string sourceFile)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Plugin.Log?.LogWarning($"[ModKit][Glyphs] {sourceFile}: remove requires a non-empty id.");
                return;
            }

            var map = GlyphDatabase.GlyphDefinitions;
            if (map == null)
            {
                Plugin.Log?.LogWarning($"[ModKit][Glyphs] {sourceFile}: GlyphDefinitions map is null; skipping remove '{id}'.");
                return;
            }

            if (map.Remove(id))
            {
                Plugin.Log?.LogInfo($"[ModKit][Glyphs] Removed glyph '{id}'.");
                // Also clear any runtime icon override for this id.
                if (GlyphIconOverrides.Remove(id))
                    Plugin.Log?.LogInfo($"[ModKit][Glyphs] IconOverride cleared for '{id}' (on remove).");
            }
            else
            {
                // idempotent: nothing to remove
                Plugin.Log?.LogInfo($"[ModKit][Glyphs] Remove '{id}': not found (no-op).");
            }
        }

        // Pseudocode: call this before constructing the GlyphDefinition
        static void ResolveMcmTokens(XElement elem)
        {
            foreach (var attr in elem.DescendantsAndSelf().Attributes())
            {
                var s = attr.Value?.Trim();
                if (s != null && s.StartsWith("${MCM:", StringComparison.Ordinal))
                {
                    var payload = s.Substring(6, s.Length - 7); // inside ${MCM:...}
                    // expected form: com.omenofspecialists.mod/SlotsCost
                    var slash = payload.IndexOf('/');
                    var modId = slash > 0 ? payload.Substring(0, slash) : payload;
                    var key   = slash > 0 ? payload.Substring(slash + 1) : "";

                    var val = Stunl0ck.TLS.ModKit.McmShim.GetString(modId, key, "0");
                    attr.Value = string.IsNullOrEmpty(val) ? "0" : val;


                    // var val   = MCM.GetValue<string>(modId, key, "0");
                    // attr.Value = string.IsNullOrEmpty(val) ? "0" : val;
                }
            }
        }

        // Minimal helper: find <IconOverride Path="..."/> in any case and return its Path.
        static string TryReadIconOverridePath(XElement definitionElement)
        {
            if (definitionElement == null) return null;
            // We expect <GlyphDefinition> ... <IconOverride Path="..."/> ... </GlyphDefinition>
            foreach (var e in definitionElement.Descendants())
            {
                if (string.Equals(e.Name.LocalName, "IconOverride", StringComparison.OrdinalIgnoreCase))
                {
                    var a = e.Attribute("Path");
                    var v = a?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(v))
                        return v;
                }
            }
            return null;
        }
    }
}
