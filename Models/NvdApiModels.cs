namespace ATLAS.Models;

// ── NVD CVE API 2.0 response DTOs ──────────────────────────────────────────
// Docs: https://nvd.nist.gov/developers/vulnerabilities

public class NvdResponse
{
    public int ResultsPerPage { get; set; }
    public int StartIndex    { get; set; }
    public int TotalResults  { get; set; }
    public List<NvdVulnWrapper> Vulnerabilities { get; set; } = new();
}

public class NvdVulnWrapper
{
    public NvdCve Cve { get; set; } = new();
}

public class NvdCve
{
    public string Id             { get; set; } = "";
    public string Published      { get; set; } = "";
    public string LastModified   { get; set; } = "";
    public string VulnStatus     { get; set; } = "";
    public List<NvdLangValue>   Descriptions   { get; set; } = new();
    public NvdMetrics           Metrics        { get; set; } = new();
    public List<NvdConfiguration> Configurations { get; set; } = new();
}

public class NvdLangValue
{
    public string Lang  { get; set; } = "";
    public string Value { get; set; } = "";
}

// ── Metrics ─────────────────────────────────────────────────────────────────

public class NvdMetrics
{
    public List<NvdCvssV3Metric> CvssMetricV31 { get; set; } = new();
    public List<NvdCvssV3Metric> CvssMetricV30 { get; set; } = new();
    public List<NvdCvssV2Metric> CvssMetricV2  { get; set; } = new();
}

public class NvdCvssV3Metric
{
    public string      Type     { get; set; } = "";
    public NvdCvssV3Data CvssData { get; set; } = new();
}

public class NvdCvssV3Data
{
    public double BaseScore    { get; set; }
    public string BaseSeverity { get; set; } = "";
}

public class NvdCvssV2Metric
{
    public string      Type     { get; set; } = "";
    public NvdCvssV2Data CvssData { get; set; } = new();
}

public class NvdCvssV2Data
{
    public double BaseScore { get; set; }
}

// ── Configurations / CPE ─────────────────────────────────────────────────────

public class NvdConfiguration
{
    public List<NvdNode> Nodes { get; set; } = new();
}

public class NvdNode
{
    public List<NvdCpeMatch> CpeMatch { get; set; } = new();
    public List<NvdNode>     Children { get; set; } = new();
}

public class NvdCpeMatch
{
    public bool   Vulnerable { get; set; }
    public string Criteria   { get; set; } = "";
}
