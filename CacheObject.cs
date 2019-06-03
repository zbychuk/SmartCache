using System;

namespace Controtex
{
    public class CacheObject {
        public object Key { get; set; }
        public DateTime Loaded { get; set; }
        public CacheObjectStatus Status { get; set; }
        public Object Value { get; set; }
    }
}