using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PVAD.WebApi.Observability;

public static class PvadTelemetry
{
    public const string ActivitySourceName = "PVAD.WebApi";
    public const string MeterName = "PVAD.WebApi";

    public static readonly ActivitySource Activities = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> JobsEnqueued =
        Meter.CreateCounter<long>("pvad.jobs.enqueued", "{job}");
    private static readonly Counter<long> JobsProcessed =
        Meter.CreateCounter<long>("pvad.jobs.processed", "{job}");
    private static readonly Counter<long> JobFailures =
        Meter.CreateCounter<long>("pvad.jobs.failures", "{failure}");
    private static readonly Histogram<double> JobDuration =
        Meter.CreateHistogram<double>("pvad.jobs.duration", "s");
    private static readonly Histogram<double> JobQueueDuration =
        Meter.CreateHistogram<double>("pvad.jobs.queue.duration", "s");
    private static readonly Gauge<long> DependencyHealth =
        Meter.CreateGauge<long>("pvad.dependencies.health", "{state}");
    private static readonly Histogram<double> DependencyHealthDuration =
        Meter.CreateHistogram<double>("pvad.dependencies.healthcheck.duration", "s");
    private static readonly Gauge<double> ServiceHeartbeat =
        Meter.CreateGauge<double>("pvad.service.heartbeat", "s");
    private static readonly Counter<long> MailDeliveries =
        Meter.CreateCounter<long>("pvad.mail.deliveries", "{message}");
    private static readonly Histogram<double> MailDeliveryDuration =
        Meter.CreateHistogram<double>("pvad.mail.delivery.duration", "s");
    private static readonly Histogram<double> MailQueueDuration =
        Meter.CreateHistogram<double>("pvad.mail.queue.duration", "s");
    private static readonly Gauge<long> MailQueueDepth =
        Meter.CreateGauge<long>("pvad.mail.queue.depth", "{message}");
    private static readonly Gauge<double> MailQueueOldestAge =
        Meter.CreateGauge<double>("pvad.mail.queue.oldest.age", "s");
    private static readonly Counter<long> OcrOperations =
        Meter.CreateCounter<long>("pvad.ocr.operations", "{operation}");
    private static readonly Histogram<double> OcrDuration =
        Meter.CreateHistogram<double>("pvad.ocr.duration", "s");
    private static readonly Counter<long> OcrTimeouts =
        Meter.CreateCounter<long>("pvad.ocr.timeouts", "{timeout}");
    private static readonly Counter<long> OcrWorkerTerminations =
        Meter.CreateCounter<long>("pvad.ocr.worker.terminations", "{termination}");
    private static readonly Counter<long> DocumentOperations =
        Meter.CreateCounter<long>("pvad.document.operations", "{operation}");
    private static readonly Histogram<double> DocumentOperationDuration =
        Meter.CreateHistogram<double>("pvad.document.operation.duration", "s");
    private static readonly Counter<long> ApiFailures =
        Meter.CreateCounter<long>("pvad.api.failures", "{failure}");
    private static readonly Gauge<long> JobQueueDepth =
        Meter.CreateGauge<long>("pvad.jobs.queue.depth", "{job}");
    private static readonly Gauge<double> OldestJobAge =
        Meter.CreateGauge<double>("pvad.jobs.queue.oldest.age", "s");
    private static readonly Counter<long> JobLeaseRecoveries =
        Meter.CreateCounter<long>("pvad.jobs.lease.recoveries", "{job}");
    private static readonly Counter<long> JobRetention =
        Meter.CreateCounter<long>("pvad.jobs.retention", "{job}");
    private static readonly Counter<long> StorageOperations =
        Meter.CreateCounter<long>("pvad.storage.operations", "{operation}");
    private static readonly Histogram<double> StorageOperationDuration =
        Meter.CreateHistogram<double>("pvad.storage.operation.duration", "s");
    private static readonly Counter<long> StorageBytes =
        Meter.CreateCounter<long>("pvad.storage.bytes", "By");
    private static readonly Counter<long> LicensingOperations =
        Meter.CreateCounter<long>("pvad.licensing.operations", "{operation}");
    private static readonly Histogram<double> LicensingOperationDuration =
        Meter.CreateHistogram<double>("pvad.licensing.operation.duration", "s");
    private static readonly Gauge<long> LicenseInventory =
        Meter.CreateGauge<long>("pvad.licensing.licenses", "{license}");
    private static readonly Counter<long> BrowserEvents =
        Meter.CreateCounter<long>("pvad.browser.events", "{event}");
    private static readonly Histogram<double> BrowserWebVital =
        Meter.CreateHistogram<double>("pvad.browser.web_vital", "{value}");
    private static readonly Counter<long> CodeOperations =
        Meter.CreateCounter<long>("pvad.designer.code.operations", "{operation}");
    private static readonly Histogram<double> CodeOperationDuration =
        Meter.CreateHistogram<double>("pvad.designer.code.operation.duration", "s");

