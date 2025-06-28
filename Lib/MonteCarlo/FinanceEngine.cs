// using Lib.DataTypes.MonteCarlo;
// using NodaTime;
//
// namespace Lib.MonteCarlo;
//
// public class FinanceEngine
// {
//     #region Simulation meta
//     
//     private CorePackage _corePackage;
//     private Logger _logger;
//     private McModel _simParams;
//     
//     #endregion  Simulation meta
//     
//     
//
//    
//     
//
//     
//
//     public FinanceEngine(CorePackage corePackage, McModel simParams, List<McInvestmentAccount> investmentAccounts,
//         List<McDebtAccount> debtAccounts)
//     {
//         _corePackage = corePackage;
//         _logger = _corePackage.Log;
//         _simParams = simParams;
//         _investmentAccounts = investmentAccounts;
//         _debtAccounts = debtAccounts;
//         SetAccountPointers();
//         _rmdDistributions = [];
//         _lastExtremeAusterityMeasureEnd = _simParams.SimStartDate;
//
//
//         if (_corePackage.DebugMode == true)
//         {
//             _reconciliationLedger = new ReconciliationLedger(_corePackage);
//             
//             AddReconLine(
//                 _simParams.SimStartDate,
//                 ReconciliationLineItemType.Credit,
//                 0.0M,
//                 $"Opening bank account"
//             );
//         }
//     }
//
//     
//
// }