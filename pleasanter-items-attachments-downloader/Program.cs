using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Xml.Schema;
using PowerArgs;

internal class pleasanter_items_attachments_downloader
{
    private static readonly HttpClient _httpClient;

    static pleasanter_items_attachments_downloader()
    {
        _httpClient = new HttpClient();
    }

    private static async Task Main(string[] args)
    {
        try
        {
            var arg = Args.Parse<MyArgs>(args);
            var nest = 0;

            if (!string.IsNullOrEmpty(arg.Target) && !string.IsNullOrEmpty(arg.Skip))
            {
                throw new ArgumentException("\"Skip\" and \"Target\" cannot be specified at the same time.");
            }

            //URLの末尾に/が付けられていた場合は取り除く
            arg.Url = arg.Url.TrimEnd('/');

            await GetRecords(nest + 1, arg, await GetSite(nest, arg));

            WriteLine(nest, "Press any key to exit.");
            _ = Console.ReadLine();
        }
        catch (Exception ex)
        {
            WriteLine(0, ex.Message);
        }
    }

    private static async Task<SiteData> GetSite(int nest, MyArgs arg)
    {
        WriteLine(nest, $"Get Site Info...");

        var respose = await _httpClient.PostAsJsonAsync($"{arg.Url}/api/items/{arg.SiteId}/getsite", new
        {
            ApiVersion = "1.1",
            arg.ApiKey
        });

        if (!respose.IsSuccessStatusCode)
        {
            throw new Exception($"Cannot get Site Info. {respose.StatusCode}");
        }

        var siteResponse = await respose.Content.ReadAsAsync<ApiSiteResponse>();

        WriteLine(nest, $"Get Site Info...Complete[{Omission(siteResponse.Response.Data.Title)}]");

        return siteResponse.Response.Data;
    }


    private static async Task GetRecords(int nest, MyArgs arg, SiteData site, long offset = 0)
    {
        if (offset == 0)
        {
            WriteLine(nest, "Get Record List...");
        }

        var respose = await _httpClient.PostAsJsonAsync($"{arg.Url}/api/items/{arg.SiteId}/get", new
        {
            ApiVersion = "1.1",
            arg.ApiKey,
            Offset = offset,
            View = new
            {
                ColumnSorterHash = new { CreatedTime = "asc" }//作成日昇順で取得することで繰り返し操作の一貫性を担保する

            }
        });

        if (!respose.IsSuccessStatusCode)
        {
            throw new Exception($"Can not get Record List. {respose.StatusCode}");
        }

        var recordsResponse = await respose.Content.ReadAsAsync<ApiRecordsResponse>();

        WriteLine(nest + 1, $"({recordsResponse.Response.Offset / (double)recordsResponse.Response.TotalCount:P2})Get Record List...({recordsResponse.Response.Offset:#,##0}/{recordsResponse.Response.TotalCount:#,##0})");

        await GetRecordsBinaries(nest + 1, arg, site, recordsResponse.Response.Data);

        if (recordsResponse.Response.Data.Any() && recordsResponse.Response.Data.Count() == recordsResponse.Response.PageSize)
        {
            await GetRecords(nest + 1, arg, site, offset + recordsResponse.Response.PageSize);
        }

        WriteLine(nest, "Get Record List...Complete");
    }

    private static async Task GetRecordsBinaries(int nest, MyArgs arg, SiteData site, List<RecordData> records)
    {
        WriteLine(nest, $"Get Records Binaries...");

        foreach (var record in records.Select((r, i) => new { r, i }))
        {
            var ratio = record.i / (double)records.Count();

            WriteLine(nest + 1, $"({ratio:P2})Get Records Binaries...[{Omission(record.r.ItemTitle)}]({record.i:#,##0}/{records.Count():#,##0})");

            await GetBodyBinaries(nest + 1, arg, site, record.r);
            await GetDescriptionBinaries(nest + 1, arg, site, record.r);
            await GetAttachmentsBinaries(nest + 1, arg, site, record.r);
            await GetCommentsBinaries(nest + 1, arg, site, record.r);
        }

        WriteLine(nest, $"Get Records Binaries...Complete");
    }

