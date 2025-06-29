// todo: delete the bank file

// using System.Runtime.InteropServices.JavaScript;
// using Lib.DataTypes.MonteCarlo;
// using NodaTime;
//
// namespace Lib.MonteCarlo
// {
//     internal class Bank
//     {
//         
//
//
//
//         
//         
//         
//         
//
// #endregion public interface
//
// #region Reconciliation methods
// // these methods are used to validate that the code is producing the correct results
// // they should not be used in the normal running of the sim
//
//
//
//         public long Recon_GetAssetTotalByType(McInvestmentPositionType t)
//         {
//             long total = 0.0M;
//             var accounts = _investmentAccounts
//                 .Where(x => x.AccountType != McInvestmentAccountType.PRIMARY_RESIDENCE
//                     && x.AccountType != McInvestmentAccountType.CASH)
//                 .ToList();
//             foreach (var a in accounts)
//             {
//                 var positions = a.Positions
//                     .Where(x => x.InvestmentPositionType == t && x.IsOpen)
//                     .ToList();
//                 foreach (var p in positions)
//                 {
//                     total += p.CurrentValue;
//                 }
//             }
//             return total;
//         }
//         
//         
//
//
//
// #endregion
//
//
//
//         #region private methods
//
//         
//         
//         
//         
//         
//
//         
//         #endregion private methods
//     }
// }