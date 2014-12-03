using System.Collections;
using System.Collections.Generic;

namespace MovieSchedule.Core.Extensions
{
    public static class DictionaryExtension
    {
        public static void AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> map, TKey key, TValue value) where TValue : IList, new()
        {
            if (!map.ContainsKey(key) || map[key] == null)
            {
                map[key] = new TValue();
                foreach (var v in value)
                {
                    map[key].Add(v);
                }
            }
            else
            {
                foreach (var v in value)
                {
                    map[key].Add(v);
                }
            }

        }
    }
}