using Lib.DataTypes;
using Lib.Utils;
using NodaTime;

namespace Lib.MonteCarlo;

public interface INudgeHandler<T> where T : IComparable<T>
{
    T GenerateNudge(T minValue, T maxValue, T parentAValue, T parentBValue);
    bool IsDifferentEnough(T value1, T value2);
    T GetHalfwayPoint(T value1, T value2);
    T AddSignificantValue(T value, bool positive);
}

public class IntNudgeHandler : INudgeHandler<int>
{
    const int Significance = 1;
    public int GenerateNudge(int minValue, int maxValue, int parentAValue, int parentBValue)
    {
        

        if (IsDifferentEnough(parentAValue, parentBValue))
        {
            return GetHalfwayPoint(parentAValue, parentBValue);
        }

        if (parentAValue + Significance > maxValue)
            return maxValue - Significance;
        if (parentAValue - Significance < minValue)
            return minValue + Significance;

        var coinFlip = MathFunc.FlipACoin();
        return AddSignificantValue(parentAValue, coinFlip == CoinFlip.Heads);
    }

    public bool IsDifferentEnough(int value1, int value2) => Math.Abs(value1 - value2) > 1;
    public int GetHalfwayPoint(int value1, int value2) => Math.Min(value1, value2) + ((Math.Max(value1, value2) - Math.Min(value1, value2)) / 2);
    public int AddSignificantValue(int value, bool positive) => positive ? value + Significance : value - Significance;
}

public class DecimalNudgeHandler : INudgeHandler<decimal>
{
    private decimal _significance = 0m;
    public decimal GenerateNudge(decimal minValue, decimal maxValue, decimal parentAValue, decimal parentBValue)
    {
        _significance = GetSignificantDifference(minValue, maxValue);

        if (IsDifferentEnough(parentAValue, parentBValue))
        {
            return GetHalfwayPoint(parentAValue, parentBValue);
        }

        if (parentAValue + _significance > maxValue)
            return maxValue - _significance;
        if (parentAValue - _significance < minValue)
            return minValue + _significance;

        var coinFlip = MathFunc.FlipACoin();
        var burp = AddSignificantValue(parentAValue, coinFlip == CoinFlip.Heads);
        return burp;
    }

    public bool IsDifferentEnough(decimal value1, decimal value2) => Math.Abs(value1 - value2) > GetSignificantDifference(value1, value2);
    public decimal GetHalfwayPoint(decimal value1, decimal value2) => Math.Min(value1, value2) + ((Math.Max(value1, value2) - Math.Min(value1, value2)) / 2m);
    public decimal AddSignificantValue(decimal value, bool positive) => positive ? value + _significance : value - _significance;
    private static decimal GetSignificantDifference(decimal minValue, decimal maxValue) => (maxValue - minValue) * 0.01m; // 1%
}

public class LocalDateTimeNudgeHandler : INudgeHandler<LocalDateTime>
{
    const int Significance = 1; // 1 month
    public LocalDateTime GenerateNudge(LocalDateTime minValue, LocalDateTime maxValue, LocalDateTime parentAValue, LocalDateTime parentBValue)
    {
        

        if (IsDifferentEnough(parentAValue, parentBValue))
        {
            var halfway = GetHalfwayPoint(parentAValue, parentBValue);
            return halfway;
        }

        if (parentAValue.PlusMonths(Significance) > maxValue)
            return maxValue.PlusMonths(-Significance);
        if (parentAValue.PlusMonths(-Significance) < minValue)
            return minValue.PlusMonths(Significance);

        var coinFlip = MathFunc.FlipACoin();
        return AddSignificantValue(parentAValue, coinFlip == CoinFlip.Heads);
    }

    public bool IsDifferentEnough(LocalDateTime value1, LocalDateTime value2)
    {
        var span = value1 - value2;
        var spanMonths = (span.Years * 12) + span.Months;
        return Math.Abs(spanMonths) > 1;
    }

    public LocalDateTime GetHalfwayPoint(LocalDateTime value1, LocalDateTime value2)
    {
        var span = value1 - value2;
        var spanMonths = (span.Years * 12) + span.Months;
        var diff = Math.Abs(spanMonths);
        var halfDiff = diff / 2;
        return value1 > value2 ? value2.PlusMonths(halfDiff) : value1.PlusMonths(halfDiff);
    }

    public LocalDateTime AddSignificantValue(LocalDateTime value, bool positive)
    {
        return positive ? value.PlusMonths(Significance) : value.PlusMonths(-Significance);
    }
}


