using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Controtex
{
    public class SmartCache
    {
        private static Dictionary<string, SmartCache> Caches { get; } = new Dictionary<string, SmartCache>();
        private Func<string, object> GetData { get; }

        private TimeSpan RefreshTime { get; set; }
        private TimeSpan ClearTime { get; set; }

        private SmartCache(Func<string, object> f, TimeSpan refresh, TimeSpan clear )
        {
            GetData = f;
            RefreshTime = refresh;
            ClearTime = clear;
        }

        public static SmartCache CreateCache(string name, Func<string, object> f, TimeSpan refresh, TimeSpan clear)
        {
            lock (Caches)
            {
                if (!Caches.ContainsKey(name))
                {
                    var cache = new SmartCache(f, refresh, clear);
                    Caches.Add(name, cache);
                }
                else
                {
                    var cache = Caches[name];
                    if (cache.RefreshTime != refresh) cache.RefreshTime = refresh;
                    if (cache.ClearTime != clear) cache.ClearTime = clear;
                }
            }

            return Caches[name];
        }

        private Dictionary<string, CacheObject> Objects { get; } = new Dictionary<string, CacheObject>();

        private CacheObject this[string key]
        {
            get
            {
                bool isLoaded = Objects != null && Objects.ContainsKey(key);
                lock (this)
                {
                    return isLoaded ? Objects[key] : null;
                }
            }
        }

        private CacheObject ObjectCached(string key)
        {
            return this[key];
        }

        private CacheObject SetValue(string key, Object obj)
        {
            var o = ObjectCached(key);
            o.Value = obj;
            o.Loaded = DateTime.Now;
            o.Status = CacheObjectStatus.Loaded;
            return o;
        }

        private void ClearOld()
        {
            lock (this)
            {
                var toRemove = new List<string>();
                foreach (var key in Objects.Keys)
                {
                    var obj = ObjectCached(key);
                    var now = DateTime.Now;
                    if (obj.Loaded + RefreshTime + ClearTime < now)
                    {
                        toRemove.Add(key);
                    }
                }

                foreach (string key in toRemove)
                {
                    Objects.Remove(key);
                }
            }
        }

        public CacheObject Get(string key)
        {
            CacheObject obj;
            lock (this)
            {
                ClearOld();
                if (ObjectCached(key) == null ) // if this is a first call or cache was cleared
                {
                    obj = new CacheObject{Key = key, Loaded = DateTime.Now, Status = CacheObjectStatus.FirstLoading};
                    Objects.Add(key, obj);
                    var retobj = GetData?.Invoke(key);
                    obj = SetValue(key, retobj);
                }
                else
                {
                    obj = ObjectCached(key);
                    if (obj.Status == CacheObjectStatus.FirstLoading) // i.e. next request when cache is not ready, so other thread is loading data
                    {
                        while(ObjectCached(key).Status == CacheObjectStatus.FirstLoading)
                            Thread.Sleep(200);
                        obj = ObjectCached(key);
                    }
                    var now = DateTime.Now;
                    if (obj.Status == CacheObjectStatus.Loaded && 
                        obj.Loaded + RefreshTime >= now) return obj;
                    if (obj.Status == CacheObjectStatus.Loading) return obj;
                    obj.Status = CacheObjectStatus.Loading;
                    Task.Run(() => GetData(key)).ContinueWith((t) =>
                    {
                        lock (this)
                        {
                            SetValue(key, t.Result);
                        }
                    });

                }
            }
            return obj;
        }

    }
}
