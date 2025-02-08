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
            .MinimumLevel.Debug()
            .WriteTo.Console()    // コンソール出力
            .WriteTo.File("Logs/ClientLog.txt", rollingInterval: RollingInterval.Day) // ファイル出力（毎日ローテーション）
            .CreateLogger();

        try
        {
            // Create and configure the host
            using IHost host = Host.CreateDefaultBuilder(args)
                .UseSerilog() // Serilog をログプロバイダとして使用
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<SignalRClientService>();
                })
                .Build();

            // サービスを実行
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            // ログをフラッシュして終了
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SignalR Client Service is starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5051/myHub") // サーバーのURLを指定
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddSerilog();
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .Build();

            _connection.On<string>(nameof(IHubClient.ReceiveMessage), async (message) =>
            {
                _logger.LogInformation("Received message from server: {Message}", message);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); // キャンセル対応
                string result = message.ToUpper();
                await _connection.InvokeAsync(nameof(IHubServer.ReceiveResult), result);
                _logger.LogInformation("Sent result to server: {Result}", result);
            });

            try
            {
                await _connection.StartAsync(stoppingToken);
                _logger.LogInformation("Connected to SignalR server.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to SignalR server. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue; // 再接続を試みる
            }

            // 接続が切れたら再接続
            _connection.Closed += async (error) =>
            {
                _logger.LogWarning("Connection closed. Reconnecting in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                await _connection.StartAsync(stoppingToken);
            };

            // 無限ループで待機（Ctrl+Cで停止可能）
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("SignalR Client Service is stopping...");
            }
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
    }
}
