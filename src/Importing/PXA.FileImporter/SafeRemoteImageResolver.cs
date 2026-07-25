using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using SkiaSharp;

namespace PXA.FileImporter;

/// <summary>
/// Downloads public HTTP(S) PNG/JPEG images with bounded resource usage and SSRF protection.
/// </summary>
public sealed class SafeRemoteImageResolver : IRemoteImageResolver, IDisposable
{
    private const int MaxRedirects = 3;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    private readonly HttpClient _client;
    private readonly Func<Uri, CancellationToken, Task<bool>> _isAllowedUri;
    private readonly bool _ownsClient;

    public SafeRemoteImageResolver()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = ConnectToValidatedPublicAddressAsync,
        };
        _client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _isAllowedUri = IsAllowedRemoteUriAsync;
        _ownsClient = true;
    }

    internal SafeRemoteImageResolver(
        HttpClient client,
        Func<Uri, CancellationToken, Task<bool>> isAllowedUri)
    {
        _client = client;
        _isAllowedUri = isAllowedUri;
    }

    public async Task<string?> ResolveAsDataUrlAsync(
        string source,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var current))
            return null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        for (var redirect = 0; redirect <= MaxRedirects; redirect++)
        {
            timeout.Token.ThrowIfCancellationRequested();
            if (!await _isAllowedUri(current, timeout.Token))
                return null;

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using var response = await _client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            if (IsRedirect(response.StatusCode))
            {
                if (redirect == MaxRedirects || response.Headers.Location is null)
                    return null;

                current = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(current, response.Headers.Location);
                continue;
            }

            if (!response.IsSuccessStatusCode)
                return null;

            var declaredMime = NormalizeMimeType(response.Content.Headers.ContentType);
            if (declaredMime is null)
                return null;
            if (response.Content.Headers.ContentLength is > MarkdownFileImporter.MaxEmbeddedImageBytes)
                return null;

            var bytes = await ReadCappedAsync(response.Content, timeout.Token);
            if (bytes is null)
                return null;

            var detectedMime = DetectSupportedImageMime(bytes);
            if (detectedMime is null || detectedMime != declaredMime)
                return null;

            return $"data:{detectedMime};base64,{Convert.ToBase64String(bytes)}";
        }

        return null;
    }

    internal static async Task<bool> IsAllowedRemoteUriAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedHttpUri(uri))
            return false;

        try
        {
            var addresses = IPAddress.TryParse(uri.Host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);

            return addresses.Length > 0 && addresses.All(IsPubliclyRoutable);
        }
        catch (Exception exception) when (
            exception is SocketException or ArgumentException)
        {
            return false;
        }
    }

    internal static bool IsSupportedHttpUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            return false;

        return uri.IsDefaultPort ||
               (uri.Scheme == Uri.UriSchemeHttp && uri.Port == 80) ||
               (uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443);
    }

    internal static bool IsPubliclyRoutable(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) ||
            address.Equals(IPAddress.IPv6None) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal ||
            address.IsIPv6Multicast)
            return false;

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (bytes[0] & 0xfe) != 0xfc;

        if (address.AddressFamily != AddressFamily.InterNetwork)
            return false;

        return bytes switch
        {
            [0, ..] => false,
            [10, ..] => false,
            [100, >= 64 and <= 127, ..] => false,
            [127, ..] => false,
            [169, 254, ..] => false,
            [172, >= 16 and <= 31, ..] => false,
            [192, 0, 0, ..] => false,
            [192, 0, 2, ..] => false,
            [192, 168, ..] => false,
            [198, 18 or 19, ..] => false,
            [198, 51, 100, ..] => false,
            [203, 0, 113, ..] => false,
            [>= 224, ..] => false,
            _ => true,
        };
    }

    private static async ValueTask<Stream> ConnectToValidatedPublicAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(
            context.DnsEndPoint.Host,
            cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !IsPubliclyRoutable(address)))
            throw new HttpRequestException("Remote image host is not publicly routable.");

        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };
            try
            {
                await socket.ConnectAsync(
                    new IPEndPoint(address, context.DnsEndPoint.Port),
                    cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (
                exception is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                lastError = exception;
                if (exception is OperationCanceledException)
                    throw;
            }
        }

        throw new HttpRequestException("Could not connect to the remote image host.", lastError);
    }

    private static async Task<byte[]?> ReadCappedAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream();
        var buffer = new byte[81920];

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;
            if (destination.Length + read > MarkdownFileImporter.MaxEmbeddedImageBytes)
                return null;

            destination.Write(buffer, 0, read);
        }

        return destination.ToArray();
    }

    private static string? DetectSupportedImageMime(byte[] bytes)
    {
        try
        {
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            if (codec is null ||
                codec.Info.Width <= 0 ||
                codec.Info.Height <= 0 ||
                (long)codec.Info.Width * codec.Info.Height > MarkdownFileImporter.MaxEmbeddedImagePixels)
                return null;

            return codec.EncodedFormat switch
            {
                SKEncodedImageFormat.Png => "image/png",
                SKEncodedImageFormat.Jpeg => "image/jpeg",
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeMimeType(MediaTypeHeaderValue? contentType) =>
        contentType?.MediaType?.ToLowerInvariant() switch
        {
            "image/png" => "image/png",
            "image/jpeg" or "image/jpg" => "image/jpeg",
            _ => null,
        };

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Moved or
            HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }
}
