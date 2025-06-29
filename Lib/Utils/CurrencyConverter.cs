// namespace Lib.Utils;
//
// /// <summary>
// /// decimal math is no bueno, so we like to convert our decimals to longs, turning 1/100ths of a penny into an integer
// /// 1. this class is designed for rapid, consistent conversion in either direction
// /// </summary>
// public static class CurrencyConverter
// {
//     private const decimal _multiplier = 10000M;
//     private const int _significantDigits = 4;
//
//     public static decimal ConvertToCurrency(long amount)
//     {
//         return Math.Round(amount / _multiplier, _significantDigits);
//     }
//     public static long ConvertFromCurrency(decimal amount)
//     {
//         return (long)(amount * _multiplier);
//     }
// }