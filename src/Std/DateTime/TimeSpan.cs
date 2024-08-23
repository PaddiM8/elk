using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Std.DataTypes.DateTime;

namespace Elk.Std.DateTime;

[ElkModule("timeSpan")]
public class TimeSpan
{
    /// <returns>The day component of the interval.</returns>
    [ElkFunction("days")]
    public static RuntimeInteger Days(RuntimeTimeSpan timeSpan)
        => new(timeSpan.Value.Days);

    /// <returns>The hour component of the interval.</returns>
    [ElkFunction("hours")]
    public static RuntimeInteger Hours(RuntimeTimeSpan timeSpan)
        => new(timeSpan.Value.Hours);

    /// <returns>The minute component of the interval.</returns>
    [ElkFunction("minutes")]
    public static RuntimeInteger Minutes(RuntimeTimeSpan timeSpan)
        => new(timeSpan.Value.Minutes);

    /// <returns>The second component of the interval.</returns>
    [ElkFunction("seconds")]
    public static RuntimeInteger Seconds(RuntimeTimeSpan timeSpan)
        => new(timeSpan.Value.Seconds);

    /// <returns>The millisecond component of the interval.</returns>
    [ElkFunction("milliseconds")]
    public static RuntimeInteger Milliseconds(RuntimeTimeSpan timeSpan)
        => new(timeSpan.Value.Milliseconds);

    /// <returns>The total amount of days.</returns>
    [ElkFunction("totalDays")]
    public static RuntimeFloat TotalDays(RuntimeTimeSpan timeSpan)
        => new(timeSpan.Value.TotalDays);

    /// <returns>The total amount of hours.</returns>
    [ElkFunction("totalHours")]
    public static RuntimeFloat TotalHours(RuntimeTimeSpan timeSpan)
        => new(timeSpan.Value.TotalHours);

    /// <returns>The total amount of minutes.</returns>
    [ElkFunction("totalMinutes")]
    public static RuntimeFloat TotalMinutes(RuntimeTimeSpan timeSpan)
        => new(timeSpan.Value.TotalMinutes);

    /// <returns>The total amount of seconds.</returns>
    [ElkFunction("totalSeconds")]
    public static RuntimeFloat TotalSeconds(RuntimeTimeSpan timeSpan)
        => new(timeSpan.Value.TotalSeconds);

    /// <returns>The total amount of milliseconds.</returns>
    [ElkFunction("totalMilliseconds")]
    public static RuntimeFloat TotalMilliseconds(RuntimeTimeSpan timeSpan)
        => new(timeSpan.Value.TotalMilliseconds);
}