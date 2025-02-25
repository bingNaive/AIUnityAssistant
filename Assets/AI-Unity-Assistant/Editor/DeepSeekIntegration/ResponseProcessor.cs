#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using Assembly = System.Reflection.Assembly;

namespace AIUnityAssistant
{
    public static class ResponseProcessor
    {
        private const string ScriptPath = "Assets/Scripts/AIGenerate/";
        private const string ObjectPattern = @"\{(?:[^{}]|(?<o>\{)|(?<-o>\}))+(?(o)(?!))\}";

        [Serializable]
        private class CombinedResponse
        {
            public ObjectData @object;
            public ScriptData script;
        }

        [Serializable]
        private class ScriptData
        {
            public string className;
            public string code;
        }

        private static string CleanInvisibleChars(string json)
        {
            return Regex.Replace(json, @"\p{C}+", "");
        }

        public static void Process(string response)
        {
            try
            {
                Debug.Log(response);
                // 尝试解析组合响应
                if (TryParseCombinedResponse(response, out var data))
                {
                    GameObject obj = CreateObject(data.@object);
                    CreateAndAttachScript(data.script, obj);
                    return;
                }

                if (IsObjectCommand(response))
                {
                    CreateObject(response);
                    return;
                }

                if (TryGetCode(response, out var code))
                {
                    CreateScript(code);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"处理失败: {ex.Message}\n原始响应:\n{response}");
            }
        }

        #region 组合功能方法

        private static bool TryParseCombinedResponse(string response, out CombinedResponse data)
        {
            // 匹配更宽松的JSON结构
            const string pattern = @"
        \{\s*
            ""object""\s*:\s*(?<object>\{.*?\})\s*,\s*
            ""script""\s*:\s*(?<script>\{.*?\})\s*
        \}
    ";

            var match = Regex.Match(response, pattern,
                RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);

            if (!match.Success)
            {
                data = null;
                return false;
            }

            string json = "";
            try
            {
                json = $@"{{
            ""object"": {match.Groups["object"].Value},
            ""script"": {match.Groups["script"].Value}
            }}";

                // 清理不可见字符
                json = CleanInvisibleChars(json);

