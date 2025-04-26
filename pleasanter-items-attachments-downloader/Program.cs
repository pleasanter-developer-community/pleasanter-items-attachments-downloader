using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Xml.Schema;
using NLog;
using PowerArgs;
using PowerArgs.Samples;
using Spectre.Console;

internal class pleasanter_items_attachments_downloader
{
    private static Logger _logger = LogManager.GetCurrentClassLogger();
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

            await AnsiConsole
                .Progress()
                .Columns(new ProgressColumn[]
                {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn(),
                }).StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("Get Records");

                    await GetRecords(task, arg, await GetSite(nest, arg));
                });

            AnsiConsole.WriteLine("Press any key to exit.");
            _ = Console.ReadLine();
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }
    }

    private static async Task<SiteData> GetSite(int nest, MyArgs arg)
    {
        AnsiConsole.WriteLine($"Get Site Info...");

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

        AnsiConsole.WriteLine($"Get Site Info...Complete[{Omission(siteResponse.Response.Data.Title)}]");

        return siteResponse.Response.Data;
    }


    private static async Task GetRecords(ProgressTask task, MyArgs arg, SiteData site, long offset = 0)
    {
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

        task.MaxValue(recordsResponse.Response.TotalCount);
        task.Value(recordsResponse.Response.Offset);

        await GetRecordsBinaries(0 + 1, arg, site, recordsResponse.Response.Data);

        if (recordsResponse.Response.Data.Any() && recordsResponse.Response.Data.Count() == recordsResponse.Response.PageSize)
        {
            await GetRecords(task, arg, site, offset + recordsResponse.Response.PageSize);
        }
    }

    private static async Task GetRecordsBinaries(int nest, MyArgs arg, SiteData site, List<RecordData> records)
    {
        AnsiConsole.WriteLine($"Get Records Binaries...");

        foreach (var record in records.Select((r, i) => new { r, i }))
        {
            var ratio = record.i / (double)records.Count();

            AnsiConsole.WriteLine($"({ratio:P2})Get Records Binaries...[{Omission(record.r.ItemTitle)}]({record.i:#,##0}/{records.Count():#,##0})");

            await GetBodyBinaries(nest + 1, arg, site, record.r);
            await GetDescriptionBinaries(nest + 1, arg, site, record.r);
            await GetAttachmentsBinaries(nest + 1, arg, site, record.r);
            await GetCommentsBinaries(nest + 1, arg, site, record.r);
        }

        AnsiConsole.WriteLine($"Get Records Binaries...Complete");
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
        AnsiConsole.WriteLine($"Get Description Binaries...");

        if (!record.DescriptionHash.Any())
        {
            //出力すべき添付ファイルがないときは処理しない
            //レコードに対する添付ファイル項目がない
            AnsiConsole.WriteLine($"Get Description Binaries...None");
            return;
        }

        foreach (var description in record.DescriptionHash)
        {
            if (arg.SkipDescription.Any(x => x == description.Key))
            {
                AnsiConsole.WriteLine($"Get Description Binaries...Skip[{description.Key}]");
                continue;
            }

            if (arg.TargetDescription.Any() && !arg.TargetDescription.Any(target => target == description.Key))
            {
                continue;
            }
            else
            {
                AnsiConsole.WriteLine($"Get Description Binaries...Target[{description.Key}]");
            }

            AnsiConsole.WriteLine($"Get Description Binaries...[{description.Key}]");

            await GetBinaries(nest + 1, arg, site, record, BinaryType.Description, description.Key, description.Value);

            AnsiConsole.WriteLine($"Get Description Binaries...Complete[{description.Key}]");
        }

        AnsiConsole.WriteLine($"Get Description Binaries...Complete");
    }

    private static async Task GetCommentsBinaries(int nest, MyArgs arg, SiteData site, RecordData record)
    {
        AnsiConsole.WriteLine($"Get Comments Binaries...");

        if (!record.Comments.Any())
        {
            //コメント項目がないときは処理しない
            AnsiConsole.WriteLine($"Get Comments Binaries...None");
            return;
        }

        if (arg.SkipComments)
        {
            AnsiConsole.WriteLine($"Get Comments Binaries...Skip");
            return;
        }

        if (arg.TargetDescription.Any() && !arg.TargetComments)
        {
            return;
        }
        else
        {
            AnsiConsole.WriteLine($"Get Comments Binaries...Target");
        }

        foreach (var comment in record.Comments)
        {
            AnsiConsole.WriteLine($"Get Comments Binaries...[Comments:{comment.CommentId}]");

            await GetBinaries(nest + 1, arg, site, record, BinaryType.Comments, "Comments", comment.Body, $"{comment.CommentId}");

            AnsiConsole.WriteLine($"Get Comments Binaries...Complete[Comments:{comment.CommentId}]");
        }

        AnsiConsole.WriteLine($"Get Comments Binaries...Complete");
    }

    private static async Task GetBodyBinaries(int nest, MyArgs arg, SiteData site, RecordData record)
    {
        AnsiConsole.WriteLine($"Get Body Binaries...");

        if (arg.SkipBody)
        {
            AnsiConsole.WriteLine($"Get Boby Binaries...Skip");
            return;
        }

        if (arg.TargetDescription.Any() && !arg.TargetBody)
        {
            return;
        }
        else
        {
            AnsiConsole.WriteLine($"Get Boby Binaries...Target");
        }

        await GetBinaries(nest + 1, arg, site, record, BinaryType.Body, "Body", record.Body);

        AnsiConsole.WriteLine($"Get Body Binaries...Complete");
    }

    private static async Task GetBinaries(int nest, MyArgs arg, SiteData site, RecordData record, BinaryType type, string itemLogicName, string body, string specialName = null)
    {
        var matches = Regex.Matches(body ?? "", @"\!\[image\]\((/\S*?)*?/binaries/[a-f0-9]{32}/show\)");

        if (!matches.Any())
        {
            //処理対象が存在しないので処理しない
            AnsiConsole.WriteLine($"Get {type} Binaries...None");
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
        AnsiConsole.WriteLine($"Get Attachments Binaries...");

        if (!record.AttachmentsHash.Any())
        {
            //出力すべき添付ファイルがないときは処理しない
            //レコードに対する添付ファイル項目がない

            AnsiConsole.WriteLine($"Get Attachments Binaries...None");
            return;
        }

        foreach (var attachments in record.AttachmentsHash)
        {
            if (!attachments.Value.Any())
            {
                //出力すべき添付ファイルがないときは処理しない
                //項目があるが添付ファイルがない
                AnsiConsole.WriteLine($"Get Attachments Binaries...None");
                return;
            }

            AnsiConsole.WriteLine($"Get Attachments Binaries...[{attachments.Key}]");

            //取込対象でない場合はスキップする
            if (arg.SkipAttachments.Any(skip => skip == attachments.Key))
            {
                AnsiConsole.WriteLine($"Get Attachments Binaries...Skip[{attachments.Key}]");
                continue;
            }

            if (arg.TargetDescription.Any() && !arg.TargetAttachments.Any(target => target == attachments.Key))
            {
                continue;
            }
            else
            {
                AnsiConsole.WriteLine($"Get Attachments Binaries...Target[{attachments.Key}]");
            }

            foreach (var attachment in attachments.Value)
            {
                await GetBinaryAndSave(nest + 1, arg, site, record, BinaryType.Attachments, attachments.Key, attachment.Guid);
            }
        }

        AnsiConsole.WriteLine($"Get GetAttachments Binaries...Complete");
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

        AnsiConsole.WriteLine($"Get Binary&Save...");

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
                        AnsiConsole.WriteLine($"Get Binary&Save...{respose.StatusCode}");
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

        AnsiConsole.WriteLine($"Get Binary&Save...Complete[{binaryResponse.Id}]");
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
}
