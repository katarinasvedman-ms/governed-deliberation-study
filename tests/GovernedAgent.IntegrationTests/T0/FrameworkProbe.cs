using System.Collections.Immutable;
using Microsoft.Agents.AI.Workflows;
using Xunit.Abstractions;

namespace GovernedAgent.IntegrationTests.T0;

// Test-only vocabulary, not the T1 component contracts or research event schema.
public enum ProbeDecision { Query, Report, Focus }

internal sealed record ProbeSnapshot(
    string Id, int Generation, ImmutableArray<string> Observations, string? Guidance);

internal sealed class ProbeTrace(ITestOutputHelper output)
{
    private readonly object _sync = new();
    private readonly List<string> _entries = [];
    private int _stage;

    public string[] Entries
    {
        get { lock (_sync) { return _entries.ToArray(); } }
    }

    public void AdvanceTo(int stage)
    {
        lock (_sync)
        {
            Assert.True(stage > _stage, "Probe stages must advance monotonically.");
            _stage = stage;
        }
    }

    public void Record(string detail)
    {
        lock (_sync)
        {
            var entry = $"{_entries.Count + 1:D2} stage={_stage} {detail}";
            _entries.Add(entry);
            output.WriteLine(entry);
        }
    }
}

internal sealed class ProbeExecutor<TInput, TOutput>(
    string id,
    Func<TInput, CancellationToken, ValueTask<TOutput>> handler)
    : Executor<TInput, TOutput>(id)
{
    public override ValueTask<TOutput> HandleAsync(
        TInput message, IWorkflowContext context, CancellationToken cancellationToken = default) =>
        handler(message, cancellationToken);
}

internal static class FrameworkProbe
{
    public static TaskCompletionSource<T> Signal<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static async Task<TOutput> InvokeAsync<TInput, TOutput>(
        ProbeExecutor<TInput, TOutput> executor,
        TInput input,
        CancellationToken cancellationToken) where TInput : notnull
    {
        var workflow = new WorkflowBuilder(executor).WithOutputFrom(executor).Build();
        await using var run = await InProcessExecution.RunAsync(
            workflow, input, cancellationToken: cancellationToken);
        var events = run.NewEvents.ToArray();
        Assert.DoesNotContain(events, item => item is ExecutorFailedEvent);
        Assert.Single(events.OfType<ExecutorInvokedEvent>());
        Assert.Single(events.OfType<ExecutorCompletedEvent>());
        return Assert.IsType<TOutput>(Assert.Single(events.OfType<WorkflowOutputEvent>()).Data);
    }
}

internal sealed class ProbeCall : IDisposable
{
    private readonly CancellationTokenSource _componentCancellation = new();
    private readonly TaskCompletionSource<bool> _started = FrameworkProbe.Signal<bool>();
    private readonly CancellationToken _lifetime;
    private readonly ProbeTrace _trace;

    public ProbeCall(
        string id, ProbeSnapshot snapshot, ProbeTrace trace, CancellationToken lifetime)
    {
        Id = id;
        Snapshot = snapshot;
        _trace = trace;
        _lifetime = lifetime;
        var executor = new ProbeExecutor<ProbeSnapshot, ProbeDecision>(id, async (input, token) =>
        {
            trace.Record($"framework.started id={id} snapshot={input.Id} generation={input.Generation}");
            _started.SetResult(true);
            // Deliberately ignore component cancellation, but never the test-lifetime watchdog.
            var decision = await Release.Task.WaitAsync(token);
            CancellationWasIgnored = _componentCancellation.IsCancellationRequested;
            trace.Record(
                $"framework.returned id={id} decision={decision} cancellationIgnored={CancellationWasIgnored}");
            return decision;
        });
        Result = FrameworkProbe.InvokeAsync(executor, snapshot, lifetime);
    }

    public string Id { get; }
    public ProbeSnapshot Snapshot { get; }
    public Task Started => _started.Task.WaitAsync(_lifetime);
    public TaskCompletionSource<ProbeDecision> Release { get; } = FrameworkProbe.Signal<ProbeDecision>();
    public Task<ProbeDecision> Result { get; }
    public bool CancellationWasIgnored { get; private set; }

    public void RequestCancellation()
    {
        _trace.Record($"cancellation.requested id={Id}");
        _componentCancellation.Cancel();
        Assert.False(Result.IsCompleted, "The scripted call must still be draining.");
        _trace.Record($"cancellation.unsettled id={Id}");
    }

    public void Dispose() => _componentCancellation.Dispose();
}
