namespace NotificationsLambda.Models;

public record UserCreatedEvent(
    Guid UserId,
    string Name,
    string Email);

public record PaymentProcessedEvent(
    Guid OrderId,
    Guid UserId,
    string UserEmail,
    Guid GameId,
    string GameName,
    decimal Price,
    string Status);

public record NotificationRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string UserId { get; init; } = default!;
    public string EventType { get; init; } = default!;
    public string Subject { get; init; } = default!;
    public string Body { get; init; } = default!;
    public string CreatedAt { get; init; } = DateTime.UtcNow.ToString("O");
    public long Ttl { get; init; } = DateTimeOffset.UtcNow.AddDays(90).ToUnixTimeSeconds();
}
