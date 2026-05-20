using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationsLambda.Handlers;
using NotificationsLambda.Models;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace NotificationsLambda;

public class Function
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Function> _logger;

    // Sufixos dos nomes das filas SQS para determinar o tipo de evento
    private const string UserCreatedQueueSuffix    = "user-created-events";
    private const string PaymentProcessedQueueSuffix = "payment-processed-events";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Function()
    {
        _serviceProvider = Startup.BuildServiceProvider();
        _logger          = _serviceProvider.GetRequiredService<ILogger<Function>>();
    }

    // Construtor para testes
    public Function(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger          = serviceProvider.GetRequiredService<ILogger<Function>>();
    }

    /// <summary>
    /// Entry point da Lambda — triggered por SQS (UserCreatedEvent e PaymentProcessedEvent).
    /// Retorna SQSBatchResponse para suporte a ReportBatchItemFailures.
    /// </summary>
    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        _logger.LogInformation(
            "Lambda triggered — {Count} message(s) received",
            sqsEvent.Records.Count);

        var batchFailures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                await ProcessRecord(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process SQS message — messageId={MessageId}",
                    record.MessageId);

                batchFailures.Add(new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = record.MessageId
                });
            }
        }

        _logger.LogInformation(
            "Batch complete — {Success} ok, {Failed} failed",
            sqsEvent.Records.Count - batchFailures.Count, batchFailures.Count);

        return new SQSBatchResponse { BatchItemFailures = batchFailures };
    }

    private async Task ProcessRecord(SQSEvent.SQSMessage record)
    {
        var queueArn  = record.EventSourceArn ?? string.Empty;
        var queueName = queueArn.Split(':').LastOrDefault() ?? string.Empty;

        _logger.LogInformation(
            "Processing message — messageId={MessageId} queue={Queue}",
            record.MessageId, queueName);

        if (queueName.Contains(UserCreatedQueueSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var evt = JsonSerializer.Deserialize<UserCreatedEvent>(record.Body, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize UserCreatedEvent");

            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<UserCreatedHandler>();
            await handler.HandleAsync(evt);
        }
        else if (queueName.Contains(PaymentProcessedQueueSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var evt = JsonSerializer.Deserialize<PaymentProcessedEvent>(record.Body, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize PaymentProcessedEvent");

            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<PaymentProcessedHandler>();
            await handler.HandleAsync(evt);
        }
        else
        {
            _logger.LogWarning(
                "Unknown queue — messageId={MessageId} queue={Queue}. Message ignored.",
                record.MessageId, queueName);
        }
    }
}
