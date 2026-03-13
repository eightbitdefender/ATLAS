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

    public VulnerabilitiesController(
        AtlasContext         db,
        NvdSyncService       nvd,
        SyncProgressTracker  progress,
        IServiceScopeFactory scopeFactory)
    {
        _db           = db;
        _nvd          = nvd;
        _progress     = progress;
        _scopeFactory = scopeFactory;
    }

    // ── Library ──────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index()
    {
        var vulns = await _db.Vulnerabilities
            .Include(v => v.AssetVulnerabilities)
            .OrderBy(v => v.Severity)
            .ToListAsync();
        return View(vulns);
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
