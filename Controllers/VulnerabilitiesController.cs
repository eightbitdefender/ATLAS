using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ATLAS.Data;
using ATLAS.Models;
using ATLAS.Services;

namespace ATLAS.Controllers;

public class VulnerabilitiesController : Controller
{
    private readonly AtlasContext    _db;
    private readonly NvdSyncService _nvd;

    public VulnerabilitiesController(AtlasContext db, NvdSyncService nvd)
    {
        _db  = db;
        _nvd = nvd;
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

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncFromNvd(
        string? keyword,
        string? severity)
    {
        var result = await _nvd.SyncAsync(_db, keyword, severity);

        if (result.Success)
        {
            TempData["SyncSuccess"] =
                $"Sync complete — {result.Added} added, {result.Updated} updated " +
                $"({result.Total} CVEs fetched from NVD).";
        }
        else
        {
            TempData["SyncError"] = $"Sync failed: {result.Error}";
        }

        return RedirectToAction(nameof(Index));
    }
}
