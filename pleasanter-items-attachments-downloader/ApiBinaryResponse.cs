using System.Text.RegularExpressions;

internal class ApiBinaryResponse
{
    public long Id { get; set; }
    public BinaryResponse Response { get; set; } = new BinaryResponse();
}

internal class BinaryResponse
{
    public string Base64 { get; set; }
    public string FileName { get; set; }
    public string FileNameFormated => Regex.Replace(FileName ?? "", $"[{string.Join("", Path.GetInvalidFileNameChars())}]", "_");
    public byte[] Binaries => Convert.FromBase64String(Base64 ?? "");
}

