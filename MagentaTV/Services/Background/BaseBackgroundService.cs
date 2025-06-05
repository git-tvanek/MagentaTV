// Services/Background/BaseBackgroundService.cs - Enhanced Version
using MagentaTV.Configuration;
using MagentaTV.Models.Background;
using MagentaTV.Services.Background;

using Microsoft.Extensions.Options;

/// <summary>
/// Abstraktní základní třída pro všechny background services - rozšířená verze
/// </summary>
public abstract class BaseBackgroundService : BackgroundService
{
    protected readonly ILogger Logger;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly BackgroundServiceOptions Options;
    protected readonly string ServiceName;

    private readonly CancellationTokenSource _stoppingCts = new();
    private readonly Dictionary<string, object> _metrics = new();
    private DateTime _lastHeartbeat = DateTime.UtcNow;
    private readonly object _metricsLock = new();

    protected BaseBackgroundService(
        ILogger logger,
        IServiceProvider serviceProvider,
        IOptions<BackgroundServiceOptions> options,
        string? serviceName = null)
    {
        Logger = logger;
        ServiceProvider = serviceProvider;
        Options = options.Value;
        ServiceName = serviceName ?? GetType().Name;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Background service {ServiceName} started", ServiceName);

        try
        {
            // Startup delay if configured
            if (Options.StartupDelaySeconds > 0)
            {
                Logger.LogDebug("Background service {ServiceName} waiting {Delay}s before starting",
                    ServiceName, Options.StartupDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(Options.StartupDelaySeconds), stoppingToken);
            }

            // Main execution loop
            await ExecuteServiceAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Background service {ServiceName} was cancelled", ServiceName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Background service {ServiceName} failed with unhandled exception", ServiceName);

            if (Options.RestartOnFailure)
            {
                Logger.LogInformation("Attempting to restart background service {ServiceName}", ServiceName);
                // V reálné implementaci by zde bylo restart logic
            }
        }
        finally
        {
            Logger.LogInformation("Background service {ServiceName} stopped", ServiceName);
        }
    }

    /// <summary>
    /// Implementuje hlavní logiku background service
    /// </summary>
    protected abstract Task ExecuteServiceAsync(CancellationToken stoppingToken);

    /// <summary>
    /// Bezpečné vykonání akce s error handlingem
    /// </summary>
    protected async Task<bool> ExecuteSafelyAsync(
        Func<Task> action,
        string actionName,
        int maxRetries = 3,
        TimeSpan? retryDelay = null)
    {
        retryDelay ??= TimeSpan.FromSeconds(5);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await action();

                if (attempt > 1)
                {
                    Logger.LogInformation("Action {ActionName} succeeded on attempt {Attempt}", actionName, attempt);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Action {ActionName} failed on attempt {Attempt}/{MaxRetries}",
                    actionName, attempt, maxRetries);

                if (attempt == maxRetries)
                {
                    Logger.LogError(ex, "Action {ActionName} failed after {MaxRetries} attempts", actionName, maxRetries);
                    return false;
                }

                if (attempt < maxRetries)
                {
                    await Task.Delay(retryDelay.Value);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Vytvoří scoped service provider pro práci s DI
    /// </summary>
    protected IServiceScope CreateScope()
    {
        return ServiceProvider.CreateScope();
    }

    /// <summary>
    /// Aktualizuje heartbeat
    /// </summary>
    protected void UpdateHeartbeat()
    {
        _lastHeartbeat = DateTime.UtcNow;
        Logger.LogTrace("Heartbeat updated for service {ServiceName}", ServiceName);
    }

    /// <summary>
    /// Zkontroluje, jestli service běží správně
    /// </summary>
    protected bool IsHealthy()
    {
        var timeSinceHeartbeat = DateTime.UtcNow - _lastHeartbeat;
        return timeSinceHeartbeat <= Options.HeartbeatTimeout;
    }

    /// <summary>
    /// Zaznamená metriku (thread-safe)
    /// </summary>
    protected void RecordMetric(string name, object value)
    {
        lock (_metricsLock)
        {
            _metrics[name] = value;
        }
        Logger.LogTrace("Recorded metric {MetricName} = {Value} for service {ServiceName}",
            name, value, ServiceName);
    }

    /// <summary>
    /// Získá všechny metriky (thread-safe copy)
    /// </summary>
    protected Dictionary<string, object> GetMetrics()
    {
        lock (_metricsLock)
        {
            return new Dictionary<string, object>(_metrics);
        }
    }

    /// <summary>
    /// Bezpečně získá hodnotu metriky s typovou kontrolou
    /// </summary>
    protected T GetMetricValue<T>(string metricName, T defaultValue = default(T))
    {
        lock (_metricsLock)
        {
            if (_metrics.TryGetValue(metricName, out var value))
            {
                try
                {
                    // Přímé přetypování pokud je možné
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }

                    // Pokus o konverzi
                    if (value != null)
                    {
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to convert metric {MetricName} value {Value} to type {Type}, using default {Default}",
                        metricName, value, typeof(T).Name, defaultValue);
                }
            }

            return defaultValue ?? default(T);
        }
    }

    /// <summary>
    /// Increment čítačové metriky (thread-safe)
    /// </summary>
    protected void IncrementMetric(string metricName, long incrementBy = 1L)
    {
        var currentValue = GetMetricValue<long>(metricName, 0L);
        RecordMetric(metricName, currentValue + incrementBy);
    }

    /// <summary>
    /// Decrement čítačové metriky (thread-safe)
    /// </summary>
    protected void DecrementMetric(string metricName, long decrementBy = 1L)
    {
        var currentValue = GetMetricValue<long>(metricName, 0L);
        var newValue = Math.Max(0L, currentValue - decrementBy); // Prevent negative values
        RecordMetric(metricName, newValue);
    }

    /// <summary>
    /// Aktualizuje gauge metriku s max/min hodnotami
    /// </summary>
    protected void UpdateGaugeMetric(string metricName, double value, bool trackMinMax = true)
    {
        RecordMetric(metricName, value);

        if (trackMinMax)
        {
            // Track minimum
            var currentMin = GetMetricValue<double>($"{metricName}_min", double.MaxValue);
            if (value < currentMin)
            {
                RecordMetric($"{metricName}_min", value);
            }

            // Track maximum
            var currentMax = GetMetricValue<double>($"{metricName}_max", double.MinValue);
            if (value > currentMax)
            {
                RecordMetric($"{metricName}_max", value);
            }
        }
    }

    /// <summary>
    /// Zaznamená trvání operace
    /// </summary>
    protected void RecordDuration(string operationName, TimeSpan duration)
    {
        RecordMetric($"{operationName}_duration_ms", duration.TotalMilliseconds);
        RecordMetric($"{operationName}_last_execution", DateTime.UtcNow);

        // Track average duration
        var totalExecutions = GetMetricValue<long>($"{operationName}_executions", 0L) + 1L;
        var totalDurationMs = GetMetricValue<double>($"{operationName}_total_duration_ms", 0.0) + duration.TotalMilliseconds;

        RecordMetric($"{operationName}_executions", totalExecutions);
        RecordMetric($"{operationName}_total_duration_ms", totalDurationMs);
        RecordMetric($"{operationName}_avg_duration_ms", totalDurationMs / totalExecutions);
    }

    /// <summary>
    /// Čeká zadaný interval s možností cancellation
    /// </summary>
    protected async Task WaitForIntervalAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(interval, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Logger.LogDebug("Background service {ServiceName} wait was cancelled", ServiceName);
            throw;
        }
    }

    /// <summary>
    /// Periodic timer helper s automatickým měřením výkonu
    /// </summary>
    protected async Task ExecutePeriodicAsync(
        TimeSpan interval,
        Func<CancellationToken, Task> action,
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                await action(stoppingToken);

                stopwatch.Stop();
                RecordDuration("periodic_execution", stopwatch.Elapsed);
                UpdateHeartbeat();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in periodic execution for service {ServiceName}", ServiceName);
                IncrementMetric("periodic_execution_errors");

                if (!Options.ContinueOnError)
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Měří výkon operace a automaticky zaznamenává metriky
    /// </summary>
    protected async Task<T> MeasureOperationAsync<T>(
        string operationName,
        Func<Task<T>> operation)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = await operation();
            stopwatch.Stop();

            RecordDuration(operationName, stopwatch.Elapsed);
            IncrementMetric($"{operationName}_success_count");

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            RecordDuration($"{operationName}_failed", stopwatch.Elapsed);
            IncrementMetric($"{operationName}_error_count");

            Logger.LogWarning(ex, "Operation {OperationName} failed after {Duration}ms",
                operationName, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    /// <summary>
    /// Overload pro void operace
    /// </summary>
    protected async Task MeasureOperationAsync(
        string operationName,
        Func<Task> operation)
    {
        await MeasureOperationAsync<object>(operationName, async () =>
        {
            await operation();
            return null!;
        });
    }

    public override void Dispose()
    {
        _stoppingCts?.Cancel();
        _stoppingCts?.Dispose();
        base.Dispose();
    }
}

// Aktualizovaná verze QueuedBackgroundService s použitím nových helper metod
public class QueuedBackgroundService : BaseBackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;

    public QueuedBackgroundService(
        IBackgroundTaskQueue taskQueue,
        ILogger<QueuedBackgroundService> logger,
        IServiceProvider serviceProvider,
        IOptions<BackgroundServiceOptions> options)
        : base(logger, serviceProvider, options, "QueuedBackgroundService")
    {
        _taskQueue = taskQueue;
    }

    protected override async Task ExecuteServiceAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Queued background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                if (workItem == null)
                {
                    await WaitForIntervalAsync(TimeSpan.FromMilliseconds(100), stoppingToken);
                    continue;
                }

                await ProcessWorkItemAsync(workItem, stoppingToken);
                UpdateHeartbeat();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing background work items");
                IncrementMetric("processing_errors");

                if (!Options.ContinueOnError)
                {
                    throw;
                }
            }
        }
    }

    private async Task ProcessWorkItemAsync(BackgroundWorkItem workItem, CancellationToken stoppingToken)
    {
        workItem.Status = BackgroundWorkItemStatus.Running;
        workItem.StartedAt = DateTime.UtcNow;

        Logger.LogInformation("Processing background work item {Id} ({Name})", workItem.Id, workItem.Name);

        try
        {
            // Měříme výkon zpracování work item
            await MeasureOperationAsync($"workitem_{workItem.Type}", async () =>
            {
                using var scope = CreateScope();
                await workItem.WorkItem(scope.ServiceProvider, stoppingToken);
            });

            workItem.Status = BackgroundWorkItemStatus.Completed;
            workItem.CompletedAt = DateTime.UtcNow;

            Logger.LogInformation("Completed background work item {Id} ({Name}) in {Duration}ms",
                workItem.Id, workItem.Name,
                (workItem.CompletedAt - workItem.StartedAt)?.TotalMilliseconds);

            // Použití nové helper metody
            IncrementMetric("processed_items");
            IncrementMetric($"processed_items_{workItem.Type}");
        }
        catch (Exception ex)
        {
            workItem.Exceptions.Add(ex);
            workItem.ErrorMessage = ex.Message;
            workItem.RetryCount++;

            Logger.LogWarning(ex, "Background work item {Id} ({Name}) failed on attempt {Attempt}",
                workItem.Id, workItem.Name, workItem.RetryCount);

            if (workItem.RetryCount < workItem.MaxRetries)
            {
                workItem.Status = BackgroundWorkItemStatus.Retrying;
                workItem.ScheduledFor = DateTime.UtcNow.Add(workItem.RetryDelay);

                Logger.LogInformation("Retrying background work item {Id} in {Delay}",
                    workItem.Id, workItem.RetryDelay);

                // Přidáme zpět do fronty pro retry
                await _taskQueue.QueueBackgroundWorkItemAsync(workItem);
                IncrementMetric("retry_items");
            }
            else
            {
                workItem.Status = BackgroundWorkItemStatus.Failed;
                workItem.CompletedAt = DateTime.UtcNow;

                Logger.LogError(ex, "Background work item {Id} ({Name}) failed after {MaxRetries} attempts",
                    workItem.Id, workItem.Name, workItem.MaxRetries);

                // Použití nové helper metody
                IncrementMetric("failed_items");
                IncrementMetric($"failed_items_{workItem.Type}");
            }
        }
    }
}