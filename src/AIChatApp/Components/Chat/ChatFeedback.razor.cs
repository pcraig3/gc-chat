using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AIChatApp.Models;

namespace AIChatApp.Components.Chat;

public partial class ChatFeedback : ComponentBase
{
  [Parameter]
  public bool Positive { get; set; }
  [Parameter]
  public required Message Message { get; set; }
  [Parameter]
  public bool Disabled { get; set; } = false;
  [Inject]
  private IJSRuntime JS { get; set; } = default!;
  [Inject]
  private ConversationService ConversationHandler { get; set; } = default!;
  private string FeedbackText = "";
  private bool IsSubmitting = false;
  private bool IsFeedbackSent = false;
  private string OverlayId = $"feedback-overlay-{Guid.NewGuid():N}";
  private IJSObjectReference? _module;
  [Parameter]
  public EventCallback OnFeedbackSubmitted { get; set; }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender)
    {
      try
      {
        _module = await JS.InvokeAsync<IJSObjectReference>("import", "/js/overlay.js");
        await _module.InvokeVoidAsync("wbInitOverlay", OverlayId);
      }
      catch (JSDisconnectedException)
      {
        // Safe to ignore
      }
    }
  }

  private async Task SubmitFeedback()
  {
    IsSubmitting = true;

    Message.Feedback = Positive ? 1 : -1;
    Message.FeedbackMessage = FeedbackText;

    await ConversationHandler.SaveMessage(Message);

    IsFeedbackSent = true;
    StateHasChanged();

    await Task.Delay(1200);

    await OnFeedbackSubmitted.InvokeAsync();
    await _module!.InvokeVoidAsync("wbCloseOverlay", OverlayId);

    IsSubmitting = false;
    IsFeedbackSent = false; // reset just in case
  }
}