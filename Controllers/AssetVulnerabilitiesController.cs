using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ATLAS.Data;
using ATLAS.Models;

namespace ATLAS.Controllers;

public class AssetVulnerabilitiesController : Controller
{
    private readonly AtlasContext _db;

    public AssetVulnerabilitiesController(AtlasContext db) => _db = db;

    public async Task<IActionResult> Create(int? assetId)
    {
        await PopulateSelectLists(assetId);
        return View(new AssetVulnerability { AssetId = assetId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AssetVulnerability av)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSelectLists(av.AssetId);
            return View(av);
        }
        av.DetectedAt = DateTime.UtcNow;
        _db.AssetVulnerabilities.Add(av);
        await _db.SaveChangesAsync();
        return RedirectToAction("Details", "Assets", new { id = av.AssetId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var av = await _db.AssetVulnerabilities
            .Include(x => x.Asset)
            .Include(x => x.Vulnerability)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (av == null) return NotFound();
        await PopulateSelectLists(av.AssetId);
        return View(av);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AssetVulnerability av)
    {
        if (id != av.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateSelectLists(av.AssetId);
            return View(av);
        }
        if (av.Status == RemediationStatus.Remediated && av.RemediatedAt == null)
            av.RemediatedAt = DateTime.UtcNow;

        _db.Entry(av).State = EntityState.Modified;
        _db.Entry(av).Property(x => x.DetectedAt).IsModified = false;
        await _db.SaveChangesAsync();
        return RedirectToAction("Details", "Assets", new { id = av.AssetId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var av = await _db.AssetVulnerabilities.FindAsync(id);
        if (av != null)
        {
            int assetId = av.AssetId;
            _db.AssetVulnerabilities.Remove(av);
            await _db.SaveChangesAsync();
            return RedirectToAction("Details", "Assets", new { id = assetId });
        }
        return RedirectToAction("Index", "Assets");
    }

    private async Task PopulateSelectLists(int? selectedAssetId = null)
    {
        ViewBag.Assets = new SelectList(
            await _db.Assets.OrderBy(a => a.Name).ToListAsync(),
            "Id", "Name", selectedAssetId);
        ViewBag.Vulnerabilities = new SelectList(
            await _db.Vulnerabilities.OrderBy(v => v.Title).ToListAsync(),
            "Id", "Title");
    }
}
