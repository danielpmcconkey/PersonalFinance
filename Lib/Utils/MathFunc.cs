using NodaTime;

namespace Lib.Utils;

public class MathFunc
{
    
    /// <summary>
    /// Helper for generating a random value in [minValue, maxValue] for supported types.
    /// </summary>
    public static T GenerateRandomBetween<T>(T minValue, T maxValue) where T : IComparable<T>
    {
        // Normalize bounds if they were provided in reverse order.
        if (Comparer<T>.Default.Compare(minValue, maxValue) > 0)
        {
            (minValue, maxValue) = (maxValue, minValue);
        }

        if (typeof(T) == typeof(int))
        {
            var result = MathFunc.GetUnSeededRandomInt((int)(object)minValue, (int)(object)maxValue);
            return (T)(object)result;
        }

        if (typeof(T) == typeof(decimal))
        {
            var result = MathFunc.GetUnSeededRandomDecimal((decimal)(object)minValue, (decimal)(object)maxValue);
            return (T)(object)result;
        }

        if (typeof(T).FullName == "NodaTime.LocalDateTime")
        {
            var result = MathFunc.GetUnSeededRandomDate((NodaTime.LocalDateTime)(object)minValue, (NodaTime.LocalDateTime)(object)maxValue);
            return (T)(object)result;
        }

        throw new NotSupportedException($"Type {typeof(T)} is not supported for random generation in MateNumericProperty.");
    }
    /// <summary>
    ///  Generic inclusive clamp used for ints, decimals, and LocalDateTime.
    /// </summary>
    public static T ClampInclusive<T>(T value, T minValue, T maxValue) where T : IComparable<T>
    {
        // Normalize bounds if they were provided in reverse order.
        if (Comparer<T>.Default.Compare(minValue, maxValue) > 0)
        {
            (minValue, maxValue) = (maxValue, minValue);
        }

        if (Comparer<T>.Default.Compare(value, minValue) < 0) return minValue;
        if (Comparer<T>.Default.Compare(value, maxValue) > 0) return maxValue;
        return value;
    }

    
    public static int GetUnSeededRandomInt(int minInclusive, int maxInclusive)
    {
        var rand = new Random();
        return rand.Next(minInclusive, maxInclusive + 1);
    }
    public static decimal GetUnSeededRandomDecimal(decimal minInclusive, decimal maxInclusive)
    {
        var rand = new Random();
        return (decimal)rand.NextDouble() * (maxInclusive - minInclusive) + minInclusive;
    }
    public static LocalDateTime GetUnSeededRandomDate(LocalDateTime min, LocalDateTime max)
    {
        var span = max - min;
        var totalMonths = span.Months + (12 * span.Years);
        var addlMonthsOverMin = GetUnSeededRandomInt(0, totalMonths);
        var newDate = min.PlusMonths(addlMonthsOverMin);
        return newDate;
    }
}