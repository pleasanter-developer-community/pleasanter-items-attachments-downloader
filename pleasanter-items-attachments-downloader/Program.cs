using System.Threading.Tasks;
using PowerArgs;

class pleasanter_items_attachments_downloader
{
    private static readonly HttpClient _httpClient;

    static pleasanter_items_attachments_downloader()
    {
        _httpClient = new HttpClient();
    }

    static async Task Main(string[] args)
    {
        try
        {
            var arg = Args.Parse<MyArgs>(args);

            //URLの末尾に/が付けられていた場合は取り除く
            arg.Url = arg.Url.TrimEnd('/');
            //サイト情報
            var site = await GetSite(arg);
            //一覧情報
            var records = await GetRecords(arg);
            //添付ファイルのダウンロード
            await GetBinaries(arg, site, records);

            Console.WriteLine("Press any key to exit.");
            _ = Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    static async Task<SiteData> GetSite(MyArgs arg)
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


    static async Task<List<RecordData>> GetRecords(MyArgs arg, long offset = 0)
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

        Console.WriteLine($"Get Record List...{recordsResponse.Response.Offset:##,#}/{recordsResponse.Response.TotalCount:##,#}({recordsResponse.Response.Offset / (double)recordsResponse.Response.TotalCount:P2})");

        var records = recordsResponse.Response.Data;

        if (recordsResponse.Response.Data.Any() && recordsResponse.Response.Data.Count() == recordsResponse.Response.PageSize)
        {
            records.AddRange((await GetRecords(arg, offset + recordsResponse.Response.PageSize)));
        }

        Console.WriteLine("Get Record List...Complete");

        return records;
    }

    static async Task GetBinaries(MyArgs arg, SiteData site, List<RecordData> records)
    {
        Console.WriteLine("Get Binary...");

        foreach (var record in records.Select((r, i) => new { r, i }))
        {
            if (record.i % 20 == 0)
            {
                Console.WriteLine($"Get Record List...{record.i:##,#}/{records.Count():##,#}({record.i / (double)records.Count():P2})");
            }

            await GetBinaries(arg, site, record.r);
        }

        Console.WriteLine("Get Binary...Complete");
    }

    static async Task GetBinaries(MyArgs arg, SiteData site, RecordData record)
    {
        foreach (var attachments in record.AttachmentsHash)
        {
            //添付ファイル項目に別名が付けられている場合は取得する
            var labelText = site.SiteSettings.Columns.Where(column => column.ColumnName == attachments.Key).FirstOrDefault()?.LabelTextFormated;

            Console.WriteLine($"Get Binary...[{record.ReferenceId}]{record.ItemTitleFormated}/[{attachments.Key}]{labelText}");

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
                    throw new Exception($"Can not get Binary. {respose.StatusCode}");
                }

                var binaryResponse = await respose.Content.ReadAsAsync<ApiBinaryResponse>();


                Console.WriteLine($"Get Binary...[{record.ReferenceId}]{record.ItemTitleFormated}/[{attachments.Key}]{labelText}/[{binaryResponse.Id}]{binaryResponse.Response.FileNameFormated}");

                var fileName = Path.Combine(path, $"[{binaryResponse.Id}]{binaryResponse.Response.FileNameFormated}");
                byte[] bytes = Convert.FromBase64String(binaryResponse.Response.Base64);
                File.WriteAllBytes(fileName, bytes);//ファイルが既に存在する場合は上書きされる


                Console.WriteLine($"Get Binary...[{record.ReferenceId}]{record.ItemTitleFormated}/[{attachments.Key}]{labelText}/[{binaryResponse.Id}]{binaryResponse.Response.FileNameFormated}...Complete");
            }

            //ディレクトリを作ったもの空だった場合は削除
            //ここまでの心配はたぶん必要ない
            Directory.Delete(path, false);

            Console.WriteLine($"Get Binary...[{record.ReferenceId}]{record.ItemTitleFormated}/[{attachments.Key}]{labelText}/...Complete");

        }
    }
}