    public static void RecordJobEnqueued(string jobType) =>
        JobsEnqueued.Add(1, new KeyValuePair<string, object?>("job.type", jobType));

    public static void RecordCodeOperation(
        string operation,
        string language,
        string outcome,
        string fidelity,
        TimeSpan duration,
        IReadOnlyCollection<string>? diagnosticCodes = null)
    {
        var tags = new TagList
        {
            { "code.operation", operation },
            { "code.language", language },
            { "code.outcome", outcome },
            { "code.fidelity", fidelity },
            { "code.diagnostics", diagnosticCodes is { Count: > 0 }
                ? string.Join(',', diagnosticCodes.Distinct(StringComparer.Ordinal).Order().Take(8))
                : "none" },
        };
        CodeOperations.Add(1, tags);
        CodeOperationDuration.Record(Math.Max(0, duration.TotalSeconds), tags);
    }

    public static Activity? StartJobEnqueue(string jobType)
    {
        var activity = Activities.StartActivity("pvad.job.enqueue", ActivityKind.Producer);
        activity?.SetTag("job.type", jobType);
        return activity;
    }

    public static Activity? StartStorageOperation(string operation)
    {
        var activity = Activities.StartActivity("pvad.storage", ActivityKind.Client);
        activity?.SetTag("storage.operation", operation);
        return activity;
    }

    public static void CompleteStorageOperation(
        Activity? activity,
        string outcome,
        long bytes = 0)
    {
        activity?.SetTag("storage.outcome", outcome);
        if (bytes > 0)
            activity?.SetTag("storage.bytes", bytes);
        if (outcome is "failed" or "rejected")
            activity?.SetStatus(ActivityStatusCode.Error, outcome);
    }

    public static Activity? StartMailEnqueue()
    {
        var activity = Activities.StartActivity("pvad.mail.enqueue", ActivityKind.Producer);
        activity?.SetTag("mail.operation", "enqueue");
        return activity;
    }

    public static Activity? StartMailDelivery(
        int attempt,
        string? traceParent,
        string? traceState)
    {
        Activity? activity;
        if (ActivityContext.TryParse(traceParent, traceState, isRemote: true, out var parent))
            activity = Activities.StartActivity("pvad.mail.deliver", ActivityKind.Consumer, parent);
        else
            activity = Activities.StartActivity("pvad.mail.deliver", ActivityKind.Consumer);

        activity?.SetTag("mail.operation", "deliver");
        activity?.SetTag("mail.attempt", attempt);
        return activity;
    }

    public static void CompleteMailOperation(Activity? activity, string outcome)
    {
        activity?.SetTag("mail.outcome", outcome);
        if (outcome is "retry" or "dead_letter")
            activity?.SetStatus(ActivityStatusCode.Error, outcome);
    }

    public static TracePropagationContext CaptureTraceContext()
    {
        var activity = Activity.Current;
        return activity?.IdFormat == ActivityIdFormat.W3C
            ? new TracePropagationContext(activity.Id, activity.TraceStateString)
            : default;
    }

