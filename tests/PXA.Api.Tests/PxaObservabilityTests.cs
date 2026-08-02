using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.FileImporter.ImageOcr;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Controllers;
using PXA.WebApi.Observability;
using PXA.WebApi.Services.Mail;

namespace PXA.Api.Tests;

public sealed class PxaObservabilityTests
{
    [Fact]
    public void Trace_processor_removes_queries_and_sensitive_identity_attributes()
    {
        using var activity = new Activity("observability-test");
        activity.SetTag("url.full", "https://example.test/render?token=secret&name=invoice.pdf");
        activity.SetTag("http.url", "https://example.test/import?api_key=secret");
        activity.SetTag("url.query", "token=secret");
        activity.SetTag("http.request.header.authorization", "Bearer secret");
        activity.SetTag("http.request.header.cookie", "PXA.Session=secret");
        activity.SetTag("user.email", "person@example.test");
        activity.SetTag("custom.api-key", "secret");
        activity.SetTag("document.id", "customer-document");
        activity.SetTag("db.statement", "select * from customers");
        activity.SetTag("exception.stacktrace", "private stack");
        activity.SetTag("safe.outcome", "completed");

        new PxaTelemetrySanitizingProcessor().OnEnd(activity);

        Assert.Equal("https://example.test/render", activity.GetTagItem("url.full"));
        Assert.Equal("https://example.test/import", activity.GetTagItem("http.url"));
        Assert.Null(activity.GetTagItem("url.query"));
        Assert.Null(activity.GetTagItem("http.request.header.authorization"));
        Assert.Null(activity.GetTagItem("http.request.header.cookie"));
        Assert.Null(activity.GetTagItem("user.email"));
        Assert.Null(activity.GetTagItem("custom.api-key"));
        Assert.Null(activity.GetTagItem("document.id"));
        Assert.Null(activity.GetTagItem("db.statement"));
        Assert.Null(activity.GetTagItem("exception.stacktrace"));
        Assert.Equal("completed", activity.GetTagItem("safe.outcome"));
    }

