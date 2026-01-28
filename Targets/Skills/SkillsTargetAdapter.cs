using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Plugin.Log?.LogWarning($"[ModKit][Skills] {src}: edit requires a non-empty id.");
                return;
            }

            if (ops == null || ops.Count == 0)
            {
                Plugin.Log?.LogWarning($"[ModKit][Skills] {src}: no operations provided for '{id}'.");
                return;
            }

            var map = SkillDatabase.SkillDefinitions;
            if (map == null)
            {
                Plugin.Log?.LogWarning($"[ModKit][Skills] {src}: SkillDefinitions map is null; skipping edit '{id}'.");
                return;
            }

            if (!map.TryGetValue(id, out var skill) || skill == null)
            {
                Plugin.Log?.LogInfo($"[ModKit][Skills] Edit '{id}': not found (no-op).");
                return;
            }

            int applied = 0;
            foreach (var op in ops)
            {
                if (op == null) continue;

                if (op.Kind != PatchOpKind.Set)
                {
                    Plugin.Log?.LogWarning($"[ModKit][Skills] {src}: unsupported op '{op.Kind}' for '{id}' (only 'Set' supported). Skipping.");
                    continue;
                }

                var sel = (op.Select ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(sel))
                {
                    Plugin.Log?.LogWarning($"[ModKit][Skills] {src}: Set missing 'select' for '{id}'. Skipping.");
                    continue;
                }

                // Backward-friendly: tolerate "token:Foo" and treat it like "Foo"
                if (sel.StartsWith("token:", StringComparison.OrdinalIgnoreCase))
                    sel = sel.Substring("token:".Length).Trim();

                // Common alias used by people guessing
                if (string.Equals(sel, "UsesPerTurn", StringComparison.OrdinalIgnoreCase))
                    sel = "UsesPerTurnCount";

                // Support dotted paths for nested objects, e.g. "Range.CardinalDirectionOnly"
                var path = sel.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (path.Length == 0) continue;

                object target = skill;
                for (int i = 0; i < path.Length - 1; i++)
                {
                    if (!TryGetMemberValue(target, path[i], out var next) || next == null)
                    {
                        target = null;
                        break;
                    }
                    target = next;
                }
                if (target == null)
                {
                    Plugin.Log?.LogWarning($"[ModKit][Skills] {src}: edit '{id}' path '{sel}' not found (no-op).");
                    continue;
                }

                var leaf = path[path.Length - 1];
                if (TrySetMemberValue(target, leaf, op.Value, out var error))
                {
                    applied++;
                }
                else
                {
                    Plugin.Log?.LogWarning($"[ModKit][Skills] {src}: edit '{id}' failed to set '{sel}': {error}");
                }
            }

            if (applied > 0)
                Plugin.Log?.LogInfo($"[ModKit][Skills] Applied {applied} edit(s) to '{id}'.");
        }

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

        private static bool TryGetMemberValue(object target, string name, out object value)
        {
            value = null;
            if (target == null || string.IsNullOrWhiteSpace(name)) return false;

            var t = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var prop = t.GetProperties(flags)
                .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (prop != null && prop.GetIndexParameters().Length == 0)
            {
                try
                {
                    value = prop.GetValue(target);
                    return true;
                }
                catch { return false; }
            }

            var field = t.GetFields(flags)
                .FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (field != null)
            {
                try
                {
                    value = field.GetValue(target);
                    return true;
                }
                catch { return false; }
            }

            return false;
        }

        private static bool TrySetMemberValue(object target, string name, string raw, out string error)
        {
            error = null;
            if (target == null) { error = "target is null"; return false; }
            if (string.IsNullOrWhiteSpace(name)) { error = "member name is empty"; return false; }

            var t = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Prefer property (even if setter is non-public)
            var prop = t.GetProperties(flags)
                .FirstOrDefault(p => p.GetIndexParameters().Length == 0 &&
                                     string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

            if (prop != null)
            {
                var canWrite = prop.SetMethod != null; // includes non-public
                if (canWrite)
                {
                    if (!TryConvert(raw, prop.PropertyType, out var converted, out error))
                        return false;
                    try
                    {
                        prop.SetValue(target, converted);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        return false;
                    }
                }

                // Auto-property with private setter? Try backing field.
                var backing = t.GetField($"<{prop.Name}>k__BackingField", flags);
                if (backing != null)
                {
                    if (!TryConvert(raw, backing.FieldType, out var converted, out error))
                        return false;
                    try
                    {
                        backing.SetValue(target, converted);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        return false;
                    }
                }
            }

            // Fall back to field
            var field = t.GetFields(flags)
                .FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (field != null)
            {
                if (!TryConvert(raw, field.FieldType, out var converted, out error))
                    return false;
                try
                {
                    field.SetValue(target, converted);
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            error = $"no field/property '{name}' on {t.Name}";
            return false;
        }

        private static bool TryConvert(string raw, Type targetType, out object converted, out string error)
        {
            converted = null;
            error = null;

            if (targetType == typeof(string))
            {
                converted = raw;
                return true;
            }

            if (raw == null)
            {
                // Allow null only for reference types / nullable
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                    return true;
                error = "value is null";
                return false;
            }

            var nonNullType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            try
            {
                if (nonNullType == typeof(bool))
                {
                    if (bool.TryParse(raw, out var b)) { converted = b; return true; }
                    if (raw == "0") { converted = false; return true; }
                    if (raw == "1") { converted = true; return true; }
                    error = $"cannot parse bool '{raw}'";
                    return false;
                }

                if (nonNullType == typeof(int))
                {
                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    { converted = i; return true; }
                    error = $"cannot parse int '{raw}'";
                    return false;
                }

                if (nonNullType == typeof(float))
                {
                    if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    { converted = f; return true; }
                    error = $"cannot parse float '{raw}'";
                    return false;
                }

                if (nonNullType == typeof(double))
                {
                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    { converted = d; return true; }
                    error = $"cannot parse double '{raw}'";
                    return false;
                }

                if (nonNullType.IsEnum)
                {
                    converted = Enum.Parse(nonNullType, raw, ignoreCase: true);
                    return true;
                }

                // Last resort
                converted = Convert.ChangeType(raw, nonNullType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
