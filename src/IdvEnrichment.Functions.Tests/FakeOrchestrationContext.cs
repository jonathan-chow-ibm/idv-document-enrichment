using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdvEnrichment.Functions.Tests;

/// <summary>
/// Minimal <see cref="TaskOrchestrationContext"/> stand-in for testing orchestrator control flow.
/// Only the members the orchestrators under test actually call are implemented; the rest throw so a
/// test that starts depending on them fails loudly instead of silently exercising a stub.
/// </summary>
internal sealed class FakeOrchestrationContext(object? input = null) : TaskOrchestrationContext
{
    private readonly TaskCompletionSource timer = new();

    public override string InstanceId => "fake-instance";

    public override DateTime CurrentUtcDateTime { get; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public override bool IsReplaying => false;

    protected override ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;

    public override TaskName Name => "FakeOrchestration";

    public override ParentOrchestrationInstance? Parent => null;

    /// <summary>Handles each sub-orchestration call; the returned task is what the orchestrator awaits.</summary>
    public Func<TaskName, object?, Task<object?>> SubOrchestratorHandler { get; init; } =
        (name, _) => throw new InvalidOperationException($"Unexpected sub-orchestration call: {name}");

    /// <summary>Deadlines passed to <see cref="CreateTimer"/>, in call order.</summary>
    public List<DateTime> TimerDeadlines { get; } = [];

    public bool TimerCancelled { get; private set; }

    /// <summary>Completes every timer created by the orchestrator, simulating expiry.</summary>
    public void FireTimers() => timer.TrySetResult();

    public override T? GetInput<T>() where T : default => (T?)input;

    public override async Task<TResult> CallSubOrchestratorAsync<TResult>(
        TaskName orchestratorName, object? input = null, TaskOptions? options = null) =>
        (TResult)(await SubOrchestratorHandler(orchestratorName, input))!;

    public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
    {
        TimerDeadlines.Add(fireAt);
        cancellationToken.Register(() => TimerCancelled = true);
        return timer.Task;
    }

    public override Task<TResult> CallActivityAsync<TResult>(
        TaskName name, object? input = null, TaskOptions? options = null) => throw new NotSupportedException();

    public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true) =>
        throw new NotSupportedException();

    public override Guid NewGuid() => throw new NotSupportedException();

    public override void SendEvent(string instanceId, string eventName, object payload) =>
        throw new NotSupportedException();

    public override void SetCustomStatus(object? customStatus) => throw new NotSupportedException();

    public override Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
