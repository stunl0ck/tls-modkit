using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Stunl0ck.TLS.ModKit.DSL;
using TheLastStand.Database;
using TheLastStand.Database.Unit;
using TheLastStand.Definition.Skill;

namespace Stunl0ck.TLS.ModKit.Targets.Skills
{
    internal sealed class SkillTargetAdapter : ITargetAdapter
    {
        public string TargetId => "SkillDefinition";
        public string DataFolderName => "Skills";

        public void ApplyAdd(XElement definitionElement, string sourceFile, bool replace)
        {
            if (definitionElement == null)
            {
                Plugin.Log?.LogWarning($"[ModKit][Skills] {sourceFile}: <Definition> null.");
                return;
            }

            SkillDefinition parsed;
            try
            {
                // Use the game’s constructor so its Deserialize() runs.
                parsed = new SkillDefinition(definitionElement);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ModKit][Skills] {sourceFile}: parse failed: {ex}");
                return;
            }

            var id = parsed?.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                Plugin.Log?.LogWarning($"[ModKit][Skills] {sourceFile}: parsed skill has no Id; skipping.");
                return;
            }

            var map = SkillDatabase.SkillDefinitions;
            if (map == null)
            {
                Plugin.Log?.LogWarning($"[ModKit][Skills] {sourceFile}: SkillDefinitions map is null; skipping.");
                return;
            }

            if (map.ContainsKey(id))
            {
                if (!replace)
                {
                    Plugin.Log?.LogWarning($"[ModKit][Skills] {sourceFile}: Id '{id}' exists. Use action='replace'.");
                    return;
                }
                map[id] = parsed;
                Plugin.Log?.LogInfo($"[ModKit][Skills] Replaced '{id}'.");
            }
            else
            {
                map.Add(id, parsed);
                Plugin.Log?.LogInfo($"[ModKit][Skills] Added '{id}'.");
            }
        }

        public void ApplyEdit(string id, IReadOnlyList<PatchOperation> ops, string src)
            => Plugin.Log?.LogWarning($"[ModKit][Skills] {src}: edit not implemented.");

        public void ApplyRemove(string id, string src)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Plugin.Log?.LogWarning($"[ModKit][Skills] {src}: remove needs id.");
                return;
            }
            if (SkillDatabase.SkillDefinitions?.Remove(id) == true)
                Plugin.Log?.LogInfo($"[ModKit][Skills] Removed '{id}'.");
            else
                Plugin.Log?.LogInfo($"[ModKit][Skills] Remove '{id}': not found (no-op).");
        }
    }
}
