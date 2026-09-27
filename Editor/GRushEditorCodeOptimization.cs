using System;
using System.Reflection;

namespace GRushSdk.Editor
{
    internal static class GRushEditorCodeOptimization
    {
        public const string Unavailable = "—";

        private const string TypeName = "UnityEditor.WebGL.UserBuildSettings";
        private const string AssemblyName = "UnityEditor.WebGL.Extensions";
        private const string PropertyName = "codeOptimization";
        private static readonly string[] Preferred = { "DiskSizeLTO", "DiskSize", "Size" };

        private static bool resolved;
        private static PropertyInfo cached;

        public static string Recommended()
        {
            var property = Property();
            return property == null ? Unavailable : PreferredName(property.PropertyType) ?? Unavailable;
        }

        public static string Current()
        {
            try
            {
                var property = Property();
                if (property == null || !property.CanRead)
                {
                    return Unavailable;
                }
                var value = property.GetValue(null);
                return value == null ? Unavailable : value.ToString();
            }
            catch (Exception)
            {
                return Unavailable;
            }
        }

        public static bool IsOk()
        {
            var recommended = Recommended();
            return recommended == Unavailable || Current() == recommended;
        }

        public static void Apply()
        {
            try
            {
                var property = Property();
                if (property == null || !property.CanWrite)
                {
                    return;
                }
                var name = PreferredName(property.PropertyType);
                if (name == null)
                {
                    return;
                }
                property.SetValue(null, Enum.Parse(property.PropertyType, name));
            }
            catch (Exception)
            {
            }
        }

        private static string PreferredName(Type enumType)
        {
            if (!enumType.IsEnum)
            {
                return null;
            }
            var names = Enum.GetNames(enumType);
            foreach (var candidate in Preferred)
            {
                if (Array.IndexOf(names, candidate) >= 0)
                {
                    return candidate;
                }
            }
            return null;
        }

        private static PropertyInfo Property()
        {
            if (!resolved)
            {
                cached = Resolve();
                resolved = true;
            }
            return cached;
        }

        private static PropertyInfo Resolve()
        {
            try
            {
                var type = FindType();
                if (type == null)
                {
                    return null;
                }
                var property = type.GetProperty(
                    PropertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                );
                return property != null && property.PropertyType.IsEnum ? property : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Type FindType()
        {
            var type = Type.GetType(TypeName + ", " + AssemblyName, false);
            if (type != null)
            {
                return type;
            }
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(TypeName, false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }
    }
}
