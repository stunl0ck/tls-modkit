using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BepInEx;
using Stunl0ck.TLS.ModKit.DSL;
using Stunl0ck.TLS.ModKit.Targets;

namespace Stunl0ck.TLS.ModKit
{
    /// <summary>
    /// Scans ModKit patch files and applies them to a specific target (e.g., "GlyphDefinition").
    /// v1 supports actions: add, replace, edit, remove (adapters may implement a subset).
    /// </summary>
    internal static class PatchEngine
    {
        private const string ModKitFolderName = "ModKit";
        private const string PatchGlob = "*.patch.xml";

        /// <summary>
        /// Apply all patches targeting <paramref name="targetId"/> (e.g., "GlyphDefinition").
        /// Returns the number of patch files processed for this target.
        /// </summary>
        public static int Apply(string targetId)
        {
            var adapter = Registry.Resolve(targetId);
            if (adapter == null)
            {
                Plugin.Log?.LogWarning($"[ModKit] No adapter registered for target '{targetId}'. Skipping.");
                return 0;
            }

            // Which on-disk category are we scanning? (e.g., Glyphs, Perks, Races)
            var category = adapter.DataFolderName;
            if (string.IsNullOrWhiteSpace(category))
            {
                Plugin.Log?.LogWarning($"[ModKit] Adapter for '{targetId}' did not provide DataFolderName. Skipping.");
                return 0;
            }

            var files = EnumeratePatchFiles(category).OrderBy(SortKey).ToList();
            if (files.Count == 0)
            {
                Plugin.Log?.LogInfo($"[ModKit] No patch files found for target '{targetId}' under {ModKitFolderName}/{category}.");
                return 0;
            }

            int applied = 0;
            foreach (var path in files)
            {
                try
                {
                    var doc = PatchDocument.Load(path);
                    if (!IsForTarget(doc, targetId))
                    {
                        // Allow category-driven discovery even if <Patch target="..."> was omitted/mismatched.
                        // We only enforce the scanner's category; log at verbose level.
                        // Plugin.Log?.LogDebug($"[ModKit] Skipping {Short(path)}: target mismatch ({doc?.Target} != {targetId}).");
                        continue;
                    }

                    var action = (doc.Action ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "add":
                        case "replace":
                            if (doc.DefinitionElement == null)
                            {
                                Plugin.Log?.LogWarning($"[ModKit] {Short(path)}: missing <Definition> for '{action}'. Skipping.");
                                break;
                            }
                            bool replace = action == "replace";
                            adapter.ApplyAdd(doc.DefinitionElement, path, replace);
                            applied++;
                            Plugin.Log?.LogInfo($"[ModKit] [APPLY] {action} → {Short(path)}");
                            break;

                        case "edit":
                            if (string.IsNullOrWhiteSpace(doc.Id))
                            {
                                Plugin.Log?.LogWarning($"[ModKit] {Short(path)}: 'edit' requires an 'id' attribute. Skipping.");
                                break;
                            }
                            adapter.ApplyEdit(doc.Id, doc.Operations ?? Array.Empty<PatchOperation>(), path);
                            applied++;
                            Plugin.Log?.LogInfo($"[ModKit] [APPLY] edit(id='{doc.Id}') → {Short(path)}");
                            break;

                        case "remove":
                            if (string.IsNullOrWhiteSpace(doc.Id))
                            {
                                Plugin.Log?.LogWarning($"[ModKit] {Short(path)}: 'remove' requires an 'id' attribute. Skipping.");
                                break;
                            }
                            adapter.ApplyRemove(doc.Id, path);
                            applied++;
                            Plugin.Log?.LogInfo($"[ModKit] [APPLY] remove(id='{doc.Id}') → {Short(path)}");
                            break;

                        default:
                            Plugin.Log?.LogWarning($"[ModKit] {Short(path)}: unknown or missing action '{doc?.Action}'. Expected add|replace|edit|remove.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[ModKit] Failed to apply patch file {Short(path)}: {ex}");
                }
            }

            return applied;
        }

        /// <summary>
        /// Enumerate all candidate patch files for a category under BepInEx/plugins/*/ModKit/{category}.
        /// </summary>
        private static IEnumerable<string> EnumeratePatchFiles(string categoryFolder)
        {
            var pluginsRoot = Paths.PluginPath;
            if (string.IsNullOrWhiteSpace(pluginsRoot) || !Directory.Exists(pluginsRoot))
                yield break;

            foreach (var pluginDir in Directory.GetDirectories(pluginsRoot))
            {
                var catDir = Path.Combine(pluginDir, ModKitFolderName, categoryFolder);
                if (!Directory.Exists(catDir))
                    continue;

                // recurse to allow authors to organize subfolders under the category
                string[] files = Array.Empty<string>();
                try
                {
                    files = Directory.GetFiles(catDir, PatchGlob, SearchOption.AllDirectories);
                }
                catch { /* ignore IO errors and continue */ }

                foreach (var f in files)
                    yield return f;
            }
        }

        /// <summary>
        /// Sort key: numeric prefix (e.g. "10_", "020-") ascending, then full path ordinal ignore-case.
        /// </summary>
        private static (int prefix, string path) SortKey(string path)
        {
            var file = Path.GetFileName(path);
            int p = int.MaxValue;

            if (!string.IsNullOrEmpty(file))
            {
                int i = 0, val = 0, digits = 0;
                while (i < file.Length && char.IsDigit(file[i]) && digits < 9) // cap to avoid overflow
                {
                    val = (val * 10) + (file[i] - '0');
                    i++; digits++;
                }
                if (digits > 0) p = val;
            }

            return (p, path.ToLowerInvariant());
        }

        private static bool IsForTarget(PatchDocument doc, string targetId)
        {
            if (doc == null) return false;
            if (string.IsNullOrWhiteSpace(doc.Target)) return true; // tolerate missing 'target' if discovered by category
            return string.Equals(doc.Target, targetId, StringComparison.OrdinalIgnoreCase);
        }

        private static string Short(string path)
            => $"{Path.GetFileName(Path.GetDirectoryName(path))}/{Path.GetFileName(path)}";
    }
}
