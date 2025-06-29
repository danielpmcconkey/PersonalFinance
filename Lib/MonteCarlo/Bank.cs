using System.Runtime.InteropServices.JavaScript;
using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo
{
    internal class Bank
    {
        


        public void DepositSocialSecurityCheck(long amount, LocalDateTime currentDate)
        {
            DepositCash(amount, currentDate);
            _totalSocialSecurityWageLifetime += amount;
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Credit,
                    amount,
                    $"Social Security check deposit"
                );
            }
        }

        
        
        
        public void MeetRmdRequirements(LocalDateTime currentDate)
        {
            var year = currentDate.Year;
            var rmdRate = _taxFiler.GetRmdRateByYear(year);
            if(rmdRate is null) { return; } // no requirement this year

            var rate = (long)rmdRate;

            // get total balance in rmd-relevant accounts
            var relevantAccounts = _investmentAccounts
                .Where(x => x.AccountType is McInvestmentAccountType.TRADITIONAL_401_K
                || x.AccountType is McInvestmentAccountType.TRADITIONAL_IRA);
            var balance = 0M;
            foreach (var account in relevantAccounts)
                balance += GetInvestmentAccountTotalValue(account);

            var totalRmdRequirement = balance / rate;
            if(!_rmdDistributions.TryGetValue(year, out long totalRmdSoFar))
            {
                _rmdDistributions[year] = 0;
                totalRmdSoFar = 0;
            }

            if (totalRmdSoFar >= totalRmdRequirement) return;

            var amountLeft = totalRmdRequirement - totalRmdSoFar;

            // start with long-term investments as you're most likely to have them there
            var cashSold =  SellInvestment(amountLeft,
                McInvestmentPositionType.LONG_TERM, currentDate, true);
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Credit,
                    cashSold,
                    "RMD: Sold long-term investment"
                );
            }

            // and invest it back into mid-term
            InvestFunds(currentDate, cashSold, McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE);
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Debit,
                    cashSold,
                    "RMD: Bought mid-term investment"
                );
            }
            amountLeft -= cashSold;
            if (amountLeft <= 0) return;

            // try mid-term investments for remainder
            cashSold = SellInvestment(amountLeft,
                McInvestmentPositionType.MID_TERM, currentDate, true);
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Credit,
                    cashSold,
                    "RMD: Sold mid-term investment"
                );
            }
            // and invest it back into mid-term
            InvestFunds(currentDate, cashSold, McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE);
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Debit,
                    cashSold,
                    "RMD: Bought mid-term investment"
                );
            }
            amountLeft -= cashSold;
            if (amountLeft <= 0) return;

            // nothing's left to try. not sure how we got here
            throw new InvalidDataException();
        }
        

        /// <summary>
        /// deduct cash from the cash account
        /// </summary>
        /// <returns>true if able to pay. false if not</returns>
        public bool WithdrawCash(long amount, LocalDateTime currentDate)
        {
            var totalCashOnHand = GetCashBalance();

            if (totalCashOnHand < amount)
            {
                // can we pull it from the mid bucket?
                var amountNeeded = amount - totalCashOnHand;
                var cashSold = SellInvestment(amountNeeded, McInvestmentPositionType.MID_TERM, currentDate);
                if (_corePackage.DebugMode == true)
                {
                    AddReconLine(
                        currentDate,
                        ReconciliationLineItemType.Credit,
                        cashSold,
                        "Investment sales from mid-term to support cash withdrawal"
                    );
                }
                totalCashOnHand += cashSold;
                if (totalCashOnHand < amount)
                {
                    // can we pull it from the long-term bucket?
                    cashSold = SellInvestment(amountNeeded, McInvestmentPositionType.LONG_TERM, currentDate);
                    if (_corePackage.DebugMode == true)
                    {
                        AddReconLine(
                            currentDate,
                            ReconciliationLineItemType.Credit,
                            cashSold,
                            "Investment sales from long-term to support cash withdrawal"
                        );
                    }
                    totalCashOnHand += cashSold;
                    if (totalCashOnHand < amount)
                    {
                        // we broke. update the account balance just in case.
                        // returning false here should result in a bankruptcy
                        // witch sets everything to 0, but we may change code
                        // flow later and it's important to add our sales
                        // proceeds to the cash account
                        UpdateCashAccountBalance(totalCashOnHand, currentDate);
                        _isBankrupt = true;
                        return false;
                    }
                }
                    
            }
            totalCashOnHand -= amount;
            UpdateCashAccountBalance(totalCashOnHand, currentDate);
            _totalSpendLifetime += amount;
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Debit,
                    amount,
                    "Cash withdrawal"
                );
            }
            return true;
        }

#endregion public interface

#region Reconciliation methods
// these methods are used to validate that the code is producing the correct results
// they should not be used in the normal running of the sim



        public long Recon_GetAssetTotalByType(McInvestmentPositionType t)
        {
            long total = 0.0M;
            var accounts = _investmentAccounts
                .Where(x => x.AccountType != McInvestmentAccountType.PRIMARY_RESIDENCE
                    && x.AccountType != McInvestmentAccountType.CASH)
                .ToList();
            foreach (var a in accounts)
            {
                var positions = a.Positions
                    .Where(x => x.InvestmentPositionType == t && x.IsOpen)
                    .ToList();
                foreach (var p in positions)
                {
                    total += p.CurrentValue;
                }
            }
            return total;
        }
        
        



#endregion



        #region private methods

        
        
        
        
        

        
        #endregion private methods
    }
}