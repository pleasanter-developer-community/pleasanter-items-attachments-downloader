using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
}

