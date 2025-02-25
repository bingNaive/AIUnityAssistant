using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Assembly = System.Reflection.Assembly;

namespace AIUnityAssistant
{
// [CreateAssetMenu(fileName = "PendingScriptTask")]
    public class PendingScriptTask : ScriptableObject
    {
        public string sceneIdentifier; // 场景路径 + 对象路径
        public string className;
        public int retryCount;
    }

// 注册编译事件监听
    [InitializeOnLoad]
    public static class ScriptAttachManager
    {
        private const int MAX_RETRY = 3;
        private static PendingScriptTask currentTask;

        static ScriptAttachManager()
        {
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        // 提交任务
        public static void SubmitTask(GameObject target, string className)
        {
            currentTask = ScriptableObject.CreateInstance<PendingScriptTask>();
            currentTask.sceneIdentifier = GetObjectIdentifier(target);
            currentTask.className = className;
            currentTask.retryCount = 0;

            string taskPath = "Assets/PendingScriptTask.asset";
            AssetDatabase.CreateAsset(currentTask, taskPath);
            AssetDatabase.SaveAssets();
        }

        // 编译完成回调
        private static void OnCompilationFinished(object obj)
        {
            if (currentTask == null) return;
            EditorApplication.delayCall += TryAttachComponent;
        }

        // 程序集重载后回调
        private static void OnAfterAssemblyReload()
        {
            currentTask = AssetDatabase.LoadAssetAtPath<PendingScriptTask>("Assets/PendingScriptTask.asset");
            if (currentTask == null) return;
            EditorApplication.delayCall += TryAttachComponent;
        }


        // 尝试附加组件
        private static void TryAttachComponent()
        {
            GameObject target = FindObjectByIdentifier(currentTask.sceneIdentifier);
            if (target == null)
            {
                Debug.LogError("目标物体已丢失");
                CleanupTask();
                return;
            }

            System.Type type = GetScriptType(currentTask.className);
            if (type != null)
            {
                Undo.AddComponent(target, type);
                Debug.Log("组件添加成功");
                CleanupTask();
            }
            else
            {
                currentTask.retryCount++;
                Debug.LogWarning($"类型未找到，重试 {currentTask.retryCount}/{MAX_RETRY}");
                AssetDatabase.SaveAssets(); // 保存重试次数
            }
        }

        // 清理任务
        private static void CleanupTask()
        {
            string path = AssetDatabase.GetAssetPath(currentTask);
            AssetDatabase.DeleteAsset(path);
            currentTask = null;
        }

        private static Type GetScriptType(string className)
        {
            if (string.IsNullOrEmpty(className)) return null;

            // 遍历所有已加载程序集
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type type = assembly.GetType(className);
                    if (type != null) return type;

                    // 模糊匹配（处理大小写问题）
                    type = assembly.GetTypes()
                        .FirstOrDefault(t => t.Name.Equals(className, StringComparison.OrdinalIgnoreCase));
                    if (type != null) return type;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"程序集 {assembly.FullName} 扫描失败: {ex.Message}");
                }
            }

            Debug.LogError($"未找到类型: {className}");
            return null;
        }

        private static string GetObjectIdentifier(GameObject obj)
        {
            if (obj == null) return null;

            // 获取场景路径和对象路径
            Scene scene = obj.scene;
            string scenePath = scene.path;
            string objectPath = GetGameObjectPath(obj);

            return $"{scenePath}|{objectPath}";
        }

        private static GameObject FindObjectByIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return null;

            string[] parts = identifier.Split('|');
            if (parts.Length != 2) return null;

            string scenePath = parts[0];
            string objectPath = parts[1];

            // 尝试查找已打开场景
            for (int i = 0; i < SceneManager.loadedSceneCount; i++)
            {
                Scene scene = EditorSceneManager.GetSceneAt(i);
                if (scene.path == scenePath)
                {
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    foreach (GameObject root in rootObjects)
                    {
                        Transform child = root.transform.Find(objectPath);
                        if (child != null) return child.gameObject;
                    }
                }
            }

            // 打开目标场景查找
            Scene targetScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            GameObject target = GameObject.Find(objectPath);
            EditorSceneManager.CloseScene(targetScene, true);

            return target;
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            if (obj == null) return "";
            if (obj.transform.parent == null) return "/" + obj.name;
            return GetGameObjectPath(obj.transform.parent.gameObject) + "/" + obj.name;
        }
    }
}