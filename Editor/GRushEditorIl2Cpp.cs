using UnityEditor;
using UnityEditor.Build;

namespace GRushSdk.Editor
{
    internal static class GRushEditorIl2Cpp
    {
#if UNITY_2022_2_OR_NEWER
        public static ManagedStrippingLevel StrippingLevel
        {
            get => PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL);
            set => PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, value);
        }

        public static Il2CppCompilerConfiguration CompilerConfiguration
        {
            get => PlayerSettings.GetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL);
            set => PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, value);
        }

        public static Il2CppCodeGeneration CodeGeneration
        {
            get => PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL);
            set => PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, value);
        }
#else
        public static ManagedStrippingLevel StrippingLevel
        {
            get => PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.WebGL);
            set => PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, value);
        }

        public static Il2CppCompilerConfiguration CompilerConfiguration
        {
            get => PlayerSettings.GetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL);
            set => PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL, value);
        }

        public static Il2CppCodeGeneration CodeGeneration
        {
            get => EditorUserBuildSettings.il2CppCodeGeneration;
            set => EditorUserBuildSettings.il2CppCodeGeneration = value;
        }
#endif
    }
}