    [Fact]
    public void Job_and_dependency_metrics_use_only_bounded_operational_tags()
    {
        var measurements = new ConcurrentQueue<Measurement>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == PxaTelemetry.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Enqueue(new Measurement(instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Enqueue(new Measurement(instrument.Name, value, tags.ToArray())));
        listener.Start();

        PxaTelemetry.RecordJobEnqueued("document.import");
        PxaTelemetry.RecordJobQueueDuration("document.import", TimeSpan.FromSeconds(3));
        PxaTelemetry.RecordJobProcessed("document.import", "completed", TimeSpan.FromSeconds(2));
        PxaTelemetry.RecordDependencyHealth("pxa-database", true, TimeSpan.FromMilliseconds(25));
        PxaTelemetry.RecordServiceHeartbeat(
            DateTimeOffset.FromUnixTimeSeconds(1_750_000_000));
        PxaTelemetry.RecordMailQueue(4, TimeSpan.FromMinutes(3));
        PxaTelemetry.RecordMailDelivery(
            "delivered",
            TimeSpan.FromMilliseconds(80),
            TimeSpan.FromSeconds(5));
        PxaTelemetry.RecordOcrOperation(
            "deu+eng",
            "timeout",
            TimeSpan.FromSeconds(45),
            workerTerminated: true);
        PxaTelemetry.RecordDocumentOperation(
            "migration",
            "completed",
            TimeSpan.FromSeconds(2));
        PxaTelemetry.RecordApiFailure("authorization", "admin");
        PxaTelemetry.RecordJobQueue("document.import", 3, TimeSpan.FromMinutes(2));
        PxaTelemetry.RecordJobLeaseRecoveries(2);
        PxaTelemetry.RecordJobRetention("expired", 4);
        PxaTelemetry.RecordStorageOperation(
            "put",
            "completed",
            TimeSpan.FromMilliseconds(20),
            1024);
        PxaTelemetry.RecordLicensingOperation(
            "offline_validation",
            "valid",
            TimeSpan.FromMilliseconds(2));
        PxaTelemetry.RecordLicenseInventory("expiring_14d", 1);
        PxaTelemetry.RecordBrowserEvent(
            "designer",
            "web_vital",
            "good",
            "templates",
            "lcp",
            1200);

        var recordedMeasurements = measurements.ToArray();
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.jobs.enqueued");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.jobs.queue.duration");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.jobs.processed");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.jobs.duration");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.dependencies.health");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.dependencies.healthcheck.duration");
        Assert.Contains(
            recordedMeasurements,
            value => value.Name == "pxa.service.heartbeat" &&
                     value.Value == 1_750_000_000);
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.mail.queue.depth");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.mail.queue.oldest.age");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.mail.deliveries");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.mail.delivery.duration");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.ocr.operations");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.ocr.duration");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.ocr.timeouts");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.ocr.worker.terminations");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.document.operations");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.document.operation.duration");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.api.failures");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.jobs.queue.depth");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.jobs.queue.oldest.age");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.jobs.lease.recoveries");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.jobs.retention");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.storage.operations");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.storage.operation.duration");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.storage.bytes");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.licensing.operations");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.licensing.operation.duration");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.licensing.licenses");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.browser.events");
        Assert.Contains(recordedMeasurements, value => value.Name == "pxa.browser.web_vital");

        var allowedTags = new[]
        {
            "job.type", "job.outcome", "dependency.name", "mail.outcome",
            "ocr.language", "ocr.outcome", "operation.type", "operation.outcome",
            "failure.type", "api.surface", "retention.outcome",
            "storage.operation", "storage.outcome", "licensing.operation",
            "licensing.outcome", "license.state",
            "browser.application", "browser.event", "browser.outcome",
            "browser.route", "browser.vital",
        };
        Assert.All(
            recordedMeasurements.SelectMany(value => value.Tags),
            tag => Assert.Contains(tag.Key, allowedTags));
        Assert.DoesNotContain(
            recordedMeasurements.SelectMany(value => value.Tags),
            tag => tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                   tag.Key.Contains("tenant", StringComparison.OrdinalIgnoreCase) ||
                   tag.Key.Contains("file", StringComparison.OrdinalIgnoreCase) ||
                   tag.Key.Contains("document", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Browser_telemetry_accepts_only_bounded_privacy_safe_values()
    {
        var valid = new BrowserTelemetryBatch(
            "account",
            [
                new BrowserTelemetryEvent("navigation", "completed", "profile"),
                new BrowserTelemetryEvent("web_vital", "good", "profile", "lcp", 1200),
            ]);

        Assert.True(BrowserTelemetryController.TryValidate(valid, out _));
        Assert.Equal("profile", BrowserTelemetryController.NormalizeRoute("account", "profile"));
        Assert.Equal("other", BrowserTelemetryController.NormalizeRoute("account", "customer-123"));
    }

    [Fact]
    public async Task Browser_telemetry_endpoint_accepts_anonymous_bounded_batches()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "traceparent",
            "00-11111111111111111111111111111111-2222222222222222-01");

        using var response = await client.PostAsJsonAsync(
            "/api/pxa/v1/telemetry/browser",
            new BrowserTelemetryBatch(
                "documentation",
                [new BrowserTelemetryEvent("navigation", "completed", "editor")]));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Theory]
    [InlineData("error", "failed", "stack", 1.0)]
    [InlineData("navigation", "poor", null, null)]
    [InlineData("custom-event", "completed", null, null)]
    public void Browser_telemetry_rejects_free_form_or_mismatched_event_data(
        string eventType,
        string outcome,
        string? name,
        double? value)
    {
        var request = new BrowserTelemetryBatch(
            "company",
            [new BrowserTelemetryEvent(eventType, outcome, "home", name, value)]);

        Assert.False(BrowserTelemetryController.TryValidate(request, out _));
    }

    [Theory]
    [InlineData("/api/document/import-docx", "import")]
    [InlineData("/api/export", "export")]
    [InlineData("/api/migration/convert", "migration")]
    [InlineData("/api/templates/render", "rendering")]
    [InlineData("/api/pxa/v1/document-jobs/code-migration", "migration")]
    public void Document_operation_routes_use_bounded_categories(string path, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;

        Assert.Equal(
            expected,
            PxaOperationMetricsMiddleware.ClassifyDocumentOperation(context.Request));
    }

    [Fact]
    public void Non_mutating_routes_are_not_document_operations()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/migration/frameworks";

        Assert.Null(PxaOperationMetricsMiddleware.ClassifyDocumentOperation(context.Request));
    }

    [Fact]
    public async Task Document_operation_middleware_continues_the_request_trace_with_bounded_tags()
    {
        Activity? operation = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PxaTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity =>
            {
                if (activity.OperationName == "pxa.document.operation")
                    operation = activity;
            },
        };
        ActivitySource.AddActivityListener(listener);
        using var request = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/migration/convert";
        context.Response.StatusCode = StatusCodes.Status202Accepted;
        var middleware = new PxaOperationMetricsMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.NotNull(operation);
        Assert.Equal(request.TraceId, operation.TraceId);
        Assert.Equal(request.SpanId, operation.ParentSpanId);
        Assert.Equal(ActivityKind.Internal, operation.Kind);
        Assert.Equal("migration", operation.GetTagItem("operation.type"));
        Assert.Equal("completed", operation.GetTagItem("operation.outcome"));
        Assert.DoesNotContain(
            operation.TagObjects,
            tag => PxaTelemetrySanitizingProcessor.IsForbiddenAttribute(tag.Key));
    }

    [Fact]
    public void Document_operation_activity_normalizes_unknown_categories()
    {
        using var listener = ListenToPxaActivities();
        using var activity = PxaTelemetry.StartDocumentOperation("customer-defined-operation");

        Assert.NotNull(activity);
        Assert.Equal("other", activity.GetTagItem("operation.type"));
    }

    [Fact]
    public void Job_activity_contains_no_customer_or_document_identifiers()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PxaTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = PxaTelemetry.StartJobProcessing("migration.code", 2);

        Assert.NotNull(activity);
        Assert.Equal("migration.code", activity.GetTagItem("job.type"));
        Assert.Equal(2, activity.GetTagItem("job.attempt"));
        Assert.DoesNotContain(
            activity.TagObjects,
            tag => tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                   tag.Key.Contains("tenant", StringComparison.OrdinalIgnoreCase) ||
                   tag.Key.Contains("file", StringComparison.OrdinalIgnoreCase) ||
                   tag.Key.Contains("document", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Storage_activity_continues_the_current_trace_with_bounded_tags()
    {
        using var listener = ListenToPxaActivities();
        using var parent = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var activity = PxaTelemetry.StartStorageOperation("put");
        PxaTelemetry.CompleteStorageOperation(activity, "completed", 1024);

        Assert.NotNull(activity);
        Assert.Equal(parent.TraceId, activity.TraceId);
        Assert.Equal(parent.SpanId, activity.ParentSpanId);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal("put", activity.GetTagItem("storage.operation"));
        Assert.Equal("completed", activity.GetTagItem("storage.outcome"));
        Assert.Equal(1024L, activity.GetTagItem("storage.bytes"));
        Assert.All(activity.TagObjects, tag =>
            Assert.False(PxaTelemetrySanitizingProcessor.IsForbiddenAttribute(tag.Key)));
    }

    [Fact]
    public void Mail_delivery_continues_the_persisted_producer_trace_without_identity_data()
    {
        using var listener = ListenToPxaActivities();
        using var request = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        string? traceParent;
        using (var producer = PxaTelemetry.StartMailEnqueue())
        {
            traceParent = PxaTelemetry.CaptureTraceContext().TraceParent;
            PxaTelemetry.CompleteMailOperation(producer, "queued");
            Assert.Equal(ActivityKind.Producer, producer!.Kind);
        }

        using var consumer = PxaTelemetry.StartMailDelivery(1, traceParent, null);
        PxaTelemetry.CompleteMailOperation(consumer, "delivered");

        Assert.NotNull(consumer);
        Assert.Equal(request.TraceId, consumer.TraceId);
        Assert.Equal(ActivityKind.Consumer, consumer.Kind);
        Assert.Equal(ActivityIdFormat.W3C, consumer.IdFormat);
        Assert.Equal("deliver", consumer.GetTagItem("mail.operation"));
        Assert.Equal(1, consumer.GetTagItem("mail.attempt"));
        Assert.DoesNotContain(
            consumer.TagObjects,
            tag => PxaTelemetrySanitizingProcessor.IsForbiddenAttribute(tag.Key));
    }

    [Fact]
    public void Mail_queue_persists_only_w3c_trace_context_from_the_producer()
    {
        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseNpgsql("Host=localhost;Database=not-used;Username=not-used;Password=not-used")
            .Options;
        using var dbContext = new PxaDbContext(options);
        var queue = new PxaMailQueue(dbContext, new EphemeralDataProtectionProvider());
        using var listener = ListenToPxaActivities();
        using var request = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();

        var message = queue.Enqueue(
            null,
            null,
            "not-exported@example.test",
            "identity.password-changed",
            new { displayName = "Not exported" },
            "not-exported");

        Assert.True(ActivityContext.TryParse(
            message.TraceParent,
            message.TraceState,
            isRemote: true,
            out var context));
        Assert.Equal(request.TraceId, context.TraceId);
        Assert.Null(message.TraceState);
    }

    [Theory]
    [InlineData("user.email")]
    [InlineData("request.body")]
    [InlineData("X-Api-Key")]
    [InlineData("template_json")]
    [InlineData("customer-id")]
    [InlineData("fileName")]
    [InlineData("ContentRoot")]
    [InlineData("KeyId")]
    public void Privacy_policy_recognizes_forbidden_attribute_variants(string attribute)
    {
        Assert.True(PxaTelemetrySanitizingProcessor.IsForbiddenAttribute(attribute));
    }

    [Fact]
    public void Structured_log_privacy_removes_sensitive_and_unbounded_values()
    {
        var attributes = new List<KeyValuePair<string, object?>>
        {
            new("{OriginalFormat}", "Operation {Outcome} for {Password}"),
            new("Outcome", "failed"),
            new("Password", "must-not-appear"),
            new("UserEmail", "person@example.test"),
            new("Attempt", 2),
            new("Payload", new { secret = "must-not-appear" }),
        };

        var sanitized = PxaLogPrivacy.SanitizeAttributes(
            attributes,
            new InvalidOperationException("must-not-appear"));

        Assert.Equal(PxaLogPrivacy.SuppressedMessage, PxaLogPrivacy.ResolveMessageTemplate(attributes));
        Assert.Contains(sanitized, value => value.Key == "Outcome" && Equals(value.Value, "failed"));
        Assert.Contains(sanitized, value => value.Key == "Attempt" && Equals(value.Value, 2));
        Assert.Contains(
            sanitized,
            value => value.Key == "exception.type" &&
                     Equals(value.Value, typeof(InvalidOperationException).FullName));
        Assert.DoesNotContain(sanitized, value => value.Key is "Password" or "UserEmail" or "Payload");
        Assert.DoesNotContain(
            sanitized.Select(value => value.Value?.ToString()),
            value => value?.Contains("must-not-appear", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Json_console_log_contains_resource_and_trace_fields_without_secret_values()
    {
        var formatter = new PxaJsonConsoleFormatter(
            Options.Create(new PxaObservabilityOptions
            {
                ServiceName = "pxa-test",
                ServiceNamespace = "PowerDoxAutomation",
            }),
            new TestHostEnvironment { EnvironmentName = Environments.Production });
        var attributes = new List<KeyValuePair<string, object?>>
        {
            new("{OriginalFormat}", "Operation completed with {Outcome}"),
            new("Outcome", "completed"),
            new("ApiKey", "must-not-appear"),
        };
        var entry = new LogEntry<IReadOnlyList<KeyValuePair<string, object?>>>(
            LogLevel.Information,
            "PXA.Test",
            new EventId(42, "TestCompleted"),
            attributes,
            new InvalidOperationException("must-not-appear"),
            static (_, _) => "formatted must-not-appear");
        using var activity = new Activity("json-log-test")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        using var output = new StringWriter();

        formatter.Write(in entry, null, output);

        using var document = JsonDocument.Parse(output.ToString());
        var root = document.RootElement;
        Assert.Equal("pxa-test", root.GetProperty("service.name").GetString());
        Assert.Equal("PowerDoxAutomation", root.GetProperty("service.namespace").GetString());
        Assert.Equal("Production", root.GetProperty("deployment.environment.name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("service.version").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("service.instance.id").GetString()));
        Assert.Equal(activity.TraceId.ToHexString(), root.GetProperty("traceId").GetString());
        Assert.Equal(activity.SpanId.ToHexString(), root.GetProperty("spanId").GetString());
        Assert.Equal("Operation completed with {Outcome}", root.GetProperty("messageTemplate").GetString());
        Assert.Equal("completed", root.GetProperty("attributes").GetProperty("Outcome").GetString());
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            root.GetProperty("attributes").GetProperty("exception.type").GetString());
        Assert.DoesNotContain("must-not-appear", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("ApiKey", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Production", "Debug", false, null, "requires Observability:AllowTemporaryDebugLogging")]
    [InlineData("Production", "Debug", true, null, "requires Observability:DebugLoggingExpiresAtUtc")]
    public void Production_debug_logging_requires_an_explicit_bounded_expiry(
        string environment,
        string level,
        bool allowTemporaryDebug,
        string? expiresAt,
        string expectedError)
    {
        var now = DateTimeOffset.Parse("2026-07-26T12:00:00Z");
        var configuration = BuildLoggingConfiguration(level);
        var settings = new PxaObservabilityOptions
        {
            AllowTemporaryDebugLogging = allowTemporaryDebug,
            DebugLoggingExpiresAtUtc = expiresAt is null ? null : DateTimeOffset.Parse(expiresAt),
        };

        var error = PxaObservabilityExtensions.GetDebugLoggingConfigurationError(
            configuration,
            environment,
            settings,
            now);

        Assert.Contains(expectedError, error, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_debug_logging_accepts_only_a_future_window_of_at_most_24_hours()
    {
        var now = DateTimeOffset.Parse("2026-07-26T12:00:00Z");
        var configuration = BuildLoggingConfiguration("Debug");

        Assert.Contains(
            "expired",
            PxaObservabilityExtensions.GetDebugLoggingConfigurationError(
                configuration,
                Environments.Production,
                new PxaObservabilityOptions
                {
                    AllowTemporaryDebugLogging = true,
                    DebugLoggingExpiresAtUtc = now,
                },
                now),
            StringComparison.Ordinal);
        Assert.Contains(
            "at most 24 hours",
            PxaObservabilityExtensions.GetDebugLoggingConfigurationError(
                configuration,
                Environments.Production,
                new PxaObservabilityOptions
                {
                    AllowTemporaryDebugLogging = true,
                    DebugLoggingExpiresAtUtc = now.AddHours(25),
                },
                now),
            StringComparison.Ordinal);
        Assert.Null(PxaObservabilityExtensions.GetDebugLoggingConfigurationError(
            configuration,
            Environments.Production,
            new PxaObservabilityOptions
            {
                AllowTemporaryDebugLogging = true,
                DebugLoggingExpiresAtUtc = now.AddHours(2),
            },
            now));
        Assert.Null(PxaObservabilityExtensions.GetDebugLoggingConfigurationError(
            configuration,
            Environments.Development,
            new PxaObservabilityOptions(),
            now));
    }

    [Fact]
    public void Production_rejects_ocr_failure_injection()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production,
            });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Observability:EnableOcrFailureInjection"] = "true",
            });
            builder.AddPxaObservability();
        });

        Assert.Contains(
            "only in Development or Testing",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ocr_failure_injection_is_bounded_and_then_delegates()
    {
        var inner = new StubOcrEngine();
        var engine = new PxaFaultInjectingOcrEngine(inner, failureCount: 2);
        var options = new ImageToPdfConversionOptions();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecognizeAsync([], options));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecognizeAsync([], options));
        var pages = await engine.RecognizeAsync([], options);

        Assert.Empty(pages);
        Assert.Equal(1, inner.CallCount);
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public void Audit_events_are_append_only(EntityState state)
    {
        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseInMemoryDatabase($"audit-append-only-{Guid.NewGuid()}")
            .Options;
        using var dbContext = new PxaDbContext(options);
        var auditEvent = new AuditEvent
        {
            Action = "test.action",
            TargetType = "test",
            TargetId = "synthetic",
            Outcome = "completed",
        };
        dbContext.Attach(auditEvent);
        dbContext.Entry(auditEvent).State = state;

        var exception = Assert.Throws<InvalidOperationException>(() => dbContext.SaveChanges());

        Assert.Contains("append-only", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Operational_log_templates_do_not_name_sensitive_fields()
    {
        var root = FindRepositoryRoot();
        var forbiddenPlaceholders = new[]
        {
            "{Email", "{Recipient", "{Password", "{Token", "{Secret", "{ApiKey",
            "{LicenseKey", "{FileName", "{FilePath", "{DocumentId", "{TemplateJson",
            "{RequestBody", "{ResponseBody", "{MailBody",
        };
        var sources = Directory.EnumerateFiles(
            Path.Combine(root, "PXA.WebApi"),
            "*.cs",
            SearchOption.AllDirectories);

        foreach (var source in sources)
        {
            var content = File.ReadAllText(source);
            var logCalls = Regex.Matches(
                content,
                @"\.Log(?:Trace|Debug|Information|Warning|Error|Critical)\s*\((?<call>[\s\S]*?)\);");
            foreach (Match logCall in logCalls)
            {
                Assert.DoesNotContain(
                    forbiddenPlaceholders,
                    placeholder => logCall.Value.Contains(
                        placeholder,
                        StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void Job_processing_continues_the_persisted_remote_trace()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var producerSpanId = ActivitySpanId.CreateRandom();
        var traceParent = $"00-{traceId}-{producerSpanId}-01";
        Activity? processing = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PxaTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity =>
            {
                if (activity.OperationName == "pxa.job.process")
                    processing = activity;
            },
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = PxaTelemetry.StartJobProcessing(
            "document.import",
            1,
            traceParent,
            "pxa=correlated");

        Assert.NotNull(processing);
        Assert.Equal(traceId, processing.TraceId);
        Assert.Equal(producerSpanId, processing.ParentSpanId);
        Assert.Equal(traceParent, processing.ParentId);
        Assert.Equal("pxa=correlated", processing.TraceStateString);
    }

    [Fact]
    public void Job_enqueue_captures_the_producer_span_without_baggage()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PxaTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var parent = new Activity("request")
            .SetIdFormat(ActivityIdFormat.W3C)
            .AddBaggage("customer", "must-not-propagate")
            .Start();
        using var producer = PxaTelemetry.StartJobEnqueue("document.export");

        var context = PxaTelemetry.CaptureTraceContext();

        Assert.Equal(producer!.Id, context.TraceParent);
        Assert.Null(context.TraceState);
        Assert.DoesNotContain("customer", context.TraceParent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://127.0.0.1:4317")]
    public async Task Liveness_does_not_depend_on_an_otlp_collector(string? endpoint)
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Observability:Enabled"] = "true",
                        ["Observability:OtlpEndpoint"] = endpoint,
                        ["Observability:ExportLogs"] = "false",
                        ["Observability:ExportMetrics"] = "true",
                        ["Observability:ExportTraces"] = "true",
                    }));
            });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Document_export_succeeds_when_the_otlp_collector_is_unavailable()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Observability:Enabled"] = "true",
                        ["Observability:OtlpEndpoint"] = "http://127.0.0.1:1",
                        ["Observability:ExportLogs"] = "false",
                        ["Observability:ExportMetrics"] = "true",
                        ["Observability:ExportTraces"] = "true",
                    }));
            });
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/export?format=html",
            new
            {
                id = "observability-export",
                name = "Telemetry independent export",
                pages = new[]
                {
                    new
                    {
                        id = "page-1",
                        elements = new[]
                        {
                            new
                            {
                                id = "text-1",
                                type = "text",
                                x = 0,
                                y = 0,
                                width = 200,
                                height = 30,
                                content = "PXA remains available",
                            },
                        },
                    },
                },
                pageSettings = new { width = 595, height = 842 },
            });

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "PXA remains available",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PostgreSql_metrics_use_a_safe_data_source_name()
    {
        Assert.Equal("pxa-database", PersistenceServiceCollectionExtensions.DataSourceName);
        Assert.DoesNotContain(
            "Password",
            PersistenceServiceCollectionExtensions.DataSourceName,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Operator_gateway_is_the_only_host_path_to_grafana()
    {
        var root = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.yml"));
        var gateway = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "operator-gateway",
            "templates",
            "default.conf.template"));
        var publicDocumentation = File.ReadAllText(Path.Combine(
            root,
            "websites",
            "PXA.Documentation",
            "src",
            "main.js"));

        Assert.DoesNotContain("127.0.0.1:3001:3000", compose, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:3001:8080", compose, StringComparison.Ordinal);
        Assert.Contains("GF_AUTH_PROXY_ENABLED: \"true\"", compose, StringComparison.Ordinal);
        Assert.Contains("GF_AUTH_BASIC_ENABLED: \"false\"", compose, StringComparison.Ordinal);
        Assert.Contains("GF_AUTH_PROXY_WHITELIST: 172.30.50.10", compose, StringComparison.Ordinal);
        Assert.Contains("auth_request /_pxa_operator_auth;", gateway, StringComparison.Ordinal);
        Assert.Contains(
            "/api/pxa/v1/admin/operator/access",
            gateway,
            StringComparison.Ordinal);
        Assert.Contains("proxy_set_header Authorization \"\";", gateway, StringComparison.Ordinal);
        Assert.Contains("proxy_set_header X-WEBAUTH-USER \"\";", gateway, StringComparison.Ordinal);
        Assert.Contains(
            "proxy_set_header X-WEBAUTH-USER $pxa_operator;",
            gateway,
            StringComparison.Ordinal);
        Assert.DoesNotContain("/operator/grafana/", publicDocumentation, StringComparison.Ordinal);
    }

    [Fact]
    public void Alertmanager_email_delivery_is_independent_and_secret_safe()
    {
        var root = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.yml"));
        var alertmanager = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "alertmanager",
            "alertmanager.yml"));
        var production = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "alertmanager",
            "alertmanager.production.yml.example"));
        var productionCompose = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "docker-compose.alerting-production.yml"));
        var template = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "alertmanager",
            "templates",
            "pxa-email.tmpl"));
        var rules = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "prometheus",
            "rules",
            "pxa-alerts.yml"));

        Assert.Contains("receiver: operator-email", alertmanager, StringComparison.Ordinal);
        Assert.Contains("smtp_smarthost: pxa-mailpit:1025", alertmanager, StringComparison.Ordinal);
        Assert.Contains("send_resolved: true", alertmanager, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1:9093:9093", compose, StringComparison.Ordinal);
        Assert.Contains("pxa-mailpit:", compose, StringComparison.Ordinal);
        Assert.Contains(
            "./deploy/observability/alertmanager/templates:/etc/alertmanager/templates:ro",
            compose,
            StringComparison.Ordinal);

        Assert.Contains(
            "smtp_auth_password_file: /run/secrets/pxa_alertmanager_smtp_password",
            production,
            StringComparison.Ordinal);
        Assert.DoesNotContain("smtp_auth_password:", production, StringComparison.Ordinal);
        Assert.Contains("pxa_alertmanager_smtp_password:", productionCompose, StringComparison.Ordinal);
        Assert.Contains("PXA_ALERTMANAGER_SMTP_PASSWORD_FILE", productionCompose, StringComparison.Ordinal);
        Assert.Contains("PXA_ALERTMANAGER_TEMPLATES_DIR", productionCompose, StringComparison.Ordinal);

        Assert.Contains(".CommonLabels.severity", template, StringComparison.Ordinal);
        Assert.Contains(".CommonLabels.service", template, StringComparison.Ordinal);
        Assert.Contains(".CommonLabels.environment", template, StringComparison.Ordinal);
        Assert.Contains(".Annotations.dashboard_path", template, StringComparison.Ordinal);
        Assert.Contains(".Annotations.runbook_id", template, StringComparison.Ordinal);
        Assert.DoesNotContain(".Labels.SortedPairs", template, StringComparison.Ordinal);
        Assert.DoesNotContain(".Annotations.SortedPairs", template, StringComparison.Ordinal);
        Assert.DoesNotContain(".CommonLabels.SortedPairs", template, StringComparison.Ordinal);
        Assert.DoesNotContain("{{ $labels.", rules, StringComparison.Ordinal);
    }

    [Fact]
    public void Failure_recovery_test_is_bounded_and_restores_stopped_dependencies()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "failure-recovery-smoke-test.sh"));

        Assert.Contains("trap restore_containers EXIT INT TERM", script, StringComparison.Ordinal);
        Assert.Contains("docker stop --time 20", script, StringComparison.Ordinal);
        Assert.Contains("docker start", script, StringComparison.Ordinal);
        Assert.Contains("PxaTelemetryPipelineUnavailable", script, StringComparison.Ordinal);
        Assert.Contains("PxaPostgreSqlUnavailable", script, StringComparison.Ordinal);
        Assert.Contains("wait_for_mail \"FIRING\"", script, StringComparison.Ordinal);
        Assert.Contains("wait_for_mail \"RESOLVED\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("docker rm", script, StringComparison.Ordinal);
        Assert.DoesNotContain("docker compose down", script, StringComparison.Ordinal);
        Assert.DoesNotContain("kill -9", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Deployment_variants_share_a_secret_safe_observability_contract()
    {
        var root = FindRepositoryRoot();
        var contract = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "DEPLOYMENT-CONTRACT.md"));
        var onPremise = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "onprem.env.example"));
        var cloud = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "cloud-storage.env.example"));
        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.yml"));
        var loki = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "loki",
            "config.s3.yml"));
        var tempo = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "tempo",
            "config.s3.yml"));

        Assert.Contains("one instrumentation and telemetry contract", contract, StringComparison.Ordinal);
        Assert.Contains("PXA_METRIC_RETENTION", onPremise, StringComparison.Ordinal);
        Assert.Contains("PXA_OTEL_MEMORY_LIMIT_MIB", onPremise, StringComparison.Ordinal);
        Assert.Contains("PXA_TLS_TARGETS_PATH", onPremise, StringComparison.Ordinal);
        Assert.Contains("PXA_LOKI_CONFIG_PATH", cloud, StringComparison.Ordinal);
        Assert.Contains("PXA_TEMPO_CONFIG_PATH", cloud, StringComparison.Ordinal);
        Assert.Contains("object_store: s3", loki, StringComparison.Ordinal);
        Assert.Contains("backend: s3", tempo, StringComparison.Ordinal);
        Assert.DoesNotContain("AWS_ACCESS_KEY_ID", cloud, StringComparison.Ordinal);
        Assert.DoesNotContain("AWS_SECRET_ACCESS_KEY", cloud, StringComparison.Ordinal);
        Assert.Contains("pxa-observability-host", compose, StringComparison.Ordinal);
        Assert.Contains("\"127.0.0.1:4317:4317\"", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void Tls_monitoring_and_webhook_delivery_are_bounded_and_secret_backed()
    {
        var root = FindRepositoryRoot();
        var prometheus = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "prometheus",
            "prometheus.yml"));
        var rules = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "prometheus",
            "rules",
            "pxa-alerts.yml"));
        var webhookCompose = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "docker-compose.webhook.yml"));
        var productionAlertmanager = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "alertmanager",
            "alertmanager.production.yml.example"));

        Assert.Contains("job_name: pxa-tls", prometheus, StringComparison.Ordinal);
        Assert.Contains("PxaTlsCertificateExpiringSoon", rules, StringComparison.Ordinal);
        Assert.Contains("PxaTlsProbeUnavailable", rules, StringComparison.Ordinal);
        Assert.Contains("pxa-blackbox-exporter:9115/-/healthy", File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "smoke-test.sh")), StringComparison.Ordinal);
        Assert.Contains("PXA_ALERT_WEBHOOK_SIGNING_KEY_FILE", webhookCompose, StringComparison.Ordinal);
        Assert.Contains("/run/secrets/pxa_alert_webhook_signing_key", webhookCompose, StringComparison.Ordinal);
        Assert.Contains("max_alerts: 100", productionAlertmanager, StringComparison.Ordinal);
        Assert.DoesNotContain("WebhookRelay__Secret:", webhookCompose, StringComparison.Ordinal);
    }

    [Fact]
    public void Destructive_and_retention_smoke_tests_require_explicit_bounded_inputs()
    {
        var root = FindRepositoryRoot();
        var ocr = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "ocr-failure-recovery-smoke-test.sh"));
        var retention = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "retention-verification.sh"));
        var performance = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "performance-overhead-test.sh"));

        Assert.Contains("PXA_OCR_FAILURE_INJECTION_CONFIRMED", ocr, StringComparison.Ordinal);
        Assert.Contains("Waiting for the baseline counter sample", ocr, StringComparison.Ordinal);
        Assert.Contains("PXA_RETENTION_METRIC_QUERY", retention, StringComparison.Ordinal);
        Assert.Contains("PXA_RETENTION_LOG_QUERY", retention, StringComparison.Ordinal);
        Assert.Contains("PXA_RETENTION_TRACE_ID", retention, StringComparison.Ordinal);
        Assert.Contains("PXA_MAX_OBSERVABILITY_OVERHEAD_PERCENT", performance, StringComparison.Ordinal);
        Assert.Contains("5", performance, StringComparison.Ordinal);
        Assert.DoesNotContain("docker rm", ocr, StringComparison.Ordinal);
        Assert.DoesNotContain("docker compose down", ocr, StringComparison.Ordinal);
    }

    private sealed record Measurement(
        string Name,
        double Value,
        KeyValuePair<string, object?>[] Tags);

    private static IConfiguration BuildLoggingConfiguration(string level) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = level,
            })
            .Build();

    private static ActivityListener ListenToPxaActivities()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PxaTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PXA.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "PXA.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StubOcrEngine : IOcrEngine
    {
        public int CallCount { get; private set; }
        public string Name => "stub";
        public string Version => "test";

        public Task<IReadOnlyList<OcrPage>> RecognizeAsync(
            IReadOnlyList<OcrImagePage> pages,
            ImageToPdfConversionOptions options,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<OcrPage>>([]);
        }
    }
}
