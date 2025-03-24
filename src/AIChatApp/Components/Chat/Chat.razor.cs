using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AIChatApp.Models;
using AIChatApp.Services;

namespace AIChatApp.Components.Chat;

public partial class Chat
{
    [Inject]
    internal ChatService? ChatHandler { get; init; }
    [Inject]
    internal IServiceProvider? ServiceProvider { get; init; }
    [Inject]
    internal ConversationService ConversationHandler { get; init; } = default!;
    [Inject]
    public IConfiguration AppConfiguration { get; set; } = default!;
    [Parameter]
    public Guid? ConversationId { get; set; } // Route param from Home.razor
    internal SearchService? SearchHandler => ServiceProvider?.GetService<SearchService>();
    ElementReference writeMessageElement;
    string? userMessageText;
    private ElementReference bottomRef;
    private IJSObjectReference? chatJsModule;
    private CancellationTokenSource? _cts;

    protected override async Task OnInitializedAsync()
    {
        if (ConversationId.HasValue)
        {
            await ConversationHandler.GetConversationByIdAsync(ConversationId.Value);
        }
    }
    private void SetSuggestion(string suggestion)
    {
        userMessageText = suggestion;
    }
    private void CancelStreaming()
    {
        _cts?.Cancel();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            chatJsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./Components/Chat/Chat.razor.js");
            await chatJsModule.InvokeVoidAsync("submitOnEnter", writeMessageElement);
            await chatJsModule.InvokeVoidAsync("autoResizeTextarea", "expanding-textarea", 3, 5);
        }
        catch (JSDisconnectedException)
        {
            // Ignore if JS runtime is unavailable
        }
    }

    private async Task ScrollToBottom()
    {
        if (chatJsModule is null) return;

        try
        {
            await chatJsModule.InvokeVoidAsync("scrollToBottom", bottomRef);
        }
        catch (JSDisconnectedException)
        {
            // Ignore if JS runtime is unavailable
        }
    }

    private async void ResetConversation()
    {
        ConversationHandler.ClearMessages();
        StateHasChanged();

        await Task.Yield();

        if (chatJsModule is null) return;

        try
        {
            await chatJsModule.InvokeVoidAsync("focusTextarea", writeMessageElement);
        }
        catch (JSDisconnectedException)
        {
            // Ignore if JS runtime is unavailable
        }
    }

    private bool IsLoadingMessage()
    {
        var conversation = ConversationHandler.GetMessages();
        if (conversation.Count > 0)
        {
            var lastMessage = conversation.Last();
            return lastMessage.IsAssistant && string.IsNullOrWhiteSpace(lastMessage.Content);
        }
        return false;
    }

    private string GeneratePrompt(string userQuery, List<Source> docs)
    {
        var builder = new PromptBuilderService();
        return builder.Build(userQuery, docs);
    }

    async void SendMessage()
    {
        if (ChatHandler is null || string.IsNullOrWhiteSpace(userMessageText))
            return;

        // save to a temporary variable and clear the textarea
        var tempUserMessageText = userMessageText;
        userMessageText = null;

        // Add user message to conversation
        var userMessage = new Message(isAssistant: false, content: tempUserMessageText);

        ConversationHandler.AddMessage(userMessage);
        await ConversationHandler.SaveMessage(userMessage);
        StateHasChanged();

        // Create a temporary assistant message, empty content
        var assistantMessage = new Message(isAssistant: true, content: "");

        // adding this triggers the "Loading" state
        ConversationHandler.AddMessage(assistantMessage);
        StateHasChanged();

        // Get embeddings docs for the current question
        var docs = await GetRelevantDocuments(tempUserMessageText);

        var prompt = GeneratePrompt(tempUserMessageText, docs);
        // Model message is user prompt enhanced with context from docs
        // It is sent to the API but not shown to the user
        var modelMessage = new Message(isAssistant: false, content: prompt);

        var recentMessages = ConversationHandler.GetRecentMessages();
        recentMessages.Add(modelMessage);
        // pass in recent messages (+ new one with generatePrompt) to ChatRequest so it can see history
        ChatRequest request = new ChatRequest(recentMessages);

        _cts = new CancellationTokenSource();
        assistantMessage = await StreamAssistantResponse(request, assistantMessage, docs, _cts.Token);

        AttachSourcesToAssistantMessage(assistantMessage, docs);
        await ConversationHandler.SaveMessage(assistantMessage);
        StateHasChanged();
        await ScrollToBottom();
    }

    private async Task<Message> StreamAssistantResponse(ChatRequest request, Message assistantMessage, List<Source> docs, CancellationToken token)
    {
        try
        {
            await foreach (var chunk in ChatHandler!.Stream(request, token))
            {
                assistantMessage.Content += chunk;
                StateHasChanged();
                await ScrollToBottom();
            }
        }
        catch (OperationCanceledException)
        {
            assistantMessage.Content = L._("Chat.Chat.CancelMessage");
            assistantMessage.Status = "cancel";
        }
        catch (Exception ex)
        {
            assistantMessage.Content = L._("Chat.Chat.ErrorMessage");
            assistantMessage.Status = "error";
            Console.WriteLine($"Error: {ex.Message}");
        }

        _cts = null;
        return assistantMessage;
    }

    // Consolidated method to get relevant documents, or an empty list
    private async Task<List<Source>> GetRelevantDocuments(string userMessageText)
    {
        var docs = SearchHandler is not null
            ? await SearchHandler.GetTopChunks(userMessageText)
            : new List<Source>();

        // Return immediately if no documents are found
        if (docs.Count == 0)
            return new List<Source>();

        ConversationHandler.CacheDocuments(docs);
        var cachedDocs = ConversationHandler.GetCachedDocuments();
        return ConsolidateDocuments(cachedDocs);
    }

    private void AttachSourcesToAssistantMessage(Message assistantMessage, List<Source> docs)
    {
        if (docs.Count == 0)
            return;

        var citationHelper = new CitationHelperService();
        var citedDocs = citationHelper.ExtractCitedDocuments(assistantMessage.Content);
        // Console.WriteLine("Cited Docs: " + string.Join(", ", citedDocs));

        if (citedDocs.Count > 0)
        {
            var filteredSources = citationHelper.FilterSourcesByCitedDocuments(docs, citedDocs);
            assistantMessage.Content = citationHelper.ReplaceTitleCitationsWithNumberCitations(assistantMessage.Content, citedDocs);
            assistantMessage.Sources = filteredSources;
        }
    }

    public List<Source> ConsolidateDocuments(List<Source> docs)
    {
        if (docs == null || !docs.Any())
            return new List<Source>();

        // Dictionary to store chunks by title
        var consolidatedDocs = new Dictionary<string, Source>();

        foreach (var doc in docs)
        {
            if (consolidatedDocs.TryGetValue(doc.Title, out var existingDoc))
            {
                // Use the AppendToChunk method to combine text
                existingDoc.AppendToChunk($"\n\n…\n\n{doc.Chunk}");
            }
            else
            {
                // Otherwise, add it to our dictionary
                consolidatedDocs[doc.Title] = doc;
            }
        }

        // Convert dictionary values to list
        return consolidatedDocs.Values.ToList();
    }
}
