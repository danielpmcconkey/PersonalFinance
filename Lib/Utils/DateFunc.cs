using NodaTime;

namespace Lib.Utils;

public class DateFunc
{
    private static LocalDateTime _baseLineDate = new LocalDateTime(1970, 1, 1, 0, 0);
    
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

    /// <summary>
    /// Note, this only works at the month level
    /// </summary>
    public static LocalDateTime CalculateAverageDate(LocalDateTime[] dates)
    {
        if (dates == null || dates.Length == 0)
            throw new ArgumentException("dates array cannot be null or empty");

        
        var monthsSinceBaseline = dates.Select(date =>
        {
            var span = (date - _baseLineDate);
            return (span.Years * 12) + span.Months;
        }).ToArray();
        var averageMonths = (int)Math.Round(monthsSinceBaseline.Average());
        return _baseLineDate.PlusMonths(averageMonths);
    }
}