                // 处理特殊转义
                json = json.Replace(@"\\""", @"""") // 修复双重转义
                    .Replace(@"""", @""""); // 统一引号格式

                data = JsonUtility.FromJson<CombinedResponse>(json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"JSON解析失败：{ex.Message}\n处理后JSON：\n{json}");
                data = null;
                return false;
            }
        }

        private static DateTime delayCallTime;
        private static ScriptData _scriptData;
        private static GameObject _targetObject;

        private static void StartDelayCall()
        {
            delayCallTime = DateTime.Now;
            EditorApplication.update += CheckDelayCall;
            Debug.Log("11111111111111");
        }

        private static void CheckDelayCall()
        {
            Debug.Log("3333333333");
            if ((DateTime.Now - delayCallTime).TotalSeconds > 1)
            {
                Debug.Log("2222222222222");
                EditorApplication.update -= CheckDelayCall;
                // 获取类型
                Type type = GetScriptType(_scriptData.className);
                if (type != null)
                {
                    // 确保物体未被销毁
                    if (_targetObject != null)
                    {
                        // 添加组件
                        Undo.AddComponent(_targetObject, type);
                        Debug.Log($"组件 {_scriptData.className} 添加成功");
                    }
                }
                else
                {
                    Debug.LogError($"无法找到脚本类型: {_scriptData.className}");
                }
            }
        }

        private static void CreateAndAttachScript(ScriptData scriptData, GameObject targetObject)
        {
            // 生成脚本文件
            string path = Path.Combine("Assets/Scripts/AI生成/", $"{scriptData.className}.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, scriptData.code);

            AssetDatabase.Refresh();
            ScriptAttachManager.SubmitTask(targetObject, scriptData.className);
            // // 注册编译完成回调
            // CompilationPipeline.compilationFinished += OnCompilationFinished;
            // // 启动超时检测
            // // StartDelayCall();
            // void OnCompilationFinished(object obj)
            // {
            //     // 确保只执行一次
            //     CompilationPipeline.compilationFinished -= OnCompilationFinished;
            //     _scriptData=scriptData;
            //     _targetObject = targetObject;
            //     StartDelayCall();
            //     // EditorApplication.delayCall += () =>
            //     // {
            //     //     EditorApplication.delayCall += () =>
            //     //     {
            //     //
            //     //     };
            //     // };
            // }
        }

        private static Type GetScriptType(string className)
        {
            // 方式1：尝试基本类型获取
            Type type = Type.GetType(className);
            if (type != null) return type;

            // 方式2：主程序集限定名
            type = Type.GetType($"{className}, Assembly-CSharp");
            if (type != null) return type;

            // 方式3：遍历所有程序集
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(className);
                if (type != null) return type;
            }

            // 方式4：模糊匹配（应对特殊大小写情况）
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetTypes()
                    .FirstOrDefault(t => t.Name.Equals(className, StringComparison.OrdinalIgnoreCase));
                if (type != null) return type;
            }

            Debug.LogError($"类型 {className} 未找到，已加载程序集：");
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Debug.Log($" - {assembly.FullName}");
            }

            return null;
        }

        #endregion

        private static bool IsObjectCommand(string response)
        {
            return response.Contains("\"primitiveType\"") && response.Contains("\"components\"");
        }

        private static void CreateObject(string json)
        {
            try
            {
                // 修正解析方法
                var data = JsonUtility.FromJson<ObjectData>(json);

                CreateObject(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"物体生成失败: {ex.Message}\n原始JSON:\n{json}");
            }
        }

        private static GameObject CreateObject(ObjectData data)
        {
            // 创建基础物体
            GameObject obj = GameObject.CreatePrimitive(ParsePrimitiveType(data.primitiveType));
            obj.name = GetUniqueName(data.name);
            obj.transform.position = data.position;

            // 应用材质颜色
            ApplyMaterial(obj, data.material.color);

            // 添加组件
            foreach (var comp in data.components)
            {
                AddComponent(obj, comp);
            }

            // 聚焦物体
            Selection.activeGameObject = obj;
            SceneView.FrameLastActiveSceneView();
            // 标记场景为需要保存
            EditorSceneManager.MarkSceneDirty(obj.scene);
            // 立即保存场景
            EditorSceneManager.SaveScene(obj.scene);
            return obj;
        }

        private static PrimitiveType ParsePrimitiveType(string typeStr)
        {
            if (Enum.TryParse(typeStr, true, out PrimitiveType result))
            {
                return result;
            }

            return PrimitiveType.Cube;
        }

        private static void ApplyMaterial(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;

            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            renderer.material = mat;
        }

        private static void CreateScript(string code)
        {
            var className = Regex.Match(code, @"class\s+(\w+)").Groups[1].Value;
            if (string.IsNullOrEmpty(className))
            {
                className = "NewScript_" + DateTime.Now.ToString("HHmmss");
            }

            var path = Path.Combine(ScriptPath, className + ".cs");
            Directory.CreateDirectory(ScriptPath);

            File.WriteAllText(path, code);
            AssetDatabase.Refresh();
            Debug.Log($"脚本已生成: {path}");
        }

        private static string GetUniqueName(string baseName)
        {
            var name = baseName;
            var count = 1;
            while (GameObject.Find(name) != null)
            {
                name = $"{baseName}_{count++}";
            }

            return name;
        }

        private static PrimitiveType GetPrimitiveType(string type)
        {
            return type.ToLower() switch
            {
                "sphere" => PrimitiveType.Sphere,
                "capsule" => PrimitiveType.Capsule,
                _ => PrimitiveType.Cube
            };
        }

        private static void AddComponent(GameObject obj, string component)
        {
            switch (component.ToLower())
            {
                case "rigidbody":
                    if (!obj.GetComponent<Rigidbody>()) obj.AddComponent<Rigidbody>();
                    break;
                case "audiosource":
                    if (!obj.GetComponent<AudioSource>()) obj.AddComponent<AudioSource>();
                    break;
            }
        }

        [Serializable]
        private class ObjectData
        {
            public string name;
            public string primitiveType; // 字段名修正
            public Vector3 position;
            public MaterialData material; // 新增材质结构
            public string[] components = Array.Empty<string>();
        }

        [Serializable]
        private class MaterialData
        {
            public Color color = Color.white;
        }

        private static bool TryGetCode(string response, out string code)
        {
            var match = Regex.Match(response, @"```csharp(.*?)```", RegexOptions.Singleline);
            code = match.Success ? match.Groups[1].Value.Trim() : null;
            return match.Success;
        }
    }
}
#endif