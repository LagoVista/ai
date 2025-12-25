using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Publishes LLM lifecycle events to the notification pipeline.
    /// </summary>
    public sealed class LlmEventPublisher : ILLMEventPublisher
    {
        private readonly INotificationPublisher _publisher;
        private readonly IAdminLogger _logger;

        public LlmEventPublisher(INotificationPublisher publisher, IAdminLogger logger)
        {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task PublishAsync(string sessionId, string stage, string status, string message, double? elapsedMs, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) { return; }
            if (cancellationToken.IsCancellationRequested) { return; } // publisher has no CT overload in current codebase

            var evt = new AptixOrchestratorEvent
            {
                SessionId = sessionId,
                TurnId = null,
                Stage = stage,
                Status = status,
                Message = message,
                ElapsedMs = elapsedMs,
                Timestamp = DateTime.UtcNow.ToJSONString()
            };

            try
            {
                await _publisher.PublishAsync(Targets.WebSocket, Channels.Entity, sessionId, evt, NotificationVerbosity.Diagnostics);
            }
            catch (Exception ex)
            {
                _logger.AddException("[LlmEventPublisher_PublishAsync__Exception]", ex);
            }
        }
    }
}
