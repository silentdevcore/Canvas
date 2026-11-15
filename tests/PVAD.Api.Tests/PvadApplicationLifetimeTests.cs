using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PVAD.WebApi.Application.Retention;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class PvadApplicationLifetimeTests
{
    [Fact]
    public async Task Retention_rejection_returns_configuration_exit_code_and_disposes_the_host()
    {
        var disposal = new DisposalProbe();
        var host = RejectedHost(new PvadRetentionApprovalRequiredException(), disposal);

        Assert.Equal(78, await PvadApplicationLifetime.RunAsync(host));
        Assert.True(disposal.Disposed);
    }

    public static IEnumerable<object[]> UnexpectedFailures()
    {
        yield return [new InvalidOperationException(new PvadRetentionApprovalRequiredException().Message)];
        yield return [new IOException("Synthetic unrelated startup failure.")];
        yield return [new OperationCanceledException("Synthetic unrelated cancellation.")];
        yield return [new AggregateException(new PvadRetentionApprovalRequiredException(), new IOException("Synthetic second failure."))];
    }

    [Theory]
    [MemberData(nameof(UnexpectedFailures))]
    public async Task Other_failures_are_not_converted_to_a_retention_exit(Exception failure)
    {
        var disposal = new DisposalProbe();
        var host = RejectedHost(failure, disposal);

        var actual = await Record.ExceptionAsync(() => PvadApplicationLifetime.RunAsync(host));

        Assert.Same(failure, actual);
        Assert.True(disposal.Disposed);
    }

    [Fact]
    public async Task Normal_shutdown_returns_zero_and_disposes_the_host()
    {
        var disposal = new DisposalProbe();
        var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddSingleton(_ => disposal);
            services.AddHostedService<StopOnStart>();
        }).Build();
        _ = host.Services.GetRequiredService<DisposalProbe>();

        Assert.Equal(0, await PvadApplicationLifetime.RunAsync(host));
        Assert.True(disposal.Disposed);
    }

    private static IHost RejectedHost(Exception failure, DisposalProbe disposal)
    {
        var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddSingleton(_ => disposal);
            services.AddSingleton<IHostedService>(new RejectOnStart(failure));
        }).Build();
        _ = host.Services.GetRequiredService<DisposalProbe>();
        return host;
    }

    [Fact]
    public async Task Initial_retention_refusal_disposes_without_starting_host_services()
    {
        var disposal = new DisposalProbe();
        var startup = new StartupProbe();
        var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddSingleton(_ => disposal);
            services.AddSingleton<IHostedService>(startup);
        }).Build();
        _ = host.Services.GetRequiredService<DisposalProbe>();

        var code = await PvadApplicationLifetime.RunAsync(host, initialize: _ =>
            Task.FromException(new PvadRetentionApprovalRequiredException()));

        Assert.Equal(78, code);
        Assert.False(startup.Started);
        Assert.True(disposal.Disposed);
    }

    [Theory]
    [InlineData("invalid-operation")]
    [InlineData("io")]
    [InlineData("cancellation")]
    [InlineData("aggregate")]
    public async Task Other_initialization_failures_are_preserved_and_dispose_without_starting_services(string kind)
    {
        Exception failure = kind switch
        {
            "invalid-operation" => new InvalidOperationException("Synthetic initialization failure."),
            "io" => new IOException("Synthetic initialization failure."),
            "cancellation" => new OperationCanceledException("Synthetic initialization cancellation."),
            "aggregate" => new AggregateException(new PvadRetentionApprovalRequiredException(), new IOException("Synthetic second failure.")),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var disposal = new DisposalProbe();
        var startup = new StartupProbe();
        var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddSingleton(_ => disposal);
            services.AddSingleton<IHostedService>(startup);
        }).Build();
        _ = host.Services.GetRequiredService<DisposalProbe>();

        var actual = await Record.ExceptionAsync(() => PvadApplicationLifetime.RunAsync(host,
            initialize: _ => Task.FromException(failure)));

        Assert.Same(failure, actual);
        Assert.False(startup.Started);
        Assert.True(disposal.Disposed);
    }

    [Fact]
    public async Task Initialization_completes_before_host_start_and_receives_the_cancellation_token()
    {
        var initialized = false;
        using var cancellation = new CancellationTokenSource();
        var host = new HostBuilder().ConfigureServices(services =>
            services.AddHostedService(provider => new InitializedStopOnStart(
                provider.GetRequiredService<IHostApplicationLifetime>(), () => initialized))).Build();

        var result = await PvadApplicationLifetime.RunAsync(host, cancellation.Token, initialize: token =>
        {
            Assert.Equal(cancellation.Token, token);
            initialized = true;
            return Task.CompletedTask;
        });

        Assert.Equal(0, result);
        Assert.True(initialized);
    }

    private sealed class StartupProbe : IHostedService
    {
        public bool Started { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;
            throw new InvalidOperationException("Services must not start before initialization succeeds.");
        }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InitializedStopOnStart(IHostApplicationLifetime lifetime, Func<bool> initialized) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Assert.True(initialized());
            lifetime.StopApplication();
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RejectOnStart(Exception failure) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.FromException(failure);
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StopOnStart(IHostApplicationLifetime lifetime) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            lifetime.StopApplication();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class DisposalProbe : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
