public class Conversation
{
  public string Id { get; set; } = Guid.NewGuid().ToString(); // Unique ID (UUID)
  public string Type => "conversation"; // CosmosDB discriminator
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Set when created
  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow; // Can be updated on change
  public string? UserId { get; set; } // Assigned when saved
  public string Title { get; set; } = string.Empty; // Editable

  public Conversation() { }

  public Conversation(string title)
  {
    Title = title;
  }

  public void Touch()
  {
    UpdatedAt = DateTime.UtcNow;
  }

  public void UpdateTitle(string newTitle)
  {
    Title = newTitle;
    Touch();
  }
}
