using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using AIChatApp.Helpers;
using AIChatApp.Models;
using System.Runtime.CompilerServices;

namespace AIChatApp.Services;

internal class ChatService
{
    private readonly IChatCompletionService _chatService;
    private readonly OpenAIPromptExecutionSettings _executionSettings;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IChatCompletionService chatService, IConfiguration config, ILogger<ChatService> logger, ConfigHelper configHelper)
    {
        _chatService = chatService;
        _logger = logger;

        // Create execution settings from configuration with fallback defaults
        _executionSettings = new OpenAIPromptExecutionSettings
        {
            FrequencyPenalty = configHelper.GetDouble(config, "openai-configuration-FrequencyPenalty"),
            MaxTokens = configHelper.GetInt(config, "openai-configuration-MaxTokens"),
            PresencePenalty = configHelper.GetDouble(config, "openai-configuration-PresencePenalty"),
            Temperature = configHelper.GetDouble(config, "openai-configuration-Temperature", 1.0), // default to 1.0
            TopP = configHelper.GetDouble(config, "openai-configuration-TopP", 1.0) // default to 1.0
        };
    }

    internal async Task<Message> Chat(ChatRequest request, CancellationToken cancellationToken = default)
    {
        ChatHistory history = CreateHistoryFromRequest(request);

        ChatMessageContent response = await _chatService.GetChatMessageContentAsync(
            chatHistory: history,
            executionSettings: _executionSettings,
            cancellationToken: cancellationToken
        );

        var textContent = response.Items[0] as TextContent;
        if (textContent is null || string.IsNullOrEmpty(textContent.Text))
        {
            throw new InvalidOperationException("Invalid or empty text content.");
        }

        return new Message(isAssistant: response.Role == AuthorRole.Assistant, content: textContent.Text);
    }

    internal async IAsyncEnumerable<string> Stream(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatHistory history = CreateHistoryFromRequest(request);

        var response = _chatService.GetStreamingChatMessageContentsAsync(
            chatHistory: history,
            executionSettings: _executionSettings,
            cancellationToken: cancellationToken
        );

        await foreach (var content in response.WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrEmpty(content.Content))
            {
                yield return content.Content;
            }
        }
    }

    private static readonly string[] StrictIdk = ["I don't know.", "Je ne sais pas."];

    private static bool IsIDK(string? text)
      => !string.IsNullOrWhiteSpace(text) && StrictIdk.Contains(text.Trim(), StringComparer.Ordinal);

    private static ChatHistory CreateHistoryFromRequest(ChatRequest request)
    {
        // 1) Always start with system message
        var history = new ChatHistory(PromptBuilderService.GetSystemPrompt());

        // 2) Copy current messages so we can prune in place
        var msgs = new List<Message>(request.Messages);

        // 3) Remove the trailing two messages if they match the pattern:
        //    [..., (n-3)=user original question, (n-2)=assistant "", (n-1)=user RAG prompt]
        if (msgs.Count >= 3)
        {
            var last = msgs[^1];   // expected: user (RAG prompt)
            var prev = msgs[^2];   // expected: assistant (empty)
            var prevprev = msgs[^3];   // expected: user (original question)

            if (!last.IsAssistant &&
                prev.IsAssistant && string.IsNullOrWhiteSpace(prev.Content) &&
                !prevprev.IsAssistant)
            {
                // Remove (n-2) blank assistant and (n-3) original user (in that order)
                msgs.RemoveAt(msgs.Count - 2); // remove blank assistant
                msgs.RemoveAt(msgs.Count - 2); // remove original user (now at the same index)
            }
        }

        // 4) Remove any "I don't know." / "Je ne sais pas." assistant reply
        //    AND also drop the immediately preceding user turn, if present.
        var cleaned = new List<Message>();
        foreach (var m in msgs)
        {
            if (m.IsAssistant && IsIDK(m.Content))
            {
                // Drop this assistant turn and the user turn right before it (if any)
                if (cleaned.Count > 0 && !cleaned[^1].IsAssistant)
                    cleaned.RemoveAt(cleaned.Count - 1);
                continue;
            }
            cleaned.Add(m);
        }

        // 5) Add the remaining turns to ChatHistory in order
        foreach (var m in cleaned)
        {
            if (m.IsAssistant) history.AddAssistantMessage(m.Content);
            else history.AddUserMessage(m.Content);
        }

        return history;
    }
}
