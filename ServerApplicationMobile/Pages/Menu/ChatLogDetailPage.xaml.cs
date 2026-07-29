using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class ChatLogDetailPage : ContentPage
{
    private readonly ChatLog _chatLog;
    private readonly ChatTranscriptService _chatTranscriptService;
    private bool _loaded;

    public ChatLogDetailPage(
        ChatLog chatLog,
        ChatTranscriptService chatTranscriptService)
    {
        InitializeComponent();
        ArgumentNullException.ThrowIfNull(chatLog);
        ArgumentNullException.ThrowIfNull(chatTranscriptService);
        _chatLog = chatLog;
        _chatTranscriptService = chatTranscriptService;
        BindingContext = chatLog;
        Title = string.IsNullOrWhiteSpace(chatLog.CustomerName) ? "Chat Log" : chatLog.CustomerName;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;

        ActivityIndicator.IsRunning = true;
        ActivityIndicator.IsVisible = true;
        TranscriptStatusLabel.IsVisible = false;

        try
        {
            TranscriptEditor.Text = await _chatTranscriptService.GetTranscriptAsync(_chatLog.ChatID);
            _loaded = true;
        }
        catch (Exception ex)
        {
            TranscriptEditor.Text = _chatLog.MessagePreview;
            TranscriptStatusLabel.Text = $"Unable to load the complete chat history: {ex.Message}";
            TranscriptStatusLabel.IsVisible = true;
        }
        finally
        {
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }
}
