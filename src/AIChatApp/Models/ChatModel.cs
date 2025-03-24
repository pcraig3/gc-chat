namespace AIChatApp.Models;

public record ChatRequest(List<Message> Messages)
{ }

public class Message
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type => "message";
    public bool IsAssistant { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Role => IsAssistant ? "assistant" : "user";
    public string Content { get; set; } = string.Empty;
    public List<Source>? Sources { get; set; }
    public string? UserId { get; set; }
    public string? ConversationId { get; set; }
    public int? Feedback { get; set; } // -1 = negative, 1 = positive, null = none
    public string? FeedbackMessage { get; set; }
    public string Status { get; set; } = "success"; // success, cancel, error

    public Message() { }

    public Message(bool isAssistant, string content)
    {
        IsAssistant = isAssistant;
        Content = content;
    }
}

public class Source
{
    public string Title { get; set; }
    public string Chunk { get; set; }
    public string Culture { get; set; }
    public string Url { get; set; }
    public int Size { get; set; }

    // Constructor for easier creation
    public Source(string title, string chunk, string culture, string url, int size)
    {
        Title = title;
        Chunk = chunk;
        Culture = culture;
        Url = url;
        Size = size;
    }

    // Method to append text to chunk
    public void AppendToChunk(string additionalChunk)
    {
        Chunk += additionalChunk;
    }
}
