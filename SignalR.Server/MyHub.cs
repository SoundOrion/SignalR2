using Microsoft.AspNetCore.SignalR;

public class MyHub : Hub<IHubClient>, IHubServer
{
    private readonly ILogger<MyHub> _logger;

    public MyHub(ILogger<MyHub> logger)
    {
        _logger = logger;
    }

    public async Task ReceiveResult(string result)
    {
        _logger.LogInformation("Received result from client: {Result}", result);
        await Task.CompletedTask;
    }
}
