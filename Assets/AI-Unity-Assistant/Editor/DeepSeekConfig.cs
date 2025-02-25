// DeepSeekConfig.cs
using UnityEngine;

namespace AIUnityAssistant
{
    [CreateAssetMenu(fileName = "DeepSeekConfig", menuName = "DeepSeek/Configuration")]
    public class DeepSeekConfig : ScriptableObject
    {
        [Tooltip("API URL")] public string API_URL = "https://api.deepseek.com/v1/chat/completions";
        [Tooltip("从DeepSeek控制台获取的API密钥")] public string apiKey = "";

        [Range(0f, 2f), Tooltip("控制生成随机性 (0=确定性强，2=创意性强)")]
        public float temperature = 0.7f;

        [Range(100, 4096), Tooltip("最大生成token数量")]
        public int maxTokens = 2048;

        [Tooltip("生成位置偏移")] public Vector3 defaultPosition = Vector3.zero;
    }

}