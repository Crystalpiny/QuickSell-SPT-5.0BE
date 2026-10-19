using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace QuickSell.Patches
{
    /// <summary>
    /// 5.0BE 的 IL2CPP interop 把 EFT 的私有字段暴露成属性（名字带下划线），
    /// 4.x 的 Compat 只找字段，所以这里同时找属性/字段，优先属性。
    /// </summary>
    internal static class Compat
    {
        private static readonly Dictionary<(Type Owner, Type Value), MemberInfo> Cache = new();

        private const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static MemberInfo Find(Type owner, Type valueType, string preferredName = null)
        {
            var key = (owner, valueType);
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var props = owner.GetProperties(Flags)
                .Where(p => valueType.IsAssignableFrom(p.PropertyType) && p.GetIndexParameters().Length == 0)
                .Cast<MemberInfo>()
                .ToArray();
            var fields = owner.GetFields(Flags)
                .Where(f => valueType.IsAssignableFrom(f.FieldType))
                .Cast<MemberInfo>()
                .ToArray();

            var matches = props.Concat(fields).ToArray();
            MemberInfo found = null;

            if (matches.Length == 1) found = matches[0];
            else if (matches.Length > 1)
            {
                found = (preferredName != null
                            ? matches.FirstOrDefault(m => string.Equals(m.Name, preferredName, StringComparison.OrdinalIgnoreCase))
                              ?? matches.FirstOrDefault(m => m.Name.EndsWith(preferredName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                            : null)
                        ?? matches[0];
                Plugin.LogSource?.LogWarning(
                    $"QuickSell: {matches.Length} members of type {valueType.Name} on {owner.Name} " +
                    $"({string.Join(", ", matches.Select(m => m.Name))}); using '{found.Name}'.");
            }
            else
            {
                Plugin.LogSource?.LogError(
                    $"QuickSell: no field/property of type {valueType.Name} found on {owner.Name}. " +
                    "The game build changed; this feature will not work.");
            }

            Cache[key] = found;
            return found;
        }

        public static T Get<T>(object instance, string preferredName = null) where T : class
        {
            if (instance == null) return null;
            var member = Find(instance.GetType(), typeof(T), preferredName);
            return Read<T>(member, instance);
        }

        public static T GetOn<T>(Type owner, object instance, string preferredName = null) where T : class
        {
            if (instance == null) return null;
            var member = Find(owner, typeof(T), preferredName);
            return Read<T>(member, instance);
        }

        private static T Read<T>(MemberInfo member, object instance) where T : class
        {
            try
            {
                if (member is PropertyInfo prop) return prop.GetValue(instance) as T;
                if (member is FieldInfo field) return field.GetValue(instance) as T;
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogWarning($"QuickSell: reading {member?.Name} failed: {ex.Message}");
            }
            return null;
        }
    }
}
