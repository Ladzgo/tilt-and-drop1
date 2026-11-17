using System;

namespace TiltDrop.Core
{
    public interface IClock
    {
        DateTime UtcNow { get; }
        DateTime LocalNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime LocalNow => DateTime.Now;
    }

    public sealed class FakeClock : IClock
    {
        public DateTime Utc;
        public TimeSpan Offset = TimeSpan.FromHours(1);
        public FakeClock(DateTime utc) { Utc = utc; }
        public DateTime UtcNow => Utc;
        public DateTime LocalNow => Utc + Offset;
        public void Advance(TimeSpan t) => Utc += t;
    }

    public static class Days
    {
        /// <summary>Calendar day number (0 = Monday 0001-01-01). Week index = day / 7 (Monday start).</summary>
        public static int Of(DateTime d) => (int)(d.Date.Ticks / TimeSpan.TicksPerDay);
        public static DateTime ToDate(int day) => new DateTime(day * TimeSpan.TicksPerDay);
        public static int Parse(string yyyyMmDd) => Of(DateTime.ParseExact(yyyyMmDd, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    }
}
