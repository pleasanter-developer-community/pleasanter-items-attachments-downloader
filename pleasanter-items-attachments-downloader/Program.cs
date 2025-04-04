﻿using PowerArgs;

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

            //URLの末尾に/が付けられていた場合は取り除く
            arg.Url = arg.Url.TrimEnd('/');

            await GetRecords(arg, await GetSite(arg));

            Console.WriteLine("Press any key to exit.");
            _ = Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private static async Task<SiteData> GetSite(MyArgs arg)
    {
        Console.WriteLine("Get Site Info...");

        var respose = await _httpClient.PostAsJsonAsync($"{arg.Url}/api/items/{arg.SiteId}/getsite", new
        {
            ApiVersion = "1.1",
            arg.ApiKey
        });

        if (!respose.IsSuccessStatusCode)
        {
            throw new Exception($"Can not get Site Info. {respose.StatusCode}");
        }

        var siteResponse = await respose.Content.ReadAsAsync<ApiSiteResponse>();

        Console.WriteLine("Get Site Info...Complete");

        return siteResponse.Response.Data;
    }


    private static async Task GetRecords(MyArgs arg, SiteData site, long offset = 0)
    {
        if (offset == 0)
        {
            Console.WriteLine("Get Record List...");
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

        Console.WriteLine($"({recordsResponse.Response.Offset / (double)recordsResponse.Response.TotalCount:P2})Get Record List...{recordsResponse.Response.Offset:#,##0}/{recordsResponse.Response.TotalCount:#,##0}");

        await GetBinaries(arg, site, recordsResponse.Response.Data);

        if (recordsResponse.Response.Data.Any() && recordsResponse.Response.Data.Count() == recordsResponse.Response.PageSize)
        {
            await GetRecords(arg, site, offset + recordsResponse.Response.PageSize);
        }

        Console.WriteLine("Get Record List...Complete");
    }

    private static async Task GetBinaries(MyArgs arg, SiteData site, List<RecordData> records)
    {
        Console.WriteLine($"\tGet Binaries...Complete");

        foreach (var record in records.Select((r, i) => new { r, i }))
        {
            var ratio = record.i / (double)records.Count();

            if ((int)(ratio * 100) % 10 == 0)
            {
                Console.WriteLine($"\t({ratio:P2})Get Binaries...{record.i:#,##0}/{records.Count():#,##0}");
            }

            await GetBinaries(arg, site, record.r);
        }

        Console.WriteLine($"\tGet Binaries...Complete");
    }

    private static async Task GetBinaries(MyArgs arg, SiteData site, RecordData record)
    {
        if (!record.AttachmentsHash.Any())
        {
            //出力すべき添付ファイルがないときは処理しない
            //レコードに対する添付ファイル項目がない
            return;
        }

        foreach (var attachments in record.AttachmentsHash)
        {
            if (!attachments.Value.Any())
            {
                //出力すべき添付ファイルがないときは処理しない
                //項目があるが添付ファイルがない
                return;
            }

            //取込対象でない場合はスキップする
            if (arg.TargetAttachment.Any(target => $"Attachments{target}" == attachments.Key))
            {
                Console.WriteLine($"\t\tGet Binary...Skip {attachments.Key}");

                continue;
            }

            //添付ファイル項目に別名が付けられている場合は取得する
            var labelText = site.SiteSettings.Columns.Where(column => column.ColumnName == attachments.Key).FirstOrDefault()?.LabelTextFormated;

            Console.WriteLine($"\t\tGet Binary...[{record.ReferenceId}]{Omission(record.ItemTitleFormated)}/[{attachments.Key}]{Omission(labelText)}");

            var path = Path.Combine(
                arg.Path,
                $"[{arg.SiteId}]{site.TitleFormated}",
                $"[{record.ReferenceId}]{record.ItemTitleFormated}",
                $"[{attachments.Key}]{labelText}"
            );

            //書き出し先のフォルダを作る
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (var attachment in attachments.Value)
            {
                var respose = await _httpClient.PostAsJsonAsync($"{arg.Url}/api/binaries/{attachment.Guid}/get", new
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
                                throw new Exception($"\t\t\tCan not get Binary. {respose.StatusCode}");
                            }
                        case System.Net.HttpStatusCode.NotFound:
                            {
                                Console.WriteLine($"\t\t\tGet Binary...{respose.StatusCode}");
                                continue;
                            }
                    }
                }

                var binaryResponse = await respose.Content.ReadAsAsync<ApiBinaryResponse>();


                Console.WriteLine($"\t\t\tGet Binary...[{binaryResponse.Id}]{Omission(binaryResponse.Response.FileNameFormated)}");

                var fileName = Path.Combine(path, $"[{binaryResponse.Id}]{binaryResponse.Response.FileNameFormated}");
                var bytes = Convert.FromBase64String(binaryResponse.Response.Base64);
                File.WriteAllBytes(fileName, bytes);//ファイルが既に存在する場合は上書きされる


                Console.WriteLine($"\t\t\tGet Binary...[{binaryResponse.Id}]{Omission(binaryResponse.Response.FileNameFormated)}...Complete");
            }

            Console.WriteLine($"\t\tGet Binary...[{record.ReferenceId}]{Omission(record.ItemTitleFormated)}/[{attachments.Key}]{Omission(labelText)}...Complete");

        }
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
