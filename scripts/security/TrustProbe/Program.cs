using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Http;
using NzbDrone.Common.Http.Dispatchers;
using NzbDrone.Common.Http.Proxy;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Security;

var logger = LogManager.CreateNullLogger();
var validation = new ObservedValidation(new X509CertificateValidationService(
    DispatchProxy.Create<IConfigService, EnabledValidation>(), logger));
var cache = new CacheManager();
var client = new ManagedHttpDispatcher(new NoProxy(), new ManagedWebProxyFactory(cache), validation,
    new ProbeUserAgent(), cache, logger);
var control = "https://www.google.com/";
var urls = new List<string> { control };
foreach (var kind in new[] { "dv", "ov", "ev" })
foreach (var generation in new[] { "g3b", "g3c" })
foreach (var crossSign in new[] { string.Empty, "-aaa" })
{
    urls.Add($"https://{kind}-{generation}{crossSign}-emudhra.we-have-a-problem.com/");
}

Console.WriteLine(JsonSerializer.Serialize(new { runtime = RuntimeInformation.FrameworkDescription,
    client = typeof(ManagedHttpDispatcher).Assembly.FullName, validation = CertificateValidationType.Enabled.ToString() }));
var passed = true;
foreach (var url in urls)
{
    validation.RejectedTrustChain = false;
    try
    {
        var response = await client.GetResponseAsync(new HttpRequest(url) { RequestTimeout = TimeSpan.FromSeconds(15) }, new CookieContainer());
        var pass = url == control && response.StatusCode == HttpStatusCode.OK;
        Console.WriteLine(JsonSerializer.Serialize(new { url, status = (int)response.StatusCode, pass }));
        passed &= pass;
    }
    catch (Exception error)
    {
        // The real application callback must observe a trust-chain error and reject it.
        // A timeout, DNS/connect error, expired certificate or wrong hostname cannot pass.
        var pass = url != control && validation.RejectedTrustChain;
        Console.WriteLine(JsonSerializer.Serialize(new { url, error = error.GetType().FullName, validation.RejectedTrustChain, pass }));
        passed &= pass;
    }
}

return passed ? 0 : 1;

// Supply the application's normal enabled setting without touching user configuration.
public class EnabledValidation : DispatchProxy
{
    protected override object Invoke(MethodInfo method, object[] args)
    {
        if (method.Name == "get_CertificateValidation") return CertificateValidationType.Enabled;
        throw new InvalidOperationException($"Unexpected configuration access: {method.Name}");
    }
}

internal sealed class NoProxy : IHttpProxySettingsProvider
{
    public HttpProxySettings GetProxySettings(HttpUri uri) => null;
    public HttpProxySettings GetProxySettings() => null;
}

internal sealed class ProbeUserAgent : IUserAgentBuilder
{
    public string GetUserAgent(bool simplified = false) => "Sonarr-Public-Trust-Probe";
}

internal sealed class ObservedValidation : ICertificateValidationService
{
    private readonly ICertificateValidationService _applicationValidation;
    public bool RejectedTrustChain { get; set; }
    public ObservedValidation(ICertificateValidationService applicationValidation) => _applicationValidation = applicationValidation;
    public bool ShouldByPassValidationError(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
    {
        var accepted = _applicationValidation.ShouldByPassValidationError(sender, certificate, chain, errors);
        if (!accepted && errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
        {
            RejectedTrustChain |= chain.ChainStatus.Any(status => status.Status == X509ChainStatusFlags.PartialChain ||
                status.Status == X509ChainStatusFlags.UntrustedRoot || status.Status == X509ChainStatusFlags.Revoked);
        }

        return accepted;
    }
}
