using AIChatApp.Models;
using System.Text.Json;
using AIChatApp.Helpers;
using AIChatApp.Services;

public class ConversationWithMessages
{
  public Conversation Conversation { get; set; }
  public List<Message> Messages { get; set; } = new();

  public ConversationWithMessages(Conversation conversation)
  {
    Conversation = conversation;
  }
}

public class ConversationService
{
  private readonly List<Message> _messages = new();
  private readonly List<Source> _cachedDocuments = new(); // Cached document sources
  private readonly string? _userId;
  private readonly DatabaseService _databaseService;
  private readonly ILogger<ConversationService> _logger;
  private readonly ConfigHelper _configHelper;
  private readonly IConfiguration _config;
  private Conversation? _activeConversation;

  public ConversationService(UserService userService, DatabaseService databaseService, ILogger<ConversationService> logger, ConfigHelper configHelper, IConfiguration config)
  {
    _logger = logger;
    _databaseService = databaseService;
    _configHelper = configHelper;
    _config = config;

    _userId = userService.GetUserIdAsync();
    _logger.LogInformation("ConversationService: User ID {UserId}", _userId ?? "null");
  }

  public bool IsInitialized()
  {
    return _databaseService.IsInitialized();
  }

  // Get the recent messages count from configuration
  // Ensures minimum of 1 message
  private int GetRecentMessagesCount()
  {
    var configValue = _configHelper.GetInt(_config, "var-context-recent-messages") ?? 6; // default to 6
    return Math.Max(1, configValue); // ensure minimum of 1
  }

  // Calculate the max cached documents based on recent messages and search results
  // Formula: ((recent_messages / 2) * search_results) * 0.8, rounded down
  // - recent_messages / 2 = number of exchanges (each exchange = user + assistant message)
  // - multiply by search_results = how many documents are returned per exchange
  // - multiply by 0.8 = account for document overlap (same docs are likely returned on subsequent questions)
  private int GetMaxCachedDocumentsCount()
  {
    var recentMessages = GetRecentMessagesCount(); // defaults to 6
    var searchResults = _configHelper.GetInt(_config, "var-vector-search-results") ?? 5; // default to 5

    var exchanges = recentMessages / 2.0; // number of exchanges (exchange = user + assistant message)
    var documentsNeeded = exchanges * searchResults * 0.8; // account for overlap

    return (int)Math.Floor(documentsNeeded); // round down to nearest int
  }

  // ═══════════════════════════════════════════════════════════════════════════════
  // ════════════════════════════ MESSAGES ═════════════════════════════════════════
  // ═══════════════════════════════════════════════════════════════════════════════

  // Add a message to the conversation with a unique UUID
  public void AddMessage(Message message)
  {
    // If no active conversation, create one
    if (_activeConversation is null)
    {
      _activeConversation = new Conversation(title: message.Content);
      _activeConversation.UserId = _userId;
    }

    message.UserId = _userId;
    message.ConversationId = _activeConversation.Id;

    // Update conversation timestamp
    _activeConversation.Touch();

    _messages.Add(message);

    // Console.WriteLine("message: " + JsonSerializer.Serialize(message, new JsonSerializerOptions
    // {
    //   WriteIndented = true
    // }));
  }

  // Get all messages in the conversation
  public List<Message> GetMessages() => _messages.ToList();

  // Get the most recent messages up to a specified number of exchanges
  public List<Message> GetRecentMessages(int? messagesCount = null)
  {
    int count = messagesCount ?? GetRecentMessagesCount();
    if (_messages.Count <= count)
    {
      return _messages.ToList(); // Return all if we have fewer than the limit
    }

    return _messages.Skip(_messages.Count - count).ToList();
  }

  // Clear the conversation and cached documents
  public void ClearMessages()
  {
    _messages.Clear();
    _cachedDocuments.Clear();
    _activeConversation = null;
  }

  // hydrate the ConversationService with a conversation + messages from the DB
  public void SetActiveConversation(Conversation conversation, List<Message> messages)
  {
    _activeConversation = conversation;
    _messages.Clear();
    _messages.AddRange(messages.OrderBy(m => m.CreatedAt));
  }

  public Conversation? GetActiveConversation()
  {
    return _activeConversation;
  }

  // ═══════════════════════════════════════════════════════════════════════════════
  // ════════════════════════════ DOCUMENTS ════════════════════════════════════════
  // ═══════════════════════════════════════════════════════════════════════════════

