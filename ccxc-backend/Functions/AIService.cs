using Ccxc.Core.Utils;
using ccxc_backend.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Functions
{
    public class AIService : IDisposable
    {
        private static readonly Lazy<AIService> _instance = new(() => new AIService());
        public static AIService Instance => _instance.Value;
        public void Dispose()
        {
            _client?.Dispose();
        }

        private OpenAIClient _client;

        private AIService()
        {
            var urlBase = Config.SystemConfigLoader.Config.AdminAiApiUrl; //urlBase example: "https://api.openai.com/v1"

            _client = new OpenAIClient(urlBase, Config.SystemConfigLoader.Config.AdminAiApiKey);
        }

        public async Task<string> GetCodeCompletion(string codeType, string beforeText, string afterText, double temperature = 0.3)
        {
            var systemPrompt = "你是一个经验丰富的资深软件工程师。正在参与一个 Puzzle Hunt 解谜比赛的相关开发任务。" +
                "你正在和用户结对编程，你的任务是**补全代码**。\n\n" +
                "请根据用户提供的代码上下文，在光标位置（<cursor>）之后，**补全最合理的一段代码**，使其自然地继续当前逻辑，并符合用户可能的意图。\n\n" +
                "## 规则：\n" +
                "- **只返回补全的代码本体**，不要添加任何解释或上下文。" +
                "- 请严格遵循给出的光标前后的已有代码，不要给出修改前后代码的代码。" +
                $"- 遵循现代 {codeType} 语言的最佳实践和风格。\n";
            var userPrompt = "## 当前代码上下文：\n" +
                $"```\n" +
                $"{beforeText}<cursor>{afterText}\n" +
                $"```\n";

            try
            {
                var messageList = new List<OpenAIMessage>
                {
                    new (Role.System, systemPrompt),
                    new (Role.User, userPrompt)
                };

                var model = Config.SystemConfigLoader.Config.AdminAiApiModel;

                var response = await _client.ChatCompletion(messageList, model: model, temperature: temperature, maxTokens: 2000);

                var firstChoice = response?.Choices?.FirstOrDefault();
                return firstChoice?.Message.Content ?? string.Empty;
            }
            catch (Exception ex)
            {
                // Log the exception
                Logger.Error($"Error in GetCompletion: {ex}");
                return string.Empty;
            }
        }
    }
}
