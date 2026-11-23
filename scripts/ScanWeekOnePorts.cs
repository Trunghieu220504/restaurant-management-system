#:property PublishAot=false
#:property RestorePackagesWithLockFile=false

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

// Only this project's explicitly authorized VPS. TCP connect probes; no authentication.
const string host = "staging.quanlynh.id.vn";
var address = IPAddress.Parse("103.69.97.224");
if (!(await Dns.GetHostAddressesAsync(host)).Contains(address))
    throw new InvalidOperationException("DNS no longer matches the authorized VPS; stop before scanning.");
var output = Environment.GetEnvironmentVariable("WEEK1_PORT_REPORT") ?? "port-report.json";
var started = DateTime.UtcNow;
var open = new ConcurrentBag<int>();
using var stop = new CancellationTokenSource();
using var gate = new SemaphoreSlim(64);
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
var tasks = new List<Task>();
var completed = 0;
var healthy = false;
string? error = null;
async Task Health()
{
    using var response = await http.GetAsync($"https://{host}/health/ready");
    if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("Health failed; abort probes immediately.");
}
async Task Probe(int port)
{
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
    timeout.CancelAfter(TimeSpan.FromMilliseconds(1500));
    using var client = new TcpClient(AddressFamily.InterNetwork);
    try { await client.ConnectAsync(address, port, timeout.Token); open.Add(port); }
    catch (SocketException) { }
    catch (OperationCanceledException) { }
    finally { Interlocked.Increment(ref completed); gate.Release(); }
}
try
{
    await Health();
    for (var port = 1; port <= 65535; port++)
    {
        await gate.WaitAsync(stop.Token);
        tasks.Add(Probe(port));
        await Task.Delay(20); // At most 50 new probes/second; 64 pending sockets maximum.
        if (port % 1000 == 0)
        {
            await Health();
            Console.WriteLine($"TCP_PROGRESS completed={Volatile.Read(ref completed)}/65535 open={open.Count}");
        }
    }
    await Task.WhenAll(tasks);
    await Health();
    healthy = true;
}
catch (Exception exception)
{
    error = exception.GetType().Name + ": scan/health failed; do not claim a complete scan.";
    stop.Cancel();
    await Task.WhenAll(tasks);
}
var ports = open.Order().ToArray();
var passed = healthy && completed == 65535 && ports.SequenceEqual(new[] { 22, 80, 443 });
var report = new
{
    host, source = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ? "GitHub Actions external runner" : "unspecified external network",
    scope = "TCP IPv4 connect 1-65535 only; not UDP/IPv6 or a 4G attestation",
    startedAt = started, finishedAt = DateTime.UtcNow, completed, openPorts = ports,
    healthBeforeAndAfter = healthy, status = passed ? "PASS" : "FAIL_OR_INCOMPLETE", error
};
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"TCP_RESULT {report.status} completed={completed} open={string.Join(',', ports)}");
return passed ? 0 : 1;
