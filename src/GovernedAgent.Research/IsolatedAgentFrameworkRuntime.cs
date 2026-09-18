using Microsoft.Agents.AI.Workflows;

namespace GovernedAgent.Research;

public enum InvocationCancellationCapability
{
    Supported,
    Unsupported,
    RequestFails
}

public enum InvocationCancellationRequest
{
    Requested,
    Unsupported,
    Failed
}

public sealed record IsolatedInvocationOutcome<T>(
    string Settlement,
    T? Output,
    string? FailureCode,
    bool CancellationWasRequested);

public sealed class IsolatedFrameworkInvocation<T> : IAsyncDisposable
{
    private readonly CancellationTokenSource _componentCancellation = new();
    private readonly InvocationCancellationCapability _cancellationCapability;

    internal IsolatedFrameworkInvocation(
        string invocationId,
        InputSnapshot snapshot,
        Func<InputSnapshot, CancellationToken, ValueTask<T>> component,
        InvocationCancellationCapability cancellationCapability,
        CancellationToken lifetime)
    {
        InvocationId = invocationId;
        Snapshot = snapshot;
        _cancellationCapability = cancellationCapability;
        var started = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Started = started.Task.WaitAsync(lifetime);
        Settlement = InvokeAsync(
            invocationId,
            snapshot,
            component,
            started,
            _componentCancellation.Token,
            lifetime);
    }

    public string InvocationId { get; }

    public InputSnapshot Snapshot { get; }

    public Task Started { get; }

    public Task<IsolatedInvocationOutcome<T>> Settlement { get; }

    public bool CancellationWasRequested => _componentCancellation.IsCancellationRequested;

    public InvocationCancellationRequest RequestCancellation()
    {
        switch (_cancellationCapability)
        {
            case InvocationCancellationCapability.Supported:
                try
                {
                    _componentCancellation.Cancel();
                    return InvocationCancellationRequest.Requested;
                }
                catch (Exception)
                {
                    return InvocationCancellationRequest.Failed;
                }
            case InvocationCancellationCapability.Unsupported:
                return InvocationCancellationRequest.Unsupported;
            case InvocationCancellationCapability.RequestFails:
                return InvocationCancellationRequest.Failed;
            default:
                throw new InvalidOperationException("Unsupported cancellation capability.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Task.WhenAny(Settlement, Task.Delay(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            _componentCancellation.Dispose();
        }
    }

    private static async Task<IsolatedInvocationOutcome<T>> InvokeAsync(
        string invocationId,
        InputSnapshot snapshot,
        Func<InputSnapshot, CancellationToken, ValueTask<T>> component,
        TaskCompletionSource started,
        CancellationToken componentCancellation,
        CancellationToken lifetime)
    {
        var executor = new ComponentExecutor<T>(
            invocationId,
            async input =>
            {
                started.TrySetResult();
                try
                {
                    var output = await component(input, componentCancellation);
                    return new IsolatedInvocationOutcome<T>(
                        "returned",
                        output,
                        null,
                        componentCancellation.IsCancellationRequested);
                }
                catch (OperationCanceledException) when (componentCancellation.IsCancellationRequested)
                {
                    return new IsolatedInvocationOutcome<T>(
                        "cancelled",
                        default,
                        "component-cancelled",
                        true);
                }
                catch (Exception exception)
                {
                    return new IsolatedInvocationOutcome<T>(
                        "failed",
                        default,
                        exception.GetType().Name,
                        componentCancellation.IsCancellationRequested);
                }
            });
        var workflow = new WorkflowBuilder(executor).WithOutputFrom(executor).Build();
        await using var run = await InProcessExecution.RunAsync(
            workflow,
            snapshot,
            cancellationToken: lifetime);
        var events = run.NewEvents.ToArray();
        if (events.Any(item => item is ExecutorFailedEvent))
        {
            throw new InvalidOperationException("The isolated Agent Framework executor failed.");
        }

        if (events.OfType<ExecutorInvokedEvent>().Count() != 1 ||
            events.OfType<ExecutorCompletedEvent>().Count() != 1)
        {
            throw new InvalidOperationException(
                "The isolated Agent Framework invocation did not execute exactly once.");
        }

        return events
            .OfType<WorkflowOutputEvent>()
            .Select(item => item.Data)
            .OfType<IsolatedInvocationOutcome<T>>()
            .Single();
    }

    private sealed class ComponentExecutor<TOutput>(
        string id,
        Func<InputSnapshot, ValueTask<IsolatedInvocationOutcome<TOutput>>> component)
        : Executor<InputSnapshot, IsolatedInvocationOutcome<TOutput>>(id)
    {
        public override ValueTask<IsolatedInvocationOutcome<TOutput>> HandleAsync(
            InputSnapshot message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default) =>
            component(message);
    }
}

public sealed class IsolatedAgentFrameworkRuntime
{
    public IsolatedFrameworkInvocation<ActorDecision> StartActor(
        string actorTurnId,
        InputSnapshot snapshot,
        Func<InputSnapshot, CancellationToken, ValueTask<ActorDecision>> component,
        InvocationCancellationCapability cancellationCapability,
        CancellationToken lifetime) =>
        new(
            actorTurnId,
            snapshot,
            component,
            cancellationCapability,
            lifetime);

    public IsolatedFrameworkInvocation<SupervisorOutput> StartSupervisor(
        string reviewId,
        InputSnapshot snapshot,
        Func<InputSnapshot, CancellationToken, ValueTask<SupervisorOutput>> component,
        InvocationCancellationCapability cancellationCapability,
        CancellationToken lifetime) =>
        new(
            reviewId,
            snapshot,
            component,
            cancellationCapability,
            lifetime);
}
