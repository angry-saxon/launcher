using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LauncherConfig
{
    public class ClientConfig
    {
        public string clientVersion { get; set; }
        public string launcherVersion { get; set; }
        public bool replaceFolders { get; set; }
        public ReplaceFolderName[] replaceFolderName { get; set; }
        public string clientFolder { get; set; }
        public string newClientUrl { get; set; }
        public string newConfigUrl { get; set; }
        public string clientExecutable { get; set; }
        // New field for patch notes.
        public string patchNotes { get; set; }

        public static async Task<ClientConfig> LoadFromFileAsync(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                string jsonString = await client.GetStringAsync(url);
                return JsonConvert.DeserializeObject<ClientConfig>(jsonString);
            }
        }
    }

    public class ReplaceFolderName
    {
        public string name { get; set; }
    }
}
