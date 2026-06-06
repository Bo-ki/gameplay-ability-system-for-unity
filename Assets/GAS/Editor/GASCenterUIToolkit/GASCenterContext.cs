using System;
using System.IO;
using GAS.General;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor
{
    internal sealed class GASCenterContext
    {
        private readonly Action<string> _notify;

        public GASCenterContext(Action<string> notify)
        {
            _notify = notify;
            ReloadSettings();
        }

        public GASSettingAsset SettingAsset { get; private set; }
        public SerializedObject SettingSerializedObject { get; private set; }

        public string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        public void ReloadSettings()
        {
            SettingAsset = GASSettingAsset.LoadOrCreate();
            SettingSerializedObject = new SerializedObject(SettingAsset);
        }

        public void WarmCaches()
        {
            GasJsonReader.ReadAllAndCache();

            try
            {
                GeneralGasChoiceHelper.LoadCache();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EX-GAS] 刷新运行时选项缓存失败: {ex.GetType().Name}: {ex.Message}");
            }
        }

        public void SaveSettings()
        {
            SettingSerializedObject.ApplyModifiedProperties();
            GASSettingAsset.UpdateAsset(SettingAsset);
            GASSettingAsset.Save();
            Notify("GAS 设置已保存");
        }

        public void Notify(string message)
        {
            _notify?.Invoke(message);
        }

        public string ResolveProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(ProjectRoot, path));
        }

        public bool RevealPath(string path, string label)
        {
            var resolvedPath = ResolveProjectPath(path);
            if (File.Exists(resolvedPath) || Directory.Exists(resolvedPath))
            {
                EditorUtility.RevealInFinder(resolvedPath);
                return true;
            }

            EditorUtility.DisplayDialog("路径不存在", $"{label} 未找到:\n{resolvedPath}", "确定");
            return false;
        }
    }
}
