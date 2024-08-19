using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

public class BitbucketDiffFetcher
{
    private readonly string baseUrl;
    private readonly string projectKey;
    private readonly string repositorySlug;
    private readonly string username;
    private readonly string appPassword;
    private readonly HttpClient httpClient;

    public BitbucketDiffFetcher(string baseUrl, string projectKey, string repositorySlug, string username, string appPassword)
    {
        this.baseUrl = baseUrl;
        this.projectKey = projectKey;
        this.repositorySlug = repositorySlug;
        this.username = username;
        this.appPassword = appPassword;
        this.httpClient = new HttpClient();

        var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{appPassword}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
    }

    public async Task<List<string>> GetChangedFilesAsync(string commitId1, string commitId2)
    {
        var url = $"{baseUrl}/rest/api/1.0/projects/{projectKey}/repos/{repositorySlug}/diffstat/{commitId1}..{commitId2}?limit=1000";
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var fileList = new List<string>();

        foreach (var file in jsonDoc.RootElement.GetProperty("values").EnumerateArray())
        {
            var filePath = file.GetProperty("path").GetProperty("toString").GetString();
            fileList.Add(filePath);
        }

        return fileList;
    }

    public async Task DownloadFileAtCommitAsync(string filePath, string commitId, string outputPath)
    {
        var url = $"{baseUrl}/rest/api/1.0/projects/{projectKey}/repos/{repositorySlug}/raw/{filePath}?at={commitId}";
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(outputPath, content);
    }

    public async Task CompareJsonFilesAsync(string filePath, string commitId1, string commitId2, string oldFileOutputPath, string newFileOutputPath)
    {
        await DownloadFileAtCommitAsync(filePath, commitId1, oldFileOutputPath);
        await DownloadFileAtCommitAsync(filePath, commitId2, newFileOutputPath);

        var oldJson = await File.ReadAllTextAsync(oldFileOutputPath);
        var newJson = await File.ReadAllTextAsync(newFileOutputPath);

        var oldJsonDoc = JsonDocument.Parse(oldJson);
        var newJsonDoc = JsonDocument.Parse(newJson);

        // Compare JSON files here (this can be customized as needed)
        var areEqual = oldJsonDoc.RootElement.ToString() == newJsonDoc.RootElement.ToString();
        Console.WriteLine(areEqual ? "Files are identical" : "Files differ");
    }
}
