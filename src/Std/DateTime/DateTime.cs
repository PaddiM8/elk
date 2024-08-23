using System;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std.DateTime;

[ElkModule("dateTime")]
public static class DateTime
{
    /// <returns>A new DateTime with milliseconds added.</returns>
    [ElkFunction("addMilliseconds")]
    public static RuntimeDateTime AddMilliseconds(RuntimeDateTime dateTime, RuntimeInteger milliseconds)
        => new(dateTime.Value.AddMilliseconds(milliseconds.Value));

    /// <returns>A new DateTime with minutes added.</returns>
    [ElkFunction("addMinutes")]
    public static RuntimeDateTime AddMinutes(RuntimeDateTime dateTime, RuntimeInteger minutes)
        => new(dateTime.Value.AddMinutes(minutes.Value));

    /// <returns>A new DateTime with months added.</returns>
    [ElkFunction("addMonths")]
    public static RuntimeDateTime AddMonths(RuntimeDateTime dateTime, RuntimeInteger months)
        => new(dateTime.Value.AddMonths((int)months.Value));

    /// <returns>A new DateTime with seconds added.</returns>
    [ElkFunction("addSeconds")]
    public static RuntimeDateTime AddSeconds(RuntimeDateTime dateTime, RuntimeInteger seconds)
        => new(dateTime.Value.AddSeconds(seconds.Value));

    /// <returns>A new DateTime with years added.</returns>
    [ElkFunction("addYears")]
    public static RuntimeDateTime AddYears(RuntimeDateTime dateTime, RuntimeInteger years)
        => new(dateTime.Value.AddYears((int)years.Value));

    /// <returns>The day component.</returns>
    [ElkFunction("day")]
    public static RuntimeInteger Day(RuntimeDateTime dateTime)
        => new(dateTime.Value.Day);

    /// <returns>The hour component.</returns>
    [ElkFunction("hour")]
    public static RuntimeInteger Hour(RuntimeDateTime dateTime)
        => new(dateTime.Value.Hour);

    /// <returns>The minute component.</returns>
    [ElkFunction("minute")]
    public static RuntimeInteger Minute(RuntimeDateTime dateTime)
        => new(dateTime.Value.Minute);

    /// <returns>The second component.</returns>
    [ElkFunction("second")]
    public static RuntimeInteger Second(RuntimeDateTime dateTime)
        => new(dateTime.Value.Second);

    /// <returns>The millisecond component.</returns>
    [ElkFunction("millisecond")]
    public static RuntimeInteger Millisecond(RuntimeDateTime dateTime)
        => new(dateTime.Value.Millisecond);

    /// <returns>A string representation of the given DateTime in the given format.</returns>
    /// <example>dateTime::format(dateTime::now(), "yyyy-MM-dd HH:mm:ss")</example>
    [ElkFunction("format")]
    public static RuntimeString Format(RuntimeDateTime dateTime, RuntimeString format)
        => new(dateTime.Value.ToString(format.Value));

    /// <returns>The current time.</returns>
    [ElkFunction("now")]
    public static RuntimeDateTime Now()
        => new(System.DateTime.Now);

    /// <returns>The Unix timestamp of the given DateTime (in seconds).</returns>
    [ElkFunction("timestamp")]
    public static RuntimeInteger Timestamp(RuntimeDateTime dateTime)
        => dateTime.As<RuntimeInteger>();

    /// <returns>The Unix timestamp of the given DateTime (in milliseconds).</returns>
    [ElkFunction("timestampMs")]
    public static RuntimeInteger TimestampMs(RuntimeDateTime dateTime)
        => new(((DateTimeOffset)dateTime.Value).ToUnixTimeMilliseconds());

    /// <returns>The given DateTime converted to UTC.</returns>
    [ElkFunction("toUtc")]
    public static RuntimeDateTime ToUtc(RuntimeDateTime dateTime)
        => new(dateTime.Value.ToUniversalTime());

    /// <returns>The current time in UTC.</returns>
    [ElkFunction("utcNow")]
    public static RuntimeDateTime UtcNow()
        => new(System.DateTime.UtcNow);
}