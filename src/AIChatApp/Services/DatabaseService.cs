using AIChatApp.Models;
using Microsoft.Azure.Cosmos;

namespace AIChatApp.Services;

public class DatabaseService
{
  // ═══════════════════════════════════════════════════════════════════════════════
  // ════════════════════════════ INITIALIZATION AND SETUP ═════════════════════════
  // ═══════════════════════════════════════════════════════════════════════════════

  private readonly Container? _container;

  public DatabaseService(IConfiguration config, CosmosClient? cosmosClient = null)
  {
    // Only configure if CosmosClient is provided
    if (cosmosClient is not null)
    {
      _container = cosmosClient.GetContainer(config["AZURE_COSMOSDB_DATABASE_NAME"], config["AZURE_COSMOSDB_CONTAINER_NAME"]);
    }
  }

  // Check if the database is properly initialized
  public bool IsInitialized() => _container is not null;

  // ═══════════════════════════════════════════════════════════════════════════════
  // ════════════════════════════════════ MESSAGES ═════════════════════════════════
  // ═══════════════════════════════════════════════════════════════════════════════

  public async Task SaveMessageAsync(Message message)
  {
    if (!IsInitialized()) return;
    await _container!.UpsertItemAsync(message, new PartitionKey(message.UserId));
  }

  public async Task<List<Message>> GetMessagesWithFeedbackAsync()
  {
    if (!IsInitialized()) return new List<Message>();

    var query = new QueryDefinition(@"
      SELECT * FROM c
      WHERE c.type = @type
      AND (c.feedback = 1 OR c.feedback = -1)
    ").WithParameter("@type", "message");

    var messages = new List<Message>();
    using var iterator = _container!.GetItemQueryIterator<Message>(query);
    while (iterator.HasMoreResults)
    {
      var response = await iterator.ReadNextAsync();
      messages.AddRange(response);
    }

    return messages;
  }

  public async Task<List<Message>> GetMessagesForConversationAsync(string conversationId)
  {
    if (!IsInitialized()) return new List<Message>();

    var query = new QueryDefinition("SELECT * FROM c WHERE c.conversationId = @conversationId AND c.type = @type")
        .WithParameter("@conversationId", conversationId)
        .WithParameter("@type", "message");

    var messages = new List<Message>();
    using var iterator = _container!.GetItemQueryIterator<Message>(query);
    while (iterator.HasMoreResults)
    {
      var response = await iterator.ReadNextAsync();
      messages.AddRange(response);
    }

    return messages;
  }

  public async Task<List<Message>> GetMessagesForUserAsync(string userId)
  {
    if (!IsInitialized()) return new List<Message>();

    var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId AND c.type = @type")
        .WithParameter("@userId", userId)
        .WithParameter("@type", "message");

    var messages = new List<Message>();
    using var iterator = _container!.GetItemQueryIterator<Message>(query);
    while (iterator.HasMoreResults)
    {
      var response = await iterator.ReadNextAsync();
      messages.AddRange(response);
    }

    return messages;
  }

  public async Task DeleteMessagesAsync(IEnumerable<Message> messages)
  {
    if (!IsInitialized()) return;

    foreach (var message in messages)
    {
      await _container!.DeleteItemAsync<Message>(message.Id, new PartitionKey(message.UserId));
    }
  }

  // ═══════════════════════════════════════════════════════════════════════════════
  // ══════════════════════════════ CONVERSATIONS ══════════════════════════════════
  // ═══════════════════════════════════════════════════════════════════════════════

  public async Task SaveConversationAsync(Conversation conversation)
  {
    if (!IsInitialized()) return;

    await _container!.UpsertItemAsync(conversation, new PartitionKey(conversation.UserId));
  }

  public async Task<Conversation?> GetConversationByIdAsync(string conversationId)
  {
    if (!IsInitialized()) return null;

    var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id AND c.type = @type")
        .WithParameter("@id", conversationId)
        .WithParameter("@type", "conversation");

    using var iterator = _container!.GetItemQueryIterator<Conversation>(query);
    while (iterator.HasMoreResults)
    {
      var response = await iterator.ReadNextAsync();
      if (response.Any())
      {
        return response.First();
      }
    }

    return null;
  }

  public async Task<List<Conversation>> GetConversationsForUserAsync(string userId)
  {
    if (!IsInitialized()) return new List<Conversation>();

    var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId AND c.type = @type")
        .WithParameter("@userId", userId)
        .WithParameter("@type", "conversation");

    var conversations = new List<Conversation>();
    using var iterator = _container!.GetItemQueryIterator<Conversation>(query);
    while (iterator.HasMoreResults)
    {
      var response = await iterator.ReadNextAsync();
      conversations.AddRange(response);
    }

    return conversations;
  }

  public async Task DeleteConversationAsync(Conversation conversation)
  {
    if (!IsInitialized()) return;

    await _container!.DeleteItemAsync<Conversation>(conversation.Id, new PartitionKey(conversation.UserId));
  }
}
