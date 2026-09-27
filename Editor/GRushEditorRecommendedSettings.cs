using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine.Rendering;

namespace GRushSdk.Editor
{
    internal sealed class GRushSettingRule
    {
        public string Label;
        public Func<string> Current;
        public string Recommended;
        public Func<bool> IsOk;
        public Action Apply;
    }

    internal static class GRushEditorRecommendedSettings
    {
        public const long GuidelineBytes = 30L * 1024 * 1024;

        private static readonly GraphicsDeviceType[] WebGl2Only = { GraphicsDeviceType.OpenGLES3 };

        private static List<GRushSettingRule> rules;

        public static List<GRushSettingRule> All()
        {
            if (rules == null)
            {
                rules = Build();
            }
            return rules;
        }

        public static List<GRushSettingRule> Pending()
        {
            var pending = new List<GRushSettingRule>();
            foreach (var rule in All())
            {
                if (!rule.IsOk())
                {
                    pending.Add(rule);
                }
            }
            return pending;
        }

        public static List<string> ApplyAll()
        {
            var changes = new List<string>();
            foreach (var rule in Pending())
            {
                var before = rule.Current();
                rule.Apply();
                changes.Add(rule.Label + ": " + before + " -> " + rule.Current());
            }
            if (changes.Count > 0)
            {
                AssetDatabase.SaveAssets();
            }
            return changes;
        }

        public static string ColorSpaceLabel()
        {
            return PlayerSettings.colorSpace.ToString();
        }

        private static List<GRushSettingRule> Build()
        {
            return new List<GRushSettingRule>
            {
                Rule(
                    "Compression Format",
                    () => PlayerSettings.WebGL.compressionFormat.ToString(),
                    WebGLCompressionFormat.Brotli.ToString(),
                    () => PlayerSettings.WebGL.compressionFormat == WebGLCompressionFormat.Brotli,
                    () => PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli
                ),
                Toggle(
                    "Decompression Fallback",
                    () => PlayerSettings.WebGL.decompressionFallback,
                    false,
                    value => PlayerSettings.WebGL.decompressionFallback = value
                ),
                Toggle(
                    "Threads Support",
                    () => PlayerSettings.WebGL.threadsSupport,
                    false,
                    value => PlayerSettings.WebGL.threadsSupport = value
                ),
                Rule(
                    "Managed Stripping Level",
                    () => GRushEditorIl2Cpp.StrippingLevel.ToString(),
                    ManagedStrippingLevel.Medium.ToString(),
                    () =>
                        GRushEditorIl2Cpp.StrippingLevel == ManagedStrippingLevel.Medium
                        || GRushEditorIl2Cpp.StrippingLevel == ManagedStrippingLevel.High,
                    () => GRushEditorIl2Cpp.StrippingLevel = ManagedStrippingLevel.Medium
                ),
                Toggle(
                    "Strip Engine Code",
                    () => PlayerSettings.stripEngineCode,
                    true,
                    value => PlayerSettings.stripEngineCode = value
                ),
                Rule(
                    "IL2CPP Code Generation",
                    () => GRushEditorIl2Cpp.CodeGeneration.ToString(),
                    Il2CppCodeGeneration.OptimizeSize.ToString(),
                    () => GRushEditorIl2Cpp.CodeGeneration == Il2CppCodeGeneration.OptimizeSize,
                    () => GRushEditorIl2Cpp.CodeGeneration = Il2CppCodeGeneration.OptimizeSize
                ),
                Rule(
                    "C++ Compiler Configuration",
                    () => GRushEditorIl2Cpp.CompilerConfiguration.ToString(),
                    Il2CppCompilerConfiguration.Release.ToString(),
                    () =>
                        GRushEditorIl2Cpp.CompilerConfiguration
                        == Il2CppCompilerConfiguration.Release,
                    () =>
                        GRushEditorIl2Cpp.CompilerConfiguration =
                            Il2CppCompilerConfiguration.Release
                ),
                Rule(
                    "Enable Exceptions",
                    () => PlayerSettings.WebGL.exceptionSupport.ToString(),
                    WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly.ToString(),
                    () =>
                        PlayerSettings.WebGL.exceptionSupport
                        == WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly,
                    () =>
                        PlayerSettings.WebGL.exceptionSupport =
                            WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly
                ),
                Toggle(
                    "Data Caching",
                    () => PlayerSettings.WebGL.dataCaching,
                    false,
                    value => PlayerSettings.WebGL.dataCaching = value
                ),
                Toggle(
                    "Development Build",
                    () => EditorUserBuildSettings.development,
                    false,
                    value => EditorUserBuildSettings.development = value
                ),
                Rule(
                    "Graphics APIs",
                    GraphicsApisLabel,
                    "WebGL 2",
                    IsWebGl2Only,
                    ApplyWebGl2Only
                ),
                Rule(
                    "Code Optimization",
                    GRushEditorCodeOptimization.Current,
                    GRushEditorCodeOptimization.Recommended(),
                    GRushEditorCodeOptimization.IsOk,
                    GRushEditorCodeOptimization.Apply
                ),
            };
        }

        private static GRushSettingRule Rule(
            string label,
            Func<string> current,
            string recommended,
            Func<bool> isOk,
            Action apply
        )
        {
            return new GRushSettingRule
            {
                Label = label,
                Current = current,
                Recommended = recommended,
                IsOk = isOk,
                Apply = apply,
            };
        }

        private static GRushSettingRule Toggle(
            string label,
            Func<bool> read,
            bool recommended,
            Action<bool> write
        )
        {
            return Rule(
                label,
                () => OnOff(read()),
                OnOff(recommended),
                () => read() == recommended,
                () => write(recommended)
            );
        }

        private static string OnOff(bool value)
        {
            return value ? "オン" : "オフ";
        }

        private static bool IsWebGl2Only()
        {
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL))
            {
                return false;
            }
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);
            return apis != null && apis.Length == 1 && apis[0] == GraphicsDeviceType.OpenGLES3;
        }

        private static void ApplyWebGl2Only()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, WebGl2Only);
        }

        private static string GraphicsApisLabel()
        {
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL))
            {
                return "自動";
            }
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL) ?? new GraphicsDeviceType[0];
            var names = new List<string>();
            foreach (var api in apis)
            {
                names.Add(api == GraphicsDeviceType.OpenGLES3 ? "WebGL 2" : api.ToString());
            }
            return names.Count == 0 ? "なし" : string.Join(", ", names);
        }
    }
}
