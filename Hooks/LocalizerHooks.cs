using System;
using BepInEx;
using HarmonyLib;
using Stunl0ck.TLS.Shared;
using TPLib.Localization;

namespace Stunl0ck.TLS.ModKit.Hooks
{
    internal static class LocalizerHooks
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localizer), "LoadDictionary")]
        private static void LoadDictionary_Postfix(object value)
        {
            try
            {
                var log = new Localization.Logger(
                    info: s => Plugin.Log?.LogInfo(s),
                    warn: s => Plugin.Log?.LogWarning(s),
                    error: s => Plugin.Log?.LogError(s));

                Plugin.Log?.LogInfo("[ModKit] Localizer.LoadDictionary completed; merging ModKit/languages.csv from plugins.");
                Localization.MergeCsvsUnder(Paths.PluginPath, "ModKit/languages.csv", log);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[ModKit] Localizer.LoadDictionary postfix failed: {ex}");
            }
        }
    }
}
