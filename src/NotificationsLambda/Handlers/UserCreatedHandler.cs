using Microsoft.Extensions.Logging;
using NotificationsLambda.Models;
using NotificationsLambda.Repositories;

namespace NotificationsLambda.Handlers;

public class UserCreatedHandler(
    INotificationRepository repository,
    ILogger<UserCreatedHandler> logger)
{
    public async Task HandleAsync(UserCreatedEvent evt, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Processing UserCreatedEvent — userId={UserId} email={Email}",
            evt.UserId, evt.Email);

        logger.LogInformation(
            "[EMAIL] To: {Email} | Subject: Bem-vindo à FiapCloudGames, {Name}! | Body: Sua conta foi criada com sucesso.",
            evt.Email, evt.Name);

        var record = new NotificationRecord
        {
            UserId    = evt.UserId.ToString(),
            EventType = "WelcomeEmail",
            Subject   = $"Bem-vindo à FiapCloudGames, {evt.Name}!",
            Body      = $"Olá {evt.Name}, sua conta foi criada com sucesso. E-mail: {evt.Email}"
        };

        await repository.SaveAsync(record, ct);

        logger.LogInformation(
            "WelcomeEmail notification logged for userId={UserId}",
            evt.UserId);
    }
}