    enum BinaryType
    {
        Attachments,
        Body,
        Comments,
        Description,
    }

    private static async Task GetDescriptionBinaries(int nest, MyArgs arg, SiteData site, RecordData record)
    {
        WriteLine(nest, $"Get Description Binaries...");

        if (!record.DescriptionHash.Any())
        {
            //出力すべき添付ファイルがないときは処理しない
            //レコードに対する添付ファイル項目がない
            WriteLine(nest, $"Get Description Binaries...None");
            return;
        }

        foreach (var description in record.DescriptionHash)
        {
            if (arg.SkipDescription.Any(x => x == description.Key))
            {
                WriteLine(nest + 1, $"Get Description Binaries...Skip[{description.Key}]");
                continue;
            }

            if (arg.TargetDescription.Any() && !arg.TargetDescription.Any(target => target == description.Key))
            {
                continue;
            }
            else
            {
                WriteLine(nest + 1, $"Get Description Binaries...Target[{description.Key}]");
            }

            WriteLine(nest + 1, $"Get Description Binaries...[{description.Key}]");

            await GetBinaries(nest + 1, arg, site, record, BinaryType.Description, description.Key, description.Value);

            WriteLine(nest + 1, $"Get Description Binaries...Complete[{description.Key}]");
        }

        WriteLine(nest, $"Get Description Binaries...Complete");
    }

    private static async Task GetCommentsBinaries(int nest, MyArgs arg, SiteData site, RecordData record)
    {
        WriteLine(nest, $"Get Comments Binaries...");

        if (!record.Comments.Any())
        {
            //コメント項目がないときは処理しない
            WriteLine(nest, $"Get Comments Binaries...None");
            return;
        }

        if (arg.SkipComments)
        {
            WriteLine(nest + 1, $"Get Comments Binaries...Skip");
            return;
        }

        if (arg.TargetDescription.Any() && !arg.TargetComments)
        {
            return;
        }
        else
        {
            WriteLine(nest + 1, $"Get Comments Binaries...Target");
        }

        foreach (var comment in record.Comments)
        {
            WriteLine(nest + 1, $"Get Comments Binaries...[Comments:{comment.CommentId}]");

            await GetBinaries(nest + 1, arg, site, record, BinaryType.Comments, "Comments", comment.Body, $"{comment.CommentId}");

            WriteLine(nest + 1, $"Get Comments Binaries...Complete[Comments:{comment.CommentId}]");
        }

        WriteLine(nest, $"Get Comments Binaries...Complete");
    }

    private static async Task GetBodyBinaries(int nest, MyArgs arg, SiteData site, RecordData record)
    {
        WriteLine(nest, $"Get Body Binaries...");

        if (arg.SkipBody)
        {
            WriteLine(nest + 1, $"Get Boby Binaries...Skip");
            return;
        }

        if (arg.TargetDescription.Any() && !arg.TargetBody)
        {
            return;
        }
        else
        {
            WriteLine(nest + 1, $"Get Boby Binaries...Target");
        }

        await GetBinaries(nest + 1, arg, site, record, BinaryType.Body, "Body", record.Body);

        WriteLine(nest, $"Get Body Binaries...Complete");
    }

    private static async Task GetBinaries(int nest, MyArgs arg, SiteData site, RecordData record, BinaryType type, string itemLogicName, string body, string specialName = null)
    {
        var matches = Regex.Matches(body ?? "", @"\!\[image\]\((/\S+)*/binaries/[a-f0-9]{32}/show\)");

        if (!matches.Any())
        {
            //処理対象が存在しないので処理しない
            WriteLine(nest, $"Get {type} Binaries...None");
            return;
        }

        foreach (Match match in matches)
        {
            var guid = Regex.Match(match.Value, "[a-f0-9]{32}").Value;

            await GetBinaryAndSave(nest + 1, arg, site, record, type, itemLogicName, guid, specialName);
        }
    }

