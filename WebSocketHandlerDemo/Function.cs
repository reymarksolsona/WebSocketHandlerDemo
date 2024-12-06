using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace WebSocketHandlerDemo;

public class Function
{
    // Dictionary to keep track of client connections and their subscriptions
    private static Dictionary<string, HashSet<string>> _subscriptions = new Dictionary<string, HashSet<string>>();
    private static Dictionary<string, string> _clients = new Dictionary<string, string>(); // ClientId -> ConnectionId

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var routeKey = request.RequestContext.RouteKey;
        var connectionId = request.RequestContext.ConnectionId;

        context.Logger.LogLine($"RouteKey: {routeKey}, ConnectionId: {connectionId}");

        switch (routeKey)
        {
            case "$connect":
                return await HandleConnect(request, context);

            case "$disconnect":
                return await HandleDisconnect(request, context);

            case "subscribe":
                return await HandleSubscribe(request, context);

            case "publish":
                return await HandlePublish(request, context);

            default:
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = "Invalid route"
                };
        }
    }

    // Handle the connection event when a client connects
    private async Task<APIGatewayProxyResponse> HandleConnect(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var connectionId = request.RequestContext.ConnectionId;
        var apiKey = request.Headers["x-api-key"];

        // Log client connection
        context.Logger.LogLine($"Client connected with ConnectionId: {connectionId}, API Key: {apiKey}");

        // Add client connection to the dictionary
        _clients[connectionId] = apiKey;

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = "Connected successfully"
        };
    }

    // Handle the disconnection event when a client disconnects
    private async Task<APIGatewayProxyResponse> HandleDisconnect(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var connectionId = request.RequestContext.ConnectionId;
        var apiKey = request.Headers["x-api-key"];

        // Log client disconnection
        context.Logger.LogLine($"Client disconnected with ConnectionId: {connectionId}, API Key: {apiKey}");

        // Remove the client connection from the dictionary
        _clients.Remove(connectionId);

        // Remove the client from all topic subscriptions
        foreach (var topic in _subscriptions.Values)
        {
            topic.Remove(connectionId);
        }

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = "Disconnected successfully"
        };
    }

    // Handle subscription to a topic
    private async Task<APIGatewayProxyResponse> HandleSubscribe(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var connectionId = request.RequestContext.ConnectionId;
        var body = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Body);
        var topic = body["topic"];

        // Ensure the topic is registered
        if (!_subscriptions.ContainsKey(topic))
        {
            _subscriptions[topic] = new HashSet<string>();
        }

        // Add the connection to the topic's subscriber list
        _subscriptions[topic].Add(connectionId);

        context.Logger.LogLine($"Client {connectionId} subscribed to {topic}");

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = $"Subscribed to {topic}"
        };
    }

    // Handle publishing a message to all clients subscribed to a topic
    private async Task<APIGatewayProxyResponse> HandlePublish(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Body);
        var topic = body["topic"];
        var message = body["message"];

        if (!_subscriptions.ContainsKey(topic))
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 404,
                Body = $"Topic {topic} not found"
            };
        }

        // Get the list of all clients subscribed to this topic
        var subscribers = _subscriptions[topic];
        foreach (var connectionId in subscribers)
        {
            // TODO: Send message to each client
            context.Logger.LogLine($"Sending message to {connectionId}: {message}");
        }

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = $"Message sent to {subscribers.Count} subscribers of {topic}"
        };
    }
}
