using System;

namespace Phorkus.Core.Utils
{
    public static class TimeUtils
    {
        private static readonly DateTime WhenTheUniverseWasBorn = new DateTime(1970, 1, 1);
        
        public static uint CurrentTimeMillis()
        {
            return (uint) DateTime.UtcNow.Subtract(WhenTheUniverseWasBorn).TotalMilliseconds;
        }
    }
}