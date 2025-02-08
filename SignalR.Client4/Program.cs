using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Winton.Extensions.Configuration.Consul;

class Program
{
    static async Task Main(string[] args)
    {
        // 設定ファイルを読み込む
        var configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) // JSONファイルを読み込む
                        .AddConsul("appsettings", options =>  // 2. Consul から設定を取得
                        {
                            options.ConsulConfigurationOptions = cco =>
                            {
                                cco.Address = new Uri("http://localhost:8500");  // Consul の URL
                            };
                            options.Optional = true;  // Consul が落ちている場合でもエラーにならない
                            options.ReloadOnChange = true;  // Consul の変更を即時反映
                        })
                        .Build();

        // Serilog を appsettings.json から設定
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration) // 設定を適用
            .CreateLogger();

        // Set up a DI container and add Serilog as the logging provider.
        using var serviceProvider = new ServiceCollection()
            .AddLogging(loggingBuilder =>
            {
                // Clear default providers and add Serilog
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog();
            })
            .BuildServiceProvider();

        // Get an ILogger instance from the DI container.
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        // Build the SignalR HubConnection and configure its logging to use Serilog.
        var connection = new HubConnectionBuilder()
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
        connection.On<string>(nameof(IHubClient.ReceiveMessage), async (message) =>
        {
            logger.LogInformation("Received message from server: {Message}", message);

            // Simulate a process that takes some time (here, 1 second)
            await Task.Delay(TimeSpan.FromSeconds(1));

            string result = message.ToUpper();

            // Send the result back to the server.
            await connection.InvokeAsync(nameof(IHubServer.ReceiveResult), result);
            logger.LogInformation("Sent result to server: {Result}", result);
        });

        // Start the connection.
        try
        {
            await connection.StartAsync();
            logger.LogInformation("Connected to SignalR server.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to SignalR server.");
            return;
        }

        logger.LogInformation("Waiting for messages. Press Enter to exit.");
        Console.ReadLine();

        // Close the connection and flush Serilog logs.
        await connection.StopAsync();
        Log.CloseAndFlush();
    }
}