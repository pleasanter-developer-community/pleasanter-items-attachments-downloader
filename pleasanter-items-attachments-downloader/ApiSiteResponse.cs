using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

internal class ApiSiteResponse
{
    public SiteResponse Response { get; set; } = new SiteResponse();
}

internal class SiteResponse
{
    public SiteData Data { get; set; } = new SiteData();
}

internal class SiteData
{
    public string Title { get; set; }
    public string TitleFormated => Regex.Replace(Title ?? "", $"[{string.Join("", Path.GetInvalidFileNameChars())}]", "_");
    public SiteSiteSettings SiteSettings { get; set; } = new SiteSiteSettings();
}

internal class SiteSiteSettings
{
    public List<SiteColumn> Columns { get; set; } = new List<SiteColumn>();
}

internal class SiteColumn
{
    public string ColumnName { get; set; }
    public string LabelText { get; set; }
    public string LabelTextFormated => Regex.Replace(LabelText ?? "", $"[{string.Join("", Path.GetInvalidFileNameChars())}]", "_");
}
