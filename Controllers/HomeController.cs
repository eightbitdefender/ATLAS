using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ATLAS.Data;
using ATLAS.Models;

namespace ATLAS.Controllers;

public class HomeController : Controller
{
    private readonly AtlasContext _db;

    public HomeController(AtlasContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var stats = new DashboardViewModel
        {
            TotalAssets = await _db.Assets.CountAsync(),
            ActiveAssets = await _db.Assets.CountAsync(a => a.Status == AssetStatus.Active),
            TotalVulnerabilities = await _db.Vulnerabilities.CountAsync(),
            OpenFindings = await _db.AssetVulnerabilities.CountAsync(av => av.Status == RemediationStatus.Open),
            CriticalFindings = await _db.AssetVulnerabilities
                .CountAsync(av => av.Vulnerability.Severity == Severity.Critical && av.Status == RemediationStatus.Open),
            HighFindings = await _db.AssetVulnerabilities
                .CountAsync(av => av.Vulnerability.Severity == Severity.High && av.Status == RemediationStatus.Open),
            RecentAssets = await _db.Assets
                .OrderByDescending(a => a.CreatedAt).Take(5).ToListAsync(),
            RecentFindings = await _db.AssetVulnerabilities
                .Include(av => av.Asset)
                .Include(av => av.Vulnerability)
                .Where(av => av.Status == RemediationStatus.Open)
                .OrderByDescending(av => av.DetectedAt)
                .Take(5)
                .ToListAsync()
        };
        return View(stats);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

public class DashboardViewModel
{
    public int TotalAssets { get; set; }
    public int ActiveAssets { get; set; }
    public int TotalVulnerabilities { get; set; }
    public int OpenFindings { get; set; }
    public int CriticalFindings { get; set; }
    public int HighFindings { get; set; }
    public List<Asset> RecentAssets { get; set; } = new();
    public List<AssetVulnerability> RecentFindings { get; set; } = new();
}
