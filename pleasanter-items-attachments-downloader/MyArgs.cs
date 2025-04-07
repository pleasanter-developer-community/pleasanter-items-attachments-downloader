using PowerArgs;

internal class MyArgs
{
    //出力対象のパス
    [ArgRequired, ArgExistingDirectory]
    public string Path { get; set; } = string.Empty;

    //プリザンターのベースURL
    [ArgRequired, ArgRegex("^https?://[\\w/:%#\\$&\\?\\(\\)~\\.=\\+\\-]+$")]
    public string Url { get; set; } = string.Empty;

    //APIキー
    [ArgRequired]
    public string ApiKey { get; set; } = string.Empty;

    //対象とするサイトID
    [ArgRequired, ArgRange(1, long.MaxValue)]
    public long SiteId { get; set; }

    //出力から除外する
    [ArgRegex("^((Attachments|Description)([A-Z]|00[1-9]|0[1-9][0-9]|100)|Body|Comments)+?(,((Attachments|Description)([A-Z]|00[1-9]|0[1-9][0-9]|100)|Body|Comments))*$")]
    public string Skip { get; set; } = string.Empty;

    //出力の対象とする
    [ArgRegex("^((Attachments|Description)([A-Z]|00[1-9]|0[1-9][0-9]|100)|Body|Comments)+?(,((Attachments|Description)([A-Z]|00[1-9]|0[1-9][0-9]|100)|Body|Comments))*$")]
    public string Target { get; set; } = string.Empty;

    public List<string> SkipAttachments => Skip.Split(',').Where(x => x.StartsWith("Attachments")).ToList();

    public List<string> SkipDescription => Skip.Split(',').Where(x => x.StartsWith("Description")).ToList();

    public bool SkipBody => Skip.Contains("Body");
    public bool SkipComments => Skip.Contains("Comments");

    public List<string> TargetAttachments => Target.Split(',').Where(x => x.StartsWith("Attachments")).ToList();

    public List<string> TargetDescription => Target.Split(',').Where(x => x.StartsWith("Description")).ToList();

    public bool TargetBody => Target.Contains("Body");
    public bool TargetComments => Target.Contains("Comments");

}
