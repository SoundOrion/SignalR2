using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace SignalRClientUsingHostBuilder;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug() // Set the minimum logging level as needed
            .WriteTo.Console()    // Log to the console
            .WriteTo.File("Logs/ClientLog.txt", rollingInterval: RollingInterval.Day) // Log to a file
            .CreateLogger();

        try
        {
            // Create and configure the host
            using IHost host = Host.CreateDefaultBuilder(args)
                // Use Serilog as the logging provider
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    // Register our SignalR client background service
                    services.AddHostedService<SignalRClientService>();
                })
                .Build();

            // Run the host; the service will run until cancellation (e.g. Ctrl+C).
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            // Ensure to flush and stop internal timers/threads before application exit.
            Log.CloseAndFlush();
        }
    }
}

public class SignalRClientService : BackgroundService
{
    private readonly ILogger<SignalRClientService> _logger;
    private HubConnection _connection;

    public SignalRClientService(ILogger<SignalRClientService> logger)
    {
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Build the SignalR HubConnection and configure its logging to use Serilog.
        _connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5051/myHub") // Replace with your server's URL if different.
            .ConfigureLogging(logging =>
            {
                // Ensure SignalR client logs are handled by Serilog.
                logging.ClearProviders();
                logging.AddSerilog();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        // Set up a type-safe method registration for receiving messages.
        _connection.On<string>(nameof(IHubClient.ReceiveMessage), async (message) =>
        {
            _logger.LogInformation("Received message from server: {Message}", message);

            // Simulate a process that takes some time (here, 1 second)
            await Task.Delay(TimeSpan.FromSeconds(1));

            string result = message.ToUpper();

            // Send the result back to the server.
            await _connection.InvokeAsync(nameof(IHubServer.ReceiveResult), result);
            _logger.LogInformation("Sent result to server: {Result}", result);
        });

        try
        {
            await _connection.StartAsync(cancellationToken);
            _logger.LogInformation("Connected to SignalR server.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR server.");
            // Optionally, rethrow or handle the exception as needed.
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SignalR Client Service is running. Press Ctrl+C to exit.");

        // Keep the service running until cancellation is requested.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Expected when the service is stopping.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection != null)
        {
            await _connection.StopAsync(cancellationToken);
            await _connection.DisposeAsync();
        }
        _logger.LogInformation("SignalR Client Service is stopping.");
        await base.StopAsync(cancellationToken);
    }
}
