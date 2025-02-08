using Microsoft.AspNetCore.SignalR;

public class MessageSenderService : BackgroundService
{
    private readonly IHubContext<MyHub, IHubClient> _hubContext;
    private readonly ILogger<MessageSenderService> _logger;

    public MessageSenderService(IHubContext<MyHub, IHubClient> hubContext, ILogger<MessageSenderService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MessageSenderService is starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, stoppingToken); // 5秒ごとにメッセージ送信
                string message = $"Server message at {DateTime.Now}";
                _logger.LogInformation("Sending message: {Message}", message);
                await _hubContext.Clients.All.ReceiveMessage(message);
            }
            catch (TaskCanceledException)
            {
                // アプリケーションのシャットダウン時に発生するため無視
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending message.");
            }
        }
    }

}
