using Microsoft.Extensions.Logging;
using NotificationsLambda.Models;
using NotificationsLambda.Repositories;

namespace NotificationsLambda.Handlers;

public class PaymentProcessedHandler(
    INotificationRepository repository,
    ILogger<PaymentProcessedHandler> logger)
{
    public async Task HandleAsync(PaymentProcessedEvent evt, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Processing PaymentProcessedEvent — orderId={OrderId} userId={UserId} status={Status}",
            evt.OrderId, evt.UserId, evt.Status);

        var isApproved = evt.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase);
        var eventType  = isApproved ? "PaymentConfirmation" : "PaymentRejection";

        string subject, body;

        if (isApproved)
        {
            subject = $"Compra confirmada: {evt.GameName}";
            body    = $"Olá! Sua compra do jogo '{evt.GameName}' foi aprovada. " +
                      $"Valor: R$ {evt.Price:F2} | Pedido: {evt.OrderId}";
        }
        else
        {
            subject = $"Pagamento recusado: {evt.GameName}";
            body    = $"Olá! O pagamento do jogo '{evt.GameName}' foi recusado. " +
                      $"Tente novamente com outro método de pagamento. Pedido: {evt.OrderId}";
        }

        logger.LogInformation(
            "[EMAIL] To: {Email} | Subject: {Subject} | Body: {Body}",
            evt.UserEmail, subject, body);

        var record = new NotificationRecord
        {
            UserId    = evt.UserId.ToString(),
            EventType = eventType,
            Subject   = subject,
            Body      = body
        };

        await repository.SaveAsync(record, ct);

        logger.LogInformation(
            "{EventType} notification logged for userId={UserId} orderId={OrderId}",
            eventType, evt.UserId, evt.OrderId);
    }
}
