using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using TheLastStand.Controller.Modding.Module;
using Stunl0ck.TLS.Shared;
using Stunl0ck.TLS.ModKit.Runtime;

namespace Stunl0ck.TLS.ModKit
{
    [BepInPlugin(ModId, ModName, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModId = "com.tls.modkit";
        public const string ModName = "TLS ModKit";
        public const string Version = "1.0.0";

        public static ManualLogSource Log { get; private set; }
        private static bool s_initialized;

        private void Awake()
        {
            Log = Logger;

            if (s_initialized)
            {
                Log.LogInfo("[ModKit] Already initialized, skipping.");
                return;
            }
            s_initialized = true;

            // --- Load disk PNG icons from EVERY plugin folder under BepInEx/plugins/* ---
            int loadedTotal = LoadIconsFromAllMods();
            Log?.LogInfo($"[ModKit] Disk icon load complete. Total loaded: {loadedTotal}.");

            // --- Harmony patches ---
            Harmony.CreateAndPatchAll(typeof(Hooks.GlyphDatabaseHooks));   // DB-ready hook
            Harmony.CreateAndPatchAll(typeof(Hooks.GetGlyphIconHook));     // glyph icon override hook
            Harmony.CreateAndPatchAll(typeof(Hooks.PerkDatabaseHooks));   
            Harmony.CreateAndPatchAll(typeof(Hooks.PerkCtorHook)); 
            Harmony.CreateAndPatchAll(typeof(Hooks.LocalizationHooks));

            Log.LogInfo("[ModKit] Plugin initialized.");
        }

        /// <summary>
        /// Find <plugins>/<each_mod>/ModKit/Icons/*.png and register with GlyphDiskOverrides.
        /// </summary>
        private static int LoadIconsFromAllMods()
        {
            int found = 0;
            var pluginsRoot = Paths.PluginPath;

            if (string.IsNullOrWhiteSpace(pluginsRoot) || !Directory.Exists(pluginsRoot))
            {
                Log?.LogInfo("[ModKit] Plugins path not found; skipping disk icon scan.");
                return 0;
            }

            // Loader for <root>/ModKit/Icons
            int LoadFrom(string root)
            {
                return DiskSpriteLoader.LoadAllSpritesInto(
                    root,
                    (id, sprite) =>
                    {
                        GlyphDiskOverrides.Set(id, sprite);
                        Log?.LogInfo($"[ModKit] Disk icon registered for glyph '{id}' (sprite='{sprite.name}') from '{root}'.");
                        return true;
                    }
                );
            }

            // Only per-mod folders: <plugins>/<mod>/
            try
            {
                foreach (var modDir in Directory.EnumerateDirectories(pluginsRoot))
                    found += LoadFrom(modDir);
            }
            catch (System.Exception ex)
            {
                Log?.LogWarning($"[ModKit] Error scanning per-mod directories: {ex.Message}");
            }

            if (found == 0)
            {
                Log?.LogInfo(
                    $"[ModKit] No disk icons found. Place files under '<mod>/ModKit/Icons/*.png'. " +
                    $"Example: '{Path.Combine(pluginsRoot, "OmenOfSpecialists", "ModKit", "Icons", "Glyphs_Orbs_Specialist.png")}'"
                );
            }

            return found;
        }
    }
}