    public static Activity? StartJobProcessing(
        string jobType,
        int attempt,
        string? traceParent = null,
        string? traceState = null)
    {
        Activity? activity;
        if (ActivityContext.TryParse(traceParent, traceState, isRemote: true, out var parent))
            activity = Activities.StartActivity("pvad.job.process", ActivityKind.Consumer, parent);
        else
            activity = Activities.StartActivity("pvad.job.process", ActivityKind.Consumer);

        activity?.SetTag("job.type", jobType);
        activity?.SetTag("job.attempt", attempt);
        return activity;
    }

    public static void RecordJobQueueDuration(string jobType, TimeSpan duration) =>
        JobQueueDuration.Record(
            Math.Max(0, duration.TotalSeconds),
            new KeyValuePair<string, object?>("job.type", jobType));

    public static void RecordJobProcessed(
        string jobType,
        string outcome,
        TimeSpan duration)
    {
        var tags = new TagList
        {
            { "job.type", jobType },
            { "job.outcome", outcome },
        };
        JobsProcessed.Add(1, tags);
        JobDuration.Record(Math.Max(0, duration.TotalSeconds), tags);
        if (outcome is "retry" or "dead_letter")
            JobFailures.Add(1, tags);
    }

    public static void RecordDependencyHealth(
        string dependency,
        bool healthy,
        TimeSpan duration)
    {
        var tag = new KeyValuePair<string, object?>("dependency.name", dependency);
        DependencyHealth.Record(healthy ? 1 : 0, tag);
        DependencyHealthDuration.Record(Math.Max(0, duration.TotalSeconds), tag);
    }

    public static void RecordServiceHeartbeat(DateTimeOffset timestamp) =>
        ServiceHeartbeat.Record(timestamp.ToUnixTimeMilliseconds() / 1000d);

    public static void RecordMailQueue(long depth, TimeSpan oldestAge)
    {
        MailQueueDepth.Record(Math.Max(0, depth));
        MailQueueOldestAge.Record(Math.Max(0, oldestAge.TotalSeconds));
    }

    public static void RecordMailDelivery(
        string outcome,
        TimeSpan duration,
        TimeSpan queueDuration)
    {
        var outcomeTag = new KeyValuePair<string, object?>("mail.outcome", outcome);
        MailDeliveries.Add(1, outcomeTag);
        MailDeliveryDuration.Record(Math.Max(0, duration.TotalSeconds), outcomeTag);
        MailQueueDuration.Record(Math.Max(0, queueDuration.TotalSeconds), outcomeTag);
    }

    public static void RecordOcrOperation(
        string language,
        string outcome,
        TimeSpan duration,
        bool workerTerminated = false)
    {
        var tags = new TagList
        {
            { "ocr.language", NormalizeOcrLanguage(language) },
            { "ocr.outcome", outcome },
        };
        OcrOperations.Add(1, tags);
        OcrDuration.Record(Math.Max(0, duration.TotalSeconds), tags);
        if (outcome == "timeout")
            OcrTimeouts.Add(1, tags);
        if (workerTerminated)
            OcrWorkerTerminations.Add(1, tags);
    }

    public static void RecordDocumentOperation(
        string operation,
        string outcome,
        TimeSpan duration)
    {
        operation = NormalizeDocumentOperation(operation);
        var tags = new TagList
        {
            { "operation.type", operation },
            { "operation.outcome", outcome },
        };
        DocumentOperations.Add(1, tags);
        DocumentOperationDuration.Record(Math.Max(0, duration.TotalSeconds), tags);
    }

    public static Activity? StartDocumentOperation(string operation)
    {
        var activity = Activities.StartActivity("pvad.document.operation", ActivityKind.Internal);
        activity?.SetTag("operation.type", NormalizeDocumentOperation(operation));
        return activity;
    }

    public static void CompleteDocumentOperation(Activity? activity, string outcome)
    {
        activity?.SetTag("operation.outcome", outcome);
        if (outcome is "failed" or "rejected" or "retry" or "dead_letter")
            activity?.SetStatus(ActivityStatusCode.Error, outcome);
    }

    public static void RecordApiFailure(string failureType, string surface)
    {
        var tags = new TagList
        {
            { "failure.type", failureType },
            { "api.surface", surface },
        };
        ApiFailures.Add(1, tags);
    }

