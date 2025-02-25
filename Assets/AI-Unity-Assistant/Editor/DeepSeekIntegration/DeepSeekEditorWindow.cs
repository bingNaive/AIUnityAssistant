#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace AIUnityAssistant
{
    public class DeepSeekEditorWindow : EditorWindow
    {
        private DeepSeekConfig config;
        private string userInput = "";
        private Vector2 scrollPos;
        private bool isProcessing;

        [MenuItem("Tools/AIAssistant")]
        public static void ShowWindow()
        {
            var window = GetWindow<DeepSeekEditorWindow>("AIAssistant");
            window.minSize = new Vector2(600, 450);
            window.LoadConfig();
        }

        void OnGUI()
        {
            DrawConfigSection();
            DrawInputSection();
            HandleKeyboard();
        }

        private void DrawConfigSection()
        {
            GUILayout.Label("config", EditorStyles.boldLabel);
            config = EditorGUILayout.ObjectField("Configuration Files", config, typeof(DeepSeekConfig), false) as DeepSeekConfig;

            if (config == null)
            {
                EditorGUILayout.HelpBox("Please create a configuration file first", MessageType.Warning);
                if (GUILayout.Button("Create Configuration")) CreateConfig();
                return;
            }
        }

        private void DrawInputSection()
        {
            GUILayout.Space(10);
            userInput = EditorGUILayout.TextArea(userInput, GUILayout.Height(100));

            EditorGUI.BeginDisabledGroup(isProcessing);
            if (GUILayout.Button("Generate", GUILayout.Height(30)))
            {
                ExecuteGeneration();
            }

            EditorGUI.EndDisabledGroup();
        }

        private void ExecuteGeneration()
        {
            if (string.IsNullOrEmpty(userInput))
            {
                ShowError("请输入生成指令");
                return;
            }

            isProcessing = true;
            DeepSeekRequestHandler.SendRequest(
                config.API_URL,
                config.apiKey,
                userInput,
                HandleResponse,
                error =>
                {
                    isProcessing = false;
                    ShowError(error);
                }
            );
        }

        private void HandleResponse(string response)
        {
            isProcessing = false;
            ResponseProcessor.Process(response);
            Repaint();
        }

        private void CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<DeepSeekConfig>();
            AssetDatabase.CreateAsset(config, "Assets/Resources/DeepSeekConfig.asset");
            AssetDatabase.SaveAssets();
            this.config = config;
        }

        private void HandleKeyboard()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                ExecuteGeneration();
                Event.current.Use();
            }
        }

        private void ShowError(string msg)
        {
            EditorUtility.DisplayDialog("生成错误", msg, "确定");
        }
        private void LoadConfig()
        {
            config = Resources.Load<DeepSeekConfig>("DeepSeekConfig");
        }
    }
}
#endif