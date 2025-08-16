using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ccxc_backend.Functions
{
    /// <summary>
    /// OpenAI API的角色枚举
    /// </summary>
    public enum Role
    {
        System,
        User,
        Assistant,
        Function
    }

    /// <summary>
    /// OpenAI API的消息类
    /// </summary>
    public class OpenAIMessage
    {
        [JsonProperty("role")]
        public string RoleString { get; set; }

        [JsonIgnore]
        public Role Role
        {
            get => Enum.Parse<Role>(RoleString, true);
            set => RoleString = value.ToString().ToLower();
        }

        [JsonProperty("content")]
        public string Content { get; set; }

        public OpenAIMessage() { }

        public OpenAIMessage(Role role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    /// <summary>
    /// OpenAI API响应的选择类
    /// </summary>
    public class OpenAIChoice
    {
        [JsonProperty("message")]
        public OpenAIMessage Message { get; set; }

        [JsonProperty("finish_reason")]
        public string FinishReason { get; set; }

        [JsonProperty("index")]
        public int Index { get; set; }
    }

    /// <summary>
    /// OpenAI API响应类
    /// </summary>
    public class OpenAIResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("object")]
        public string Object { get; set; }

        [JsonProperty("created")]
        public long Created { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("choices")]
        public List<OpenAIChoice> Choices { get; set; }

        [JsonProperty("usage")]
        public OpenAIUsage Usage { get; set; }
    }

    /// <summary>
    /// OpenAI API使用情况统计
    /// </summary>
    public class OpenAIUsage
    {
        [JsonProperty("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonProperty("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonProperty("total_tokens")]
        public int TotalTokens { get; set; }
    }

    /// <summary>
    /// OpenAI API客户端
    /// </summary>
    public class OpenAIClient : IDisposable
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public OpenAIClient(string baseUrl, string apiKey)
        {
            _baseUrl = baseUrl;
            _apiKey = apiKey;
        }

        public void Dispose()
        {
            // 没有需要释放的资源
        }

        /// <summary>
        /// 发送聊天完成请求
        /// </summary>
        /// <param name="messages">消息列表</param>
        /// <param name="model">模型名称，例如 "gpt-3.5-turbo"</param>
        /// <param name="temperature">温度参数，控制随机性</param>
        /// <param name="maxTokens">最大生成的token数量</param>
        /// <returns>OpenAI API的响应</returns>
        public async Task<OpenAIResponse> ChatCompletion(
            List<OpenAIMessage> messages,
            string model = "gpt-3.5-turbo",
            double temperature = 0.7,
            int maxTokens = 1000)
        {
            string endpoint = $"{_baseUrl}/chat/completions";

            var requestBody = new
            {
                model,
                messages,
                temperature,
                max_tokens = maxTokens
            };

            string jsonBody = JsonConvert.SerializeObject(requestBody);

            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {_apiKey}" }
            };

            try
            {
                string response = await HttpRequest.Post(endpoint, jsonBody, headers);
                return JsonConvert.DeserializeObject<OpenAIResponse>(response);
            }
            catch (Exception ex)
            {
                Ccxc.Core.Utils.Logger.Error($"OpenAIClient ChatCompletion error: {ex}");
                return null;
            }
        }
    }
} 