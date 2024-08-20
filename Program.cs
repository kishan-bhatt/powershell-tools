using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

class Program
{
    private static readonly string baseUrl = "https://your-bitbucket-server-url/rest/api/1.0/";
    private static readonly string repositorySlug = "your-repository-slug";
    private static readonly string projectKey = "your-project-key";
    private static readonly string username = "your-username";
    private static readonly string password = "your-password";

    static async Task Main(string[] args)
    {
        string commitId1 = "commit-id-1";
        string commitId2 = "commit-id-2";

        var changedFiles = await GetChangedFiles(commitId1, commitId2);

        foreach (var file in changedFiles)
        {
            if (file.EndsWith(".json"))
            {
                var oldFileContent = await GetFileContent(commitId1, file);
                var newFileContent = await GetFileContent(commitId2, file);

                CompareJsonFiles(oldFileContent, newFileContent);
            }
        }
    }

    static async Task<List<string>> GetChangedFiles(string commitId1, string commitId2)
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", 
                Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{password}")));

            var url = $"{baseUrl}projects/{projectKey}/repos/{repositorySlug}/compare/diff?from={commitId1}&to={commitId2}";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var diffJson = JObject.Parse(content);

            var changedFiles = new List<string>();

            foreach (var diff in diffJson["diffs"])
            {
                string filePath = diff["to"]["path"].ToString();
                changedFiles.Add(filePath);
            }

            return changedFiles;
        }
    }

    static async Task<string> GetFileContent(string commitId, string filePath)
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", 
                Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{password}")));

            var url = $"{baseUrl}projects/{projectKey}/repos/{repositorySlug}/browse/{filePath}?at={commitId}";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var fileContent = JObject.Parse(content)["lines"];

            return string.Join(Environment.NewLine, fileContent);
        }
    }

    static void CompareJsonFiles(string oldContent, string newContent)
    {
        var oldJson = JObject.Parse(oldContent);
        var newJson = JObject.Parse(newContent);

        var differences = new List<string>();

        foreach (var property in oldJson.Properties())
        {
            if (!JToken.DeepEquals(property.Value, newJson[property.Name]))
            {
                differences.Add($"Property '{property.Name}' changed from '{property.Value}' to '{newJson[property.Name]}'");
            }
        }

        foreach (var property in newJson.Properties())
        {
            if (oldJson[property.Name] == null)
            {
                differences.Add($"Property '{property.Name}' added with value '{property.Value}'");
            }
        }

        if (differences.Count == 0)
        {
            Console.WriteLine("No differences found.");
        }
        else
        {
            Console.WriteLine("Differences:");
            foreach (var difference in differences)
            {
                Console.WriteLine(difference);
            }
        }
    }
}
