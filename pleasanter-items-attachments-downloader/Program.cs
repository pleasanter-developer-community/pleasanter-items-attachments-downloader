using System.Net;
using System.Net.Mail;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        var arg = Args.Parse<MyArgs>(args);

        //URLの末尾に/が付けられていた場合は取り除く
        arg.Url = arg.Url.TrimEnd('/');

        //まずは現状のサイト設定を呼び出してくる
        var site = await GetSite(arg);

        if (!site.Item1 || site.Item2 == default || site.Item3 == default)
        {
            //処理すべき対象がないので打ち切る
            Console.WriteLine("サイトから情報が取得出来ません。接続情報やAPIキー、サイトに添付ファイル項目が含まれるか確認して下さい。");
            return;
        }

        //ファイル名に使用できない文字が含まれていた場合は置換する
        site.Item2 = string.Concat(site.Item2.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

        var sitePath = Path.Combine(arg.Path, $"{arg.SiteId}_{site.Item2}");

        if (!Directory.Exists(sitePath))
        {
            Directory.CreateDirectory(sitePath);
        }


        await GetItems(arg, site.Item3);
    }


    static async Task GetItems(MyArgs arg, List<KeyValuePair<string, string>> columns, long offset = 0)
    {
        var respose = await _httpClient.PostAsJsonAsync($"{arg.Url}/api/items/{arg.SiteId}/get", new
        {
            ApiVersion = "1.1",
            arg.ApiKey,
            Offset = offset,
            View = new
            {
                ApiDataType = "KeyValues",
                GridColumns = columns.Select(x => x.Key).ToArray(),
                ColumnSorterHash = new
                {
                    CreatedTime = "asc"
                }
            }
        });

        var json = await respose.Content.ReadAsStringAsync();
        var items = JObject.Parse(json);

        var count = ((JArray)items.SelectToken("Response.Data")).Count();//取得された要素数
        var pageSize = (long)items.SelectToken("Response.PageSize");

        if (count == pageSize)
        {
            await GetItems(arg, columns, offset + pageSize);
        }

         ((JArray)items.SelectToken("Response.Data")).Select(data => ((JArray)data).Select(attachment => new
         {

         }));
    }

    static async Task<(bool, string, List<KeyValuePair<string, string>>)> GetSite(MyArgs arg)
    {
        var respose = await _httpClient.PostAsJsonAsync($"{arg.Url}/api/items/{arg.SiteId}/getsite", new
        {
            ApiVersion = "1.1",
            arg.ApiKey
        });

        if (!respose.IsSuccessStatusCode)
        {
            return (false, default, default);
        }

        var json = await respose.Content.ReadAsStringAsync();
        var site = JObject.Parse(json);

        //サイト名を取得する
        var title = (string)site.SelectToken("Response.Data.Title");

        //一覧画面の設定を取得してくる
        var gridColumns = ((JArray)site.SelectToken("Response.Data.SiteSettings.GridColumns"))
            .Select(col => ((string)col))
            .Where(col => col?.StartsWith("Attachments") == true)
            .Select(col => new
            {
                ColumnName = col
            });

        //編集画面の設定を取得してくる
        var editorColumn = ((JArray)site.SelectToken("Response.Data.SiteSettings.EditorColumnHash.General"))
            .Select(col => ((string)col))
            .Where(col => col?.StartsWith("Attachments") == true)
            .Select(col => new
            {
                ColumnName = col
            });

        //一覧画面も編集画面も空でない場合
        if (gridColumns?.Any() == true && editorColumn?.Any() == true)
        {
            gridColumns = gridColumns.Union(editorColumn);
        }

        //一覧画面は空で編集画面は空でない場合
        if (gridColumns?.Any() != true && editorColumn?.Any() == true)
        {
            gridColumns = editorColumn;
        }

        if (gridColumns?.Any() != true)
        {
            //処理すべき対象がない
            return (true, title, default);
        }

        //カラムの別名を取得してくる
        var columns = ((JArray)site?.SelectToken("Response.Data.SiteSettings.Columns"))
            .Select(col => (JObject)col)
            .Where(col => ((string)col?["ColumnName"])?.StartsWith("Attachments") == true)
            .Select(col => new
            {
                ColumnName = (string)col?["ColumnName"],
                LabelText = (string)col?["LabelText"],
            });

        if (columns?.Any() != true)
        {
            //結合すべき対象が存在しないので、そのままくっつけて返してしまう
            return (true, title, gridColumns.Select(x => new KeyValuePair<string, string>(x.ColumnName, x.ColumnName)).ToList());
        }

        //取得した項目情報に対してLabelTextをくっつける
        var joinedGridColumns = gridColumns
            .GroupJoin(
                columns,
                gridColumns => gridColumns.ColumnName,
                columns => columns.ColumnName,
                (gridColumns, columns) => new
                {
                    gridColumns.ColumnName,
                    LabelText = columns?.FirstOrDefault()?.LabelText ?? gridColumns.ColumnName,
                });

        if (joinedGridColumns?.Any() == true)
        {
            return (true, title, joinedGridColumns.Select(x => new KeyValuePair<string, string>(x.ColumnName, x.LabelText)).ToList());
        }

        return (true, title, default);
    }
}
