using System.IO;
using System.Text.Json;
using ABI.Windows.Storage.Search;

namespace VKTalker.Models
{
    public class ConfigModel
    {
        public ulong AppId { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }

        public static ConfigModel CreateConfig(string filename)
        {
            var text = File.ReadAllText(filename);
            return JsonSerializer.Deserialize<ConfigModel>(text);
        }
    }
}