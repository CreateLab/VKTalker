using System.Collections.Concurrent;

namespace VKTalker.Models
{
    public static class GlobalImageDictionary
    {
       private  static ConcurrentDictionary<string,string> _dictionary = new ConcurrentDictionary<string, string>();
       public static string Get(string s)
       {
           return _dictionary.TryGetValue(s, out var name) ? name : null;
       }


       public static void AddOrUpdate(string key, string value)
       {
            _dictionary.AddOrUpdate(key, value, (key, oldValue) => value);
       }
    }
}