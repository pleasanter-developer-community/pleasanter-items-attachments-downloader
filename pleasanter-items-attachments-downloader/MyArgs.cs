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

    //出力対象となる添付ファイル項目
    [ArgRegex("^([A-Z]|00[1-9]|0[1-9][0-9]|100)+?(,([A-Z]|00[1-9]|0[1-9][0-9]|100))*$")]
    public string Target { get; set; } = string.Empty;

    public string[] TargetAttachment => Target.Split(',');
}
