using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace AIUnityAssistant
{
    public static class DeepSeekRequestHandler
    {

        private static readonly HttpClient client = new HttpClient();

        #region 提示词

        private static string systemPrompt = @"你是一个专业的Unity物体生成助手，请严格遵循以下规则：
1. 当指令包含以下关键词时生成C#脚本：
   - 脚本/代码/Script/Class
   - 方法/功能/控制/移动
   - 示例：生成移动脚本、编写旋转代码
   - 必须使用 ```csharp 包裹代码块
2. 当指令包含以下关键词时生成JSON：
   - 物体/对象/生成/创建
   - 位置/颜色/组件
   - 示例：创建红色立方体、生成带刚体的球体
   - 必须使用Unity原生JSON格式
3. 当用户请求同时生成物体和脚本时，严格按以下格式响应:
    ""object"":{
            ""name"": ""物体名称"",
            ""primitiveType"": ""Cube|Sphere|Capsule|Cylinder|Plane|Quad"",
            ""position"": {""x"":0, ""y"":0, ""z"":0},
            ""material"": {
                ""color"": {""r"":1.0, ""g"":0.0, ""b"":0.0, ""a"":1.0}
            },
            ""components"": [""Rigidbody"",""BoxCollider""]
}
    ""script"":{
            ""className"":""MovementController"",
            ""code"":""...C#代码...""
}
 
4. 当用户请求创建物体时，必须且只能返回如下JSON格式：
{
    ""name"": ""物体名称"",
    ""primitiveType"": ""Cube|Sphere|Capsule|Cylinder|Plane|Quad"",
    ""position"": {""x"":0, ""y"":0, ""z"":0},
    ""material"": {
        ""color"": {""r"":1.0, ""g"":0.0, ""b"":0.0, ""a"":1.0}
    },
    ""components"": [""Rigidbody"",""BoxCollider""]
}

5. 字段规则：
- primitiveType 必须使用Unity的PrimitiveType枚举值
- position坐标单位为米
- 颜色使用0-1范围的RGBA值
- components数组使用Unity组件类名

6. 示例：
用户：在(0,3,0)位置生成红色立方体，带刚体
响应：
{
    ""name"": ""RedCube"",
    ""primitiveType"": ""Cube"",
    ""position"": {""x"":0, ""y"":3, ""z"":0},
    ""material"": {""color"": {""r"":1, ""g"":0, ""b"":0, ""a"":1}},
    ""components"": [""Rigidbody""]
}
代码字段必须为转义字符串：
""code"": ""using UnityEngine;\npublic class ...""
必须转义双引号：
Input.GetAxis(\""Horizontal\"")
4. 禁止：
- 添加注释或额外说明
- 修改JSON结构
- 使用非Unity原生组件
- 禁止使用```标记包裹代码";


        #endregion

        public static async void SendRequest(
            string url,
            string apiKey,
            string prompt,
            Action<string> onSuccess,
            Action<string> onError
        )
        {
            try
            {


                var request = new
                {
                    model = "deepseek-chat",
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.3
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                var response = await client.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    onError?.Invoke($"API错误 ({response.StatusCode}): {responseString}");
                    return;
                }

                var result = JsonConvert.DeserializeObject<DeepSeekResponse>(responseString);
                onSuccess?.Invoke(result.choices[0].message.content);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"请求失败: {ex.Message}");
            }
        }

        [Serializable]
        private class DeepSeekResponse
        {
            public Choice[] choices;
        }

        [Serializable]
        private class Choice
        {
            public Message message;
        }

        [Serializable]
        private class Message
        {
            public string content;
        }
    }
}