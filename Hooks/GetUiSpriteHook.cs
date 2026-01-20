using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Stunl0ck.TLS.ModKit.Runtime; // ItemDiskOverrides
using TheLastStand.Definition.Meta.Glyphs; // only to locate the game assembly

namespace Stunl0ck.TLS.ModKit.Hooks
{
    /// <summary>
    /// Intercepts any static Sprite GetUiSprite(string itemDefinitionId, bool isBG)
    /// and returns your disk sprite if registered in ItemDiskOverrides under the
    /// exact resource key the game would build.
    /// </summary>
    [HarmonyPatch]
    internal static class GetUiSpriteHook
    {
        // Find all matching methods in the game assembly (robust against class name moves)
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
                             mi.Name == "GetUiSprite" &&
                             mi.ReturnType == typeof(Sprite) &&
                             mi.GetParameters().Length == 2 &&
                             mi.GetParameters()[0].ParameterType == typeof(string) &&
                             mi.GetParameters()[1].ParameterType == typeof(bool));
                }
                catch { /* ignore type load issues */ }

                if (m != null)
                    yield return m;
            }
        }

        // Prefix: build the same key vanilla will use; return our sprite if present
        static bool Prefix(string itemDefinitionId, bool isBG, ref Sprite __result)
        {
            if (string.IsNullOrEmpty(itemDefinitionId))
                return true;

            // EXACT casing & path the game uses
            var key = string.Concat(new string[] {
                "View/Sprites/UI/Items/Icons/",
                isBG ? "Background" : "Foreground",
                "/UI_Icon_Items_", itemDefinitionId, "_", (isBG ? "BG" : "FG")
            });

            // *** single probe log line ***
            // Plugin.Log?.LogInfo($"[ModKit][Icons] Probe id='{itemDefinitionId}' kind={(isBG ? "BG" : "FG")} → key='{key}'");

            if (ItemDiskOverrides.TryGet(key, out var custom) && custom)
            {
                __result = custom;
                Plugin.Log?.LogInfo($"[ModKit][Icons] Override HIT → {key}");
                return false; // skip vanilla load
            }

            return true; // fall back to vanilla
        }
    }
}
