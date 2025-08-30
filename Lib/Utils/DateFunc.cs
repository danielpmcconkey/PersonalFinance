using NodaTime;

namespace Lib.Utils;

public class DateFunc
{
    
    /// <summary>
    /// reads any LocalDateTime and returns the first of the month closest to it
    /// </summary>
    public static LocalDateTime NormalizeDate(LocalDateTime providedDate)
    {
        var firstOfThisMonth = new LocalDateTime(providedDate.Year, providedDate.Month, 1, 0, 0);
        var firstOfNextMonth = firstOfThisMonth.PlusMonths(1);
        var timeSpanToThisFirst = providedDate - firstOfThisMonth;
        var timeSpanToNextFirst = firstOfNextMonth - providedDate;
        return (timeSpanToThisFirst.Days <= timeSpanToNextFirst.Days) ?
            firstOfThisMonth : // t2 is longer, return this first
            firstOfNextMonth; // t1 is longer than t2, return next first
    }
}