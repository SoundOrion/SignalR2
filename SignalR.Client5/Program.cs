using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using dotnet_etcd;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

class Program
{
    static async Task Main(string[] args)
    {
        var etcdUrl = "http://localhost:2379"; // etcd のエンドポイント
        var etcdClient = new EtcdClient(etcdUrl);

        // 設定ファイルを読み込み、etcd から設定を取得して適用
        var configurationBuilder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true); // JSONファイルを読み込む

        // etcd から設定を取得して追加
        var etcdKey = "/config/AppSettings/Message";
        var etcdValue = await GetEtcdValueAsync(etcdClient, etcdKey);
        if (!string.IsNullOrEmpty(etcdValue))
        {
            configurationBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("AppSettings:Message", etcdValue)
            });
        }

        var configuration = configurationBuilder.Build();

        // Serilog を appsettings.json から設定
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration) // 設定を適用
            .CreateLogger();

        using var serviceProvider = new ServiceCollection()
            .AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog();
            })
            .BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        // Build the SignalR HubConnection and configure its logging to use Serilog.
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5051/myHub") // Replace with your server's URL if different.
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSerilog();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        // Set up a type-safe method registration for receiving messages.
        connection.On<string>("ReceiveMessage", async (message) =>
        {
            logger.LogInformation("Received message from server: {Message}", message);

            // Simulate a process that takes some time (here, 1 second)
            await Task.Delay(TimeSpan.FromSeconds(1));

            string result = message.ToUpper();

            // Send the result back to the server.
            await connection.InvokeAsync("ReceiveResult", result);
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

    // etcd から設定を取得するメソッド
    static async Task<string> GetEtcdValueAsync(EtcdClient client, string key)
    {
        try
        {
            var response = await client.GetValAsync(key);
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching from etcd: {ex.Message}");
        }
        return null;
    }
}
