/// <summary>
/// クライアントが受信するメソッド
/// </summary>
public interface IHubClient
{
    Task ReceiveMessage(string message);
}

/// <summary>
/// クライアント → サーバー メソッドの定義
/// </summary>
public interface IHubServer
{
    Task ReceiveResult(string result);
}