  // Cache documents for reuse in the conversation, ensuring no duplicates (by title + chunk)
  public void CacheDocuments(List<Source> documents)
  {
    foreach (var doc in documents)
    {
      // Only add if it is a new document (matching both title and content)
      if (!_cachedDocuments.Any(d => d.Title == doc.Title && d.Chunk == doc.Chunk))
      {
        _cachedDocuments.Add(doc);
      }
    }

    // Trim the cache to the maximum size
    var maxCachedDocuments = GetMaxCachedDocumentsCount();
    if (_cachedDocuments.Count > maxCachedDocuments)
    {
      _cachedDocuments.RemoveRange(0, _cachedDocuments.Count - maxCachedDocuments);
    }
  }

  // Retrieve cached documents
  public List<Source> GetCachedDocuments() => _cachedDocuments.ToList();

  // ═══════════════════════════════════════════════════════════════════════════════
  // ════════════════════════════ DATABASE OPERATIONS ══════════════════════════════
  // ═══════════════════════════════════════════════════════════════════════════════

  // Save a message to the database
  public async Task SaveMessage(Message message)
  {
    if (string.IsNullOrEmpty(_userId))
    {
      _logger.LogWarning("ConversationService.SaveMessage: User ID is null.");
      return;
    }

    if (!IsInitialized())
    {
      _logger.LogWarning("ConversationService.SaveMessage: Database not initialized.");
      return;
    }

    await _databaseService.SaveConversationAsync(_activeConversation!);
    await _databaseService.SaveMessageAsync(message);
  }

  public async Task<List<ConversationWithMessages>> GetConversationThreadsAsync()
  {
    if (string.IsNullOrEmpty(_userId))
    {
      _logger.LogWarning("ConversationService.GetChatHistoryForCurrentUser: User ID is null.");
      return new List<ConversationWithMessages>();
    }

    if (!IsInitialized())
    {
      _logger.LogWarning("ConversationService.GetChatHistoryForCurrentUser: Database not initialized.");
      return new List<ConversationWithMessages>();
    }

    var conversations = await _databaseService.GetConversationsForUserAsync(_userId);
    var messages = await _databaseService.GetMessagesForUserAsync(_userId);

    var grouped = messages
        .Where(m => !string.IsNullOrEmpty(m.ConversationId))
        .GroupBy(m => m.ConversationId!)
        .ToDictionary(g => g.Key, g => g.ToList());

    var result = new List<ConversationWithMessages>();

    foreach (var convo in conversations.OrderByDescending(c => c.UpdatedAt))
    {
      var convoId = convo.Id;
      var convoMessages = grouped.ContainsKey(convoId) ? grouped[convoId] : new List<Message>();

      result.Add(new ConversationWithMessages(convo) { Messages = convoMessages.OrderBy(m => m.CreatedAt).ToList() });
    }

    return result;
  }

  public async Task<ConversationWithMessages?> GetConversationByIdAsync(Guid conversationId)
  {
    if (string.IsNullOrEmpty(_userId))
    {
      _logger.LogWarning("ConversationService.GetConversationByIdAsync: User ID is null.");
      return null;
    }

    if (!IsInitialized())
    {
      _logger.LogWarning("ConversationService.GetConversationByIdAsync: Database not initialized.");
      return null;
    }

    var convo = await _databaseService.GetConversationByIdAsync(conversationId.ToString());
    if (convo is null || convo.UserId != _userId)
    {
      _logger.LogWarning("ConversationService.GetConversationByIdAsync: Conversation not found or unauthorized.");
      return null;
    }

    var messages = await _databaseService.GetMessagesForConversationAsync(conversationId.ToString());

    SetActiveConversation(conversation: convo, messages: messages);

    return new ConversationWithMessages(convo)
    {
      Messages = messages.OrderBy(m => m.CreatedAt).ToList()
    };
  }

  public async Task<bool> DeleteConversationByIdAsync(Guid conversationId)
  {
    if (string.IsNullOrEmpty(_userId))
    {
      _logger.LogWarning("ConversationService.DeleteConversationAsync: User ID is null.");
      return false;
    }

    if (!IsInitialized())
    {
      _logger.LogWarning("ConversationService.DeleteConversationAsync: Database not initialized.");
      return false;
    }

    var convo = await _databaseService.GetConversationByIdAsync(conversationId.ToString());

    if (convo is null || convo.UserId != _userId)
    {
      _logger.LogWarning("ConversationService.DeleteConversationAsync: Conversation not found or unauthorized.");
      return false;
    }

    var messages = await _databaseService.GetMessagesForConversationAsync(conversationId.ToString());

    await _databaseService.DeleteMessagesAsync(messages);
    await _databaseService.DeleteConversationAsync(convo);

    return true;
  }
}
