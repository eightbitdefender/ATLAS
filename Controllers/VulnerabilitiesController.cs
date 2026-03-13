using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ATLAS.Data;
using ATLAS.Models;
using ATLAS.Services;

namespace ATLAS.Controllers;

public class VulnerabilitiesController : Controller
{
    private readonly AtlasContext           _db;
    private readonly NvdSyncService         _nvd;
    private readonly SyncProgressTracker    _progress;
    private readonly IServiceScopeFactory   _scopeFactory;
    private readonly IConfiguration         _config;

    public VulnerabilitiesController(
        AtlasContext         db,
        NvdSyncService       nvd,
        SyncProgressTracker  progress,
        IServiceScopeFactory scopeFactory,
        IConfiguration       config)
    {
        _db           = db;
        _nvd          = nvd;
        _progress     = progress;
        _scopeFactory = scopeFactory;
        _config       = config;
    }

    // ── Library ──────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index(
        string? search   = null,
        string? severity = null,
        int     page     = 1)
    {
        const int PageSize = 50;

        // ── 1. Summary stats — fast aggregation queries, no entity materialisation ──
        var totalCount = await _db.Vulnerabilities.CountAsync();

        var countsBySev = await _db.Vulnerabilities
            .GroupBy(v => v.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync();

        var lastSync = await _db.Vulnerabilities
            .MaxAsync(v => (DateTime?)v.DiscoveredAt);

        // ── 2. Build filtered IQueryable ──────────────────────────────────────────
        var query = _db.Vulnerabilities.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(v =>
                (v.CveId != null && v.CveId.Contains(term)) ||
                v.Title.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(severity) && severity != "all" &&
            Enum.TryParse<Severity>(severity, ignoreCase: true, out var sev))
        {
            query = query.Where(v => v.Severity == sev);
        }
        else if (string.IsNullOrWhiteSpace(severity))
        {
            // Default landing: Critical + High only — keeps the table useful on first load
            query = query.Where(v => v.Severity == Severity.Critical ||
                                     v.Severity == Severity.High);
        }
        // severity == "all" → no additional WHERE, show everything

        // ── 3. Filtered count (single COUNT query) ─────────────────────────────────
        var totalFiltered = await query.CountAsync();

        // ── 4. Paginated fetch — no Include, no full-table materialisation ──────────
        page = Math.Max(1, page);
        var items = await query
            .OrderBy(v => v.Severity)
            .ThenByDescending(v => v.CvssScore)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        // ── 5. Assemble ViewModel ─────────────────────────────────────────────────
        var vm = new VulnerabilityIndexViewModel
        {
            TotalCount         = totalCount,
            CriticalCount      = countsBySev.FirstOrDefault(x => x.Severity == Severity.Critical)?.Count      ?? 0,
            HighCount          = countsBySev.FirstOrDefault(x => x.Severity == Severity.High)?.Count          ?? 0,
            MediumCount        = countsBySev.FirstOrDefault(x => x.Severity == Severity.Medium)?.Count        ?? 0,
            LowCount           = countsBySev.FirstOrDefault(x => x.Severity == Severity.Low)?.Count           ?? 0,
            InformationalCount = countsBySev.FirstOrDefault(x => x.Severity == Severity.Informational)?.Count ?? 0,
            LastSyncDate       = lastSync,
            Search             = search,
            SeverityFilter     = severity,
            Page               = page,
            PageSize           = PageSize,
            Items              = items,
            TotalFilteredCount = totalFiltered,
            NvdKeyConfigured   = !string.IsNullOrWhiteSpace(_config["NvdApiKey"])
        };

        return View(vm);
    }

    // ── CVE search (AJAX — used by AssetVulnerabilities Create picker) ────────

    /// <summary>
    /// Returns top-20 CVE matches for a search term.
    /// GET /Vulnerabilities/SearchVulnerabilities?q=log4j
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchVulnerabilities(string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Json(Array.Empty<object>());

        var term = q.Trim();
        var results = await _db.Vulnerabilities
            .Where(v => (v.CveId != null && v.CveId.Contains(term)) ||
                        v.Title.Contains(term))
            .OrderBy(v => v.Severity)
            .ThenByDescending(v => v.CvssScore)
            .Take(20)
            .Select(v => new
            {
                v.Id,
                v.CveId,
                v.Title,
                Severity  = v.Severity.ToString(),
                CvssScore = v.CvssScore.HasValue
                    ? v.CvssScore.Value.ToString("F1")
                    : (string?)null
            })
            .ToListAsync();

        return Json(results);
    }

    public async Task<IActionResult> Details(int id)
    {
        var vuln = await _db.Vulnerabilities
            .Include(v => v.AssetVulnerabilities)
                .ThenInclude(av => av.Asset)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (vuln == null) return NotFound();
        return View(vuln);
    }

    // ── Manual create / edit / delete ────────────────────────────────────────

    public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Vulnerability vuln)
    {
        if (!ModelState.IsValid) return View(vuln);
        vuln.DiscoveredAt = DateTime.UtcNow;
        _db.Vulnerabilities.Add(vuln);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var vuln = await _db.Vulnerabilities.FindAsync(id);
        if (vuln == null) return NotFound();
        return View(vuln);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Vulnerability vuln)
    {
        if (id != vuln.Id) return BadRequest();
        if (!ModelState.IsValid) return View(vuln);
        _db.Entry(vuln).State = EntityState.Modified;
        _db.Entry(vuln).Property(v => v.DiscoveredAt).IsModified = false;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var vuln = await _db.Vulnerabilities.FindAsync(id);
        if (vuln == null) return NotFound();
        return View(vuln);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var vuln = await _db.Vulnerabilities.FindAsync(id);
        if (vuln != null)
        {
            _db.Vulnerabilities.Remove(vuln);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    // ── NVD Sync ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Kicks off an NVD sync in a background Task and returns immediately.
    /// The UI polls SyncStatus() every 2 s to show live progress.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SyncFromNvd(string? keyword, string? severity)
    {
        if (_progress.IsRunning)
            return Json(new { started = false, message = "A sync is already in progress." });

        _progress.Start();

        // Fire and forget — use a fresh DI scope so the DbContext lifetime is correct
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db  = scope.ServiceProvider.GetRequiredService<AtlasContext>();
                var nvd = scope.ServiceProvider.GetRequiredService<NvdSyncService>();

                var result = await nvd.SyncAsync(db, keyword, severity);
                _progress.Complete(result);
            }
            catch (Exception ex)
            {
                _progress.Complete(new SyncResult { Error = ex.Message });
            }
        });

        return Json(new { started = true });
    }

    /// <summary>
    /// Polled every 2 s by the UI while a sync is running.
    /// Returns current progress as JSON.
    /// </summary>
    [HttpGet]
    public IActionResult SyncStatus() => Json(_progress.GetStatus());
}