    public static void RecordJobQueue(string jobType, long depth, TimeSpan oldestAge)
    {
        var tag = new KeyValuePair<string, object?>("job.type", jobType);
        JobQueueDepth.Record(Math.Max(0, depth), tag);
        OldestJobAge.Record(Math.Max(0, oldestAge.TotalSeconds), tag);
    }

    public static void RecordJobLeaseRecoveries(long count)
    {
        if (count > 0)
            JobLeaseRecoveries.Add(count);
    }

    public static void RecordJobRetention(string outcome, long count)
    {
        if (count > 0)
            JobRetention.Add(count, new KeyValuePair<string, object?>("retention.outcome", outcome));
    }

    public static void RecordStorageOperation(
        string operation,
        string outcome,
        TimeSpan duration,
        long bytes = 0)
    {
        var tags = new TagList
        {
            { "storage.operation", operation },
            { "storage.outcome", outcome },
        };
        StorageOperations.Add(1, tags);
        StorageOperationDuration.Record(Math.Max(0, duration.TotalSeconds), tags);
        if (bytes > 0)
            StorageBytes.Add(bytes, tags);
    }

    public static void RecordLicensingOperation(
        string operation,
        string outcome,
        TimeSpan duration)
    {
        var tags = new TagList
        {
            { "licensing.operation", operation },
            { "licensing.outcome", outcome },
        };
        LicensingOperations.Add(1, tags);
        LicensingOperationDuration.Record(Math.Max(0, duration.TotalSeconds), tags);
    }

    public static void RecordLicenseInventory(string state, long count) =>
        LicenseInventory.Record(
            Math.Max(0, count),
            new KeyValuePair<string, object?>("license.state", state));

    public static void RecordBrowserEvent(
        string application,
        string eventType,
        string outcome,
        string route,
        string? vitalName = null,
        double? value = null,
        string version = "unknown")
    {
        var tags = new TagList
        {
            { "browser.application", application },
            { "browser.event", eventType },
            { "browser.outcome", outcome },
            { "browser.route", route },
            { "browser.version", version },
        };
        if (eventType is "status_page" or "status_loop" && vitalName is not null)
        {
            tags.Add("browser.status", vitalName);
            tags.Add("browser.category", BrowserStatusCategory(vitalName));
        }
        else if (eventType == "status_recovery" && vitalName is not null)
            tags.Add("browser.recovery", vitalName);

        BrowserEvents.Add(1, tags);
        if (eventType == "web_vital" && vitalName is not null && value is not null)
        {
            tags.Add("browser.vital", vitalName);
            BrowserWebVital.Record(Math.Max(0, value.Value), tags);
        }
    }

    private static string BrowserStatusCategory(string status) => status switch
    {
        "unauthenticated" or "sessionExpired" => "authentication",
        "forbidden" or "entitlementDenied" or "suspended" or "trialExpired" => "access",
        "badRequest" or "expiredLink" or "fileTooLarge" => "request",
        "conflict" => "conflict",
        "rateLimited" => "rate_limit",
        "unexpected" => "server",
        "upstreamUnavailable" or "unavailable" or "maintenance" or "timeout" or "requestTimeout" => "availability",
        "offline" => "connectivity",
        "incompatibleVersion" => "compatibility",
        "unsupportedBrowser" => "browser",
        "notFound" => "navigation",
        _ => "unknown",
    };

    private static string NormalizeOcrLanguage(string language)
    {
        var supported = new HashSet<string>(StringComparer.Ordinal)
        {
            "ara", "chi_sim", "chi_tra", "deu", "eng", "fra", "heb", "ita",
            "jpn", "kor", "nld", "pol", "por", "rus", "spa", "tur", "ukr",
        };
        var values = (language ?? string.Empty)
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return values.Length is >= 1 and <= 3 && values.All(supported.Contains)
            ? string.Join('+', values)
            : "other";
    }

    private static string NormalizeDocumentOperation(string operation) =>
        operation is "import" or "export" or "migration" or "rendering"
            ? operation
            : "other";
}

public readonly record struct TracePropagationContext(
    string? TraceParent,
    string? TraceState);
