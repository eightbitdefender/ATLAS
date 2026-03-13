using System.Runtime.CompilerServices;
using System.Text.Json;
using ATLAS.Data;
using ATLAS.Models;
using Microsoft.EntityFrameworkCore;

namespace ATLAS.Services;

// ── Result returned to the controller ────────────────────────────────────────

public class SyncResult
{
    public int    Added     { get; set; }
    public int    Updated   { get; set; }
    public int    Total     { get; set; }
    public string? Error    { get; set; }
    public bool   Success   => Error == null;
}

// ── Service ───────────────────────────────────────────────────────────────────

public class NvdSyncService(
    HttpClient             http,
    IConfiguration         config,
    ILogger<NvdSyncService> logger,
    SyncProgressTracker    progress)
{
    private const string BaseUrl = "https://services.nvd.nist.gov/rest/json/cves/2.0";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── Public entry point ──────────────────────────────────────────────────

    public async Task<SyncResult> SyncAsync(
        AtlasContext db,
        string?     keyword  = null,
        string?     severity = null)
    {
        var result = new SyncResult();
        try
        {
            // Load all existing CVE IDs once so every page's upsert is O(1)
            // and we never duplicate a CVE even across page boundaries.
            var existing = await db.Vulnerabilities
                .Where(v => v.CveId != null)
                .ToDictionaryAsync(v => v.CveId!);

            // Fetch one NVD page at a time and commit it to the database before
            // requesting the next page. This means an interrupted sync preserves
            // everything already saved — on restart those CVEs are simply
            // refreshed (updated) rather than lost or re-inserted.
            await foreach (var page in FetchPagesAsync(keyword, severity))
            {
                result.Total += page.Count;

                foreach (var wrapper in page)
                {
                    var mapped = MapToVulnerability(wrapper);

                    if (existing.TryGetValue(mapped.CveId!, out var dbVuln))
                    {
                        // Update — preserve the original DiscoveredAt and any asset links
                        dbVuln.Title               = mapped.Title;
                        dbVuln.Description         = mapped.Description;
                        dbVuln.Severity            = mapped.Severity;
                        dbVuln.CvssScore           = mapped.CvssScore;
                        dbVuln.AffectedSoftware    = mapped.AffectedSoftware;
                        dbVuln.RemediationGuidance = mapped.RemediationGuidance;
                        result.Updated++;
                    }
                    else
                    {
                        db.Vulnerabilities.Add(mapped);
                        existing[mapped.CveId!] = mapped; // prevent duplicate insert within same batch
                        result.Added++;
                    }
                }

                // Commit this page to disk before fetching the next one.
                // SQLite page writes take ~10–50 ms — negligible versus the NVD
                // rate-limit delay — but guarantee at-most-one-page data loss on crash.
                await db.SaveChangesAsync();

                // Keep the tracker's running totals in sync for the UI
                progress.UpdateSaved(result.Added, result.Updated);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NVD sync failed");
            result.Error = ex.Message;
        }

        return result;
    }

    // ── Page-by-page fetch (yields one NVD page at a time) ─────────────────
    // IAsyncEnumerable lets the caller save between pages without buffering
    // the entire result set in memory.

    private async IAsyncEnumerable<List<NvdVulnWrapper>> FetchPagesAsync(
        string? keyword, string? severity,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int pageSize     = 2000; // NVD maximum — minimises number of API round trips
        int start        = 0;
        int totalFetched = 0;

        // NVD rate limit: 5 req / 30 s without API key (≈ 6 s between calls)
        //                50 req / 30 s with API key    (≈ 0.6 s between calls)
        var apiKey = config["NvdApiKey"];
        int delay  = string.IsNullOrWhiteSpace(apiKey) ? 6500 : 700;

        while (!ct.IsCancellationRequested)
        {
            var url = BuildUrl(keyword, severity, pageSize, start, apiKey);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(apiKey))
                request.Headers.Add("apiKey", apiKey);

            var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var body        = await response.Content.ReadAsStringAsync(ct);
            var nvdResponse = JsonSerializer.Deserialize<NvdResponse>(body, JsonOpts);

            if (nvdResponse?.Vulnerabilities is null or { Count: 0 })
                break;

            totalFetched += nvdResponse.Vulnerabilities.Count;

            // Report live fetch progress to the tracker before yielding
            // so the UI counter updates as soon as the page arrives
            progress.UpdateFetch(totalFetched, nvdResponse.TotalResults);

            yield return nvdResponse.Vulnerabilities;

            // Stop when we have received every matching CVE NVD has
            if (totalFetched >= nvdResponse.TotalResults)
                break;

            start += nvdResponse.ResultsPerPage;

            // Respect rate limit before requesting the next page
            await Task.Delay(delay, ct);
        }
    }

    private static string BuildUrl(
        string? keyword, string? severity,
        int resultsPerPage, int startIndex,
        string? apiKey)
    {
        var qs = new List<string>
        {
            $"resultsPerPage={resultsPerPage}",
            $"startIndex={startIndex}"
        };

        if (!string.IsNullOrWhiteSpace(keyword))
            qs.Add($"keywordSearch={Uri.EscapeDataString(keyword)}");

        if (!string.IsNullOrWhiteSpace(severity))
            qs.Add($"cvssV3Severity={severity.ToUpperInvariant()}");

        return $"{BaseUrl}?{string.Join("&", qs)}";
    }

    // ── Mapping NVD → Vulnerability ─────────────────────────────────────────

    private static Vulnerability MapToVulnerability(NvdVulnWrapper wrapper)
    {
        var cve = wrapper.Cve;

        var description = cve.Descriptions
            .FirstOrDefault(d => d.Lang == "en")?.Value ?? "";

        // ── CVSS: prefer V3.1 → V3.0 → V2 ────────────────────────────────
        double? cvssScore   = null;
        string  severityStr = "";

        var v31 = PrimaryOrFirst(cve.Metrics.CvssMetricV31);
        var v30 = PrimaryOrFirst(cve.Metrics.CvssMetricV30);
        var v2  = PrimaryOrFirst(cve.Metrics.CvssMetricV2);

        if (v31 is not null)
        {
            cvssScore   = v31.CvssData.BaseScore;
            severityStr = v31.CvssData.BaseSeverity;
        }
        else if (v30 is not null)
        {
            cvssScore   = v30.CvssData.BaseScore;
            severityStr = v30.CvssData.BaseSeverity;
        }
        else if (v2 is not null)
        {
            cvssScore   = v2.CvssData.BaseScore;
            severityStr = cvssScore switch
            {
                >= 7.0 => "HIGH",
                >= 4.0 => "MEDIUM",
                > 0    => "LOW",
                _      => "INFORMATIONAL"
            };
        }

        var severity = severityStr.ToUpperInvariant() switch
        {
            "CRITICAL" => Severity.Critical,
            "HIGH"     => Severity.High,
            "MEDIUM"   => Severity.Medium,
            "LOW"      => Severity.Low,
            _          => Severity.Informational
        };

        // ── Affected software from CPE strings ─────────────────────────────
        var cpeNames = AllCpeMatches(cve.Configurations)
            .Where(m => m.Vulnerable)
            .Select(m => ParseCpeName(m.Criteria))
            .Where(s => s.Length > 0)
            .Distinct()
            .Take(6)
            .ToList();

        string? affectedSoftware = cpeNames.Count > 0
            ? Truncate(string.Join(", ", cpeNames), 500)
            : null;

        // ── Title: first 200 chars of description (or CVE ID if blank) ─────
        var title = description.Length > 0
            ? Truncate(description, 200)
            : cve.Id;

        DateTime discoveredAt = DateTime.TryParse(cve.Published, out var pub)
            ? pub.ToUniversalTime()
            : DateTime.UtcNow;

        return new Vulnerability
        {
            CveId               = cve.Id,
            Title               = title,
            Description         = Truncate(description, 2000),
            Severity            = severity,
            CvssScore           = cvssScore,
            AffectedSoftware    = affectedSoftware,
            RemediationGuidance = "Refer to the NVD advisory and apply vendor-supplied patches.",
            DiscoveredAt        = discoveredAt
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static T? PrimaryOrFirst<T>(List<T> list) where T : class
    {
        if (list.Count == 0) return null;
        // Try to find a "Primary" source; fall back to first entry
        if (list is List<NvdCvssV3Metric> v3)
            return (T?)(object)(v3.FirstOrDefault(m => m.Type == "Primary") ?? v3[0]);
        if (list is List<NvdCvssV2Metric> v2)
            return (T?)(object)(v2.FirstOrDefault(m => m.Type == "Primary") ?? v2[0]);
        return list[0];
    }

    private static IEnumerable<NvdCpeMatch> AllCpeMatches(List<NvdConfiguration> configs)
    {
        foreach (var cfg in configs)
            foreach (var match in AllNodeMatches(cfg.Nodes))
                yield return match;
    }

    private static IEnumerable<NvdCpeMatch> AllNodeMatches(List<NvdNode> nodes)
    {
        foreach (var node in nodes)
        {
            foreach (var m in node.CpeMatch) yield return m;
            foreach (var m in AllNodeMatches(node.Children)) yield return m;
        }
    }

    /// <summary>Parses "cpe:2.3:a:vendor:product:version:..." → "product version"</summary>
    private static string ParseCpeName(string cpe)
    {
        var parts = cpe.Split(':');
        if (parts.Length < 5) return "";
        var product = parts[4].Replace('_', ' ');
        var version = parts.Length > 5 && parts[5] is { Length: > 0 } v && v != "*" ? v : "";
        return version.Length > 0 ? $"{product} {version}" : product;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];
}
