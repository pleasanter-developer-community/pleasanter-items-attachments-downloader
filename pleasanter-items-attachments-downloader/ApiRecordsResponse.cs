using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

internal class ApiRecordsResponse
{
    public RecordsResponse Response { get; set; } = new RecordsResponse();
}

internal class RecordsResponse
{
    public List<RecordData> Data { get; set; } = new List<RecordData>();
    public long PageSize { get; set; }
    public long TotalCount { get; set; }
    public long Offset { get; set; }
}

internal class RecordData
{
    public long? ResultId { get; set; }
    public long? IssueId { get; set; }
    public long? ReferenceId => ResultId ?? IssueId;
    public string ItemTitle { get; set; }
    public string ItemTitleFormated => Regex.Replace(ItemTitle ?? "", $"[{string.Join("", Path.GetInvalidFileNameChars())}]", "_");
    public Dictionary<string, List<RecordAttachment>> AttachmentsHash { get; set; } = new Dictionary<string, List<RecordAttachment>>();
}

internal class RecordAttachment
{
    public string Guid { get; set; }
    public string Name { get; set; }
    public string NameFormated => Regex.Replace(Name ?? "", $"[{string.Join("", Path.GetInvalidFileNameChars())}]", "_");
    public string Size { get; set; }
    public string HashCode { get; set; }
}
