using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace NzbWebDAV.Websocket;

public class WebsocketManager
{
    private readonly ConcurrentDictionary<WebSocket, bool> _sockets = new();

    public async Task AcceptWebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync();
        _sockets.TryAdd(socket, true);
        await WaitForClose(socket);
    }

    private static async Task WaitForClose(WebSocket socket)
    {
        var buffer = new byte[1024];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
        }
    }

    public async Task PublishAsync(WebsocketTopic topic, object payload, CancellationToken cancellationToken = default)
    {
        var message = JsonSerializer.Serialize(new { topic = topic.ToString(), payload });
        var buffer = Encoding.UTF8.GetBytes(message);
        foreach (var socket in _sockets.Keys)
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);
            }
        }
    }
}
