// Skills hook: run after vanilla skills are loaded
using HarmonyLib;
using System.Xml.Linq;
using TheLastStand.Database;
using TheLastStand.Database.Unit;

namespace Stunl0ck.TLS.ModKit.Hooks
{
    internal static class SkillDatabaseHooks
    {
        static bool _appliedOnce;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SkillDatabase), nameof(SkillDatabase.Deserialize), new[] { typeof(XContainer) })]
        static void Postfix()
        {
            if (_appliedOnce) return;
            _appliedOnce = true;

            if (SkillDatabase.SkillDefinitions == null)
            {
                Plugin.Log?.LogWarning("[ModKit] SkillDefinitions is null; skipping skill patches.");
                return;
            }

            Plugin.Log?.LogInfo($"[ModKit] Skills ready ({SkillDatabase.SkillDefinitions.Count}). Applying skill patches…");
            PatchEngine.Apply("SkillDefinition");
            Plugin.Log?.LogInfo($"[ModKit] Skill patch pass done. Total: {SkillDatabase.SkillDefinitions.Count}.");
        }
    }
}
