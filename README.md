## Pleasanter Bulk Attachment Downloader

This tool, developed in C#, simplifies the process of bulk downloading attachments from your Pleasanter instance. It addresses the challenges of accessing attached files, whether they are stored in the database (requiring BASE64 string manipulation) or locally (where filenames are GUIDs and database lookups are necessary).

By allowing downloads at the site level with just the site ID, this tool streamlines the entire process. It is compatible with environments running .NET 8, including Windows, MacOS, and Linux, and has been tested to work with Pleasanter version 1.4.* and later.

### Features

* **Site-Level Download:** Download all attachments associated with a specific Pleasanter site.
* **Handles Both Storage Types:** Works with attachments stored directly in the database (as BASE64) and those stored locally (with GUID filenames).
* **Cross-Platform:** Runs on Windows, MacOS, and Linux environments with .NET 8 installed.
* **Pleasanter Compatibility:** Tested with Pleasanter version 1.4.* and later.
* **Command-Line Interface:** Operated through simple command-line arguments.
* **Organized Output:** Downloads are structured in a directory format for easy management.
* **Optional Target Fields:** Allows you to specify which attachment fields to include in the download.

### Usage

The tool is executed via the command line. Ensure that .NET 8 is installed on your system.

```bash
dotnet pleasanter-items-attachments-downloader.dll /Url:{PLEASANTER_URL} /SiteId:{SITE_ID} /ApiKey:{API_KEY} /Path:{OUTPUT_PATH} [/Target:{TARGET_FIELDS}] [/Skip:{SKIP_FIELDS}]
```

#### Parameters

* /Url:{PLEASANTER_URL}: The URL of your Pleasanter instance.
* /SiteId:{SITE_ID}: The ID of the Pleasanter site to download attachments from.
* /ApiKey:{API_KEY}: Your Pleasanter API key with necessary read permissions.
* /Path:{OUTPUT_PATH}: The local directory where the downloaded attachments will be saved.
* /Target:{TARGET_FIELDS} (Optional): A comma-separated list of the physical names of attachment fields to specifically download (e.g., AttachmentsA,DescriptionA).
* /Skip:{TARGET_FIELDS} (Optional): A comma-separated list of the physical names of attachment fields to skip download (e.g., AttachmentsA,DescriptionA).

#### Example

```Bash
dotnet pleasanter-items-attachments-downloader.dll /Url:[https://your-pleasanter.com](https://your-pleasanter.com) /SiteId:123 /ApiKey:your_secret_api_key /Path:./downloads /Target:AttachmentsA,DescriptionA
```
### License
This tool is released under the MIT License.

### Note
While this tool simplifies the download process, please be aware that the exception handling is basic and may halt subsequent processes upon encountering an error.