    private static async Task GetAttachmentsBinaries(int nest, MyArgs arg, SiteData site, RecordData record)
    {
        WriteLine(nest, $"Get Attachments Binaries...");

        if (!record.AttachmentsHash.Any())
        {
            //出力すべき添付ファイルがないときは処理しない
            //レコードに対する添付ファイル項目がない

            WriteLine(nest, $"Get Attachments Binaries...None");
            return;
        }

        foreach (var attachments in record.AttachmentsHash)
        {
            if (!attachments.Value.Any())
            {
                //出力すべき添付ファイルがないときは処理しない
                //項目があるが添付ファイルがない
                WriteLine(nest, $"Get Attachments Binaries...None");
                return;
            }

            WriteLine(nest + 1, $"Get Attachments Binaries...[{attachments.Key}]");

            //取込対象でない場合はスキップする
            if (arg.SkipAttachments.Any(skip => skip == attachments.Key))
            {
                WriteLine(nest + 1, $"Get Attachments Binaries...Skip[{attachments.Key}]");
                continue;
            }

            if (arg.TargetDescription.Any() && !arg.TargetAttachments.Any(target => target == attachments.Key))
            {
                continue;
            }
            else
            {
                WriteLine(nest + 1, $"Get Attachments Binaries...Target[{attachments.Key}]");
            }

            foreach (var attachment in attachments.Value)
            {
                await GetBinaryAndSave(nest + 1, arg, site, record, BinaryType.Attachments, attachments.Key, attachment.Guid);
            }
        }

        WriteLine(nest, $"Get GetAttachments Binaries...Complete");
    }

    /// <summary>
    /// GUIDを指定して指定パスにファイルをダウンロードしてくる
    /// </summary>
    /// <param name="arg"></param>
    /// <param name="site"></param>
    /// <param name="record"></param>
    /// <param name="type"></param>
    /// <param name="itemLogicName"></param>
    /// <param name="guid"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private static async Task GetBinaryAndSave(int nest, MyArgs arg, SiteData site, RecordData record, BinaryType type, string itemLogicName, string guid, string specialName = null)
    {
        var itemPhysicName = site.SiteSettings.Columns.Where(column => column.ColumnName == itemLogicName).FirstOrDefault()?.LabelText;
        var path = Path.Combine(arg.Path, $"[{arg.SiteId}]{site.TitleFormated}", $"[{record.ReferenceId}]{record.ItemTitleFormated}", $"[{itemLogicName}]{itemPhysicName}");

        WriteLine(nest, $"Get Binary&Save...");

        var respose = await _httpClient.PostAsJsonAsync($"{arg.Url}/api/binaries/{guid}/get", new
        {
            ApiVersion = "1.1",
            arg.ApiKey
        });

        if (!respose.IsSuccessStatusCode)
        {
            switch (respose.StatusCode)
            {
                default:
                    {
                        throw new Exception();
                    }
                case HttpStatusCode.NotFound:
                    {
                        WriteLine(nest, $"Get Binary&Save...{respose.StatusCode}");
                        return;
                    }
            }
        }

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        var binaryResponse = await respose.Content.ReadAsAsync<ApiBinaryResponse>();

        var fileName = string.Join("", new[] {
            $"[{binaryResponse.Id}]",
           string.IsNullOrEmpty(specialName) ? string.Empty : $"[{specialName}]",
            $"{binaryResponse.Response.FileNameFormated}"
        });

        File.WriteAllBytes(Path.Combine(path, fileName), binaryResponse.Response.Binaries);

        WriteLine(nest, $"Get Binary&Save...Complete[{binaryResponse.Id}]");
    }

    private static string Omission(string str)
    {
        if (str == null)
        {
            return "";
        }
        if (str.Length <= 15)
        {
            return str;
        }
        return $"{str.Substring(0, 12)}...";
    }

    private static void WriteLine(int nest, string message) => Console.WriteLine($"{string.Join("", Enumerable.Range(0, nest).Select(n => "  "/*半角SPx2*/))}{message}");
}
