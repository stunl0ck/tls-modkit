using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Stunl0ck.TLS.ModKit.Runtime;          // ItemDiskOverrides
using TheLastStand.Definition.Unit;         // BodyPartDefinition
using TheLastStand.Model.Unit;              // BodyPart

namespace Stunl0ck.TLS.ModKit.Hooks
{
    /// <summary>
    /// Intercept BodyPartView.GetSprite(BodyPart, faceId, gender, orientation)
    /// Build the exact Resources key via bodyPart.GetSpritePath(...) and, if a disk
    /// override exists in ItemDiskOverrides, return it. Adds verbose probe logs.
    /// </summary>
    [HarmonyPatch]
    internal static class BodyPartView_GetSprite_Hook
    {
        // Robust target finder: any static Sprite GetSprite(BodyPart, string, string, BodyPartDefinition.E_Orientation)
        static IEnumerable<MethodBase> TargetMethods()
        {
            // BodyPartView lives in the same assembly
            var asm = typeof(BodyPartDefinition).Assembly;
            foreach (var t in asm.GetTypes())
            {
                MethodInfo m = null;
                try
                {
                    m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                         .FirstOrDefault(mi =>
                             mi.Name == "GetSprite" &&
                             mi.ReturnType == typeof(Sprite) &&
                             mi.GetParameters().Length == 4 &&
                             mi.GetParameters()[0].ParameterType == typeof(BodyPart) &&
                             mi.GetParameters()[1].ParameterType == typeof(string) &&
                             mi.GetParameters()[2].ParameterType == typeof(string) &&
                             mi.GetParameters()[3].ParameterType == typeof(BodyPartDefinition.E_Orientation));
                }
                catch
                {
                    // ignore reflection / type-load issues and keep scanning
                }

                if (m != null)
                    yield return m;
            }
        }

        // Prefix: compute key exactly like vanilla, log it, and serve an override if present.
        static bool Prefix(
            BodyPart bodyPart,
            string faceId,
            string gender,
            BodyPartDefinition.E_Orientation orientation,
            ref Sprite __result)
        {
            // keep vanilla behavior if null
            if (bodyPart == null) return true;

            // vanilla early-out: hidden parts
            if (bodyPart.AdditionalConstraints != null &&
                bodyPart.AdditionalConstraints.Contains("Hide"))
            {
                return true; // vanilla will return null
            }

            string key = null;
            try
            {
                key = bodyPart.GetSpritePath(faceId, gender, orientation);
                key = NormalizeKey(key);
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogError($"[ModKit][BodyPart] GetSpritePath threw: {ex}");
                return true; // fall back to vanilla
            }

            if (string.IsNullOrEmpty(key))
                return true; // vanilla will return null

            if (ItemDiskOverrides.TryGet(key, out var custom) && custom)
            {
                __result = custom;
                var bpId = bodyPart.BodyPartDefinition?.Id ?? "(unknown)";
                var oStr = OrientationToString(orientation);

                Plugin.Log?.LogInfo($"[ModKit][BodyPart] Override HIT → key='{key}' bp='{bpId}' face='{faceId}' gender='{gender}' orient={oStr}");

                // also dump sizing so you can confirm PPU / pivot results
                var r = custom.rect;
                var ppu = custom.pixelsPerUnit;
                Plugin.Log?.LogInfo($"[ModKit][BodyPart] Override HIT → {key} | px=({r.width}x{r.height}) ppu={ppu:0.###} pivot=({custom.pivot.x:0.###},{custom.pivot.y:0.###})");
                return false; // skip vanilla ResourcePooler.LoadOnce
            }

            // miss → let vanilla load from Resources
            return true;
        }

        private static string NormalizeKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // strip accidental whitespace / newlines from XML
            return s.Trim().Replace("\r", "").Replace("\n", "").Replace("\t", "");
        }

        private static string OrientationToString(BodyPartDefinition.E_Orientation o)
        {
            // the enum is [Flags]; log something sensible for mixed values
            var parts = new List<string>(2);
            if ((o & BodyPartDefinition.E_Orientation.Front) == BodyPartDefinition.E_Orientation.Front) parts.Add("Front");
            if ((o & BodyPartDefinition.E_Orientation.Back)  == BodyPartDefinition.E_Orientation.Back)  parts.Add("Back");
            return parts.Count > 0 ? string.Join("|", parts) : o.ToString();
        }
    }
}
