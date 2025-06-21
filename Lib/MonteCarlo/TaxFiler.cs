using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo
{
    internal class TaxFiler
    {
        private List<(LocalDateTime earnedDate, decimal amount)> _socialSecurityIncome;
        private List<(LocalDateTime earnedDate, decimal amount)> _ordinaryIncome;
        private List<(LocalDateTime earnedDate, decimal amount)> _capitalGains;

        private (decimal rate, decimal min, decimal max)[] _incomeTaxBrackets;
        private (decimal rate, decimal min, decimal max)[] _capitalGainsBrackets;

        private Dictionary<int, decimal> _rmdTable;

        /// <summary>
        /// this is the amount of income above social security that you want to
        /// maximize the 12% tax bracket
        /// </summary>
        private decimal _incomeTarget = 80000M;

        private const decimal _standardDeduction = 30000M;
        private const decimal _ncFiatTaxRate = 0.0399M;
        private decimal _totalTaxPaid;
        public decimal TotalTaxPaid { get { return _totalTaxPaid; } }
        private Bank _bank;
        private CorePackage _corePackage;
        public TaxFiler(CorePackage corePackage)
        {
            _corePackage = corePackage;
            
            
            _socialSecurityIncome = [];
            _ordinaryIncome = [];
            _capitalGains = [];

            _incomeTaxBrackets = [
                (0.10M, 0M, 23850M),
                (0.12M, 23850M, 96950M),
                (0.22M, 96950M, 206700M),
                (0.24M, 206700M, 394600M),
                (0.32M, 394600M, 501050M),
                (0.35M, 501050M, 751600M),
                (0.37M, 751600M, decimal.MaxValue),
                ];
            _capitalGainsBrackets = [
                (0.0M, 0M, 94050M),
                (0.15M, 94050M, 583750M),
                (0.20M, 583750M, decimal.MaxValue),
                ];
            _rmdTable = [];
            _rmdTable[2048] = 26.5M; // age 73
            _rmdTable[2049] = 25.5M; // age 74
            _rmdTable[2050] = 24.6M; // age 75
            _rmdTable[2051] = 23.7M; // age 76
            _rmdTable[2052] = 22.9M; // age 77
            _rmdTable[2053] = 22.0M; // age 78
            _rmdTable[2054] = 21.1M; // age 79
            _rmdTable[2055] = 20.2M; // age 80
            _rmdTable[2056] = 19.4M; // age 81
            _rmdTable[2057] = 18.5M; // age 82
            _rmdTable[2058] = 17.7M; // age 83
            _rmdTable[2059] = 16.8M; // age 84
            _rmdTable[2060] = 16.0M; // age 85
            _rmdTable[2061] = 15.2M; // age 86
            _rmdTable[2062] = 14.4M; // age 87
            _rmdTable[2063] = 13.7M; // age 88
            _rmdTable[2064] = 12.9M; // age 89
            _rmdTable[2065] = 12.2M; // age 90

            _totalTaxPaid = 0M;
        }

        #region public methods

        public void SetBank(Bank bank)
        {
            _bank = bank;
        }
        /// <summary>
        /// The best income scenario is that all taxable social security and
        /// all taxable capital gains add up to $96,950 and are taxed at 12%,
        /// with all other income coming from Roth or HSA accounts. So this
        /// takes our income target (which is already $96,950 minus last year's
        /// taxable social security minus the standard deduction) and subtracts
        /// any income or taxable capital gains accrued thus far in the year
        /// </summary>
        /// <param name="year"></param>
        /// <returns></returns>
        public decimal CalculateIncomeRoom(int year)
        {
            var room = _incomeTarget -
                CalculateOrdinaryIncomeForYear(year) -
                CalculateCapitalGainsForYear(year)
            ;
            return Math.Max(room, 0);
        }
        public decimal CalculateTaxLiabilityForYear(LocalDateTime currentDate, int taxYear)
        {
            var earnedIncome = CalculateEarnedIncomeForYear(taxYear);
            var totalCapitalGains = CalculateCapitalGainsForYear(taxYear);
            if (_corePackage.DebugMode == true)
            {
                _bank.AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Credit,
                    earnedIncome,
                    $"Earned income calculated for tax year {taxYear}"
                );
            }
            if (_corePackage.DebugMode == true)
            {
                _bank.AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Credit,
                    totalCapitalGains,
                    $"Total capital gains calculated for tax year {taxYear}"
                );
            }
            
            

            //Logger.Info($"earned income: {earnedIncome.ToString("C")}");
            //Logger.Info($"capital gains: {totalCapitalGains.ToString("C")}");

            UpdateIncomeTarget(taxYear);


            decimal totalLiability = 0.0M;

            // tax on ordinary income
            foreach (var bracket in _incomeTaxBrackets)
            {
                var amountInBracket =
                    earnedIncome
                    - (Math.Max(earnedIncome, bracket.max) - bracket.max) // amount above max
                    - (Math.Min(earnedIncome, bracket.min)) // amount below min
                    ;
                totalLiability += (amountInBracket * bracket.rate);
            }
            // tax on capital gains
            if (earnedIncome + totalCapitalGains < _capitalGainsBrackets[0].max)
            {
                // you have 0 capital gains to pay. It stacks on top of earned
                // income but still comes out less than the 0% max
            }
            else if (earnedIncome < _capitalGainsBrackets[0].max)
            {
                // the difference between your earned income and the free
                // bracket max is free. the rest is charged at normal capital
                // gains rates

                var bracket1 = _capitalGainsBrackets[1];
                var bracket2 = _capitalGainsBrackets[2];

                var totalRevenue = earnedIncome + totalCapitalGains;
                // any of totalRevenue above 583,750 is taxed at 20%
                var amountAtBracket2 = Math.Max(0, totalRevenue - bracket2.min);
                totalLiability += (amountAtBracket2 * bracket2.rate);

                // any of totalRevenue above 94,050 but below 583,750 is taxed at 15%
                var amountAtBracket1 = Math.Max(0, totalRevenue - bracket1.min - amountAtBracket2);
                totalLiability += (amountAtBracket1 * bracket1.rate);
            }
            else
            {
                // there is no free bracket. Everything below the bracket 1 max
                // is taxed at the bracket 1 rate
                var bracket1 = _capitalGainsBrackets[1];
                var amountInBracket1 =
                    totalCapitalGains
                        - (Math.Max(totalCapitalGains, bracket1.max) - bracket1.max) // amount above max
                    ;
                totalLiability += (amountInBracket1 * bracket1.rate);
                var bracket2 = _capitalGainsBrackets[2];
                var amountInBracket2 =
                        totalCapitalGains
                        - (Math.Max(totalCapitalGains, bracket2.max) - bracket2.max) // amount above max
                        - (Math.Min(totalCapitalGains, bracket2.min)) // amount below min
                        ;
                totalLiability += (amountInBracket2 * bracket2.rate);
            }

            // NC state income tax
            totalLiability += earnedIncome * _ncFiatTaxRate;
            totalLiability += totalCapitalGains * _ncFiatTaxRate;


            //Logger.Info($"Tax bill of {totalLiability.ToString("C")}");

            _totalTaxPaid += totalLiability;
            return totalLiability;
        }
        public decimal? GetRmdRateByYear(int year)
        {
            if(!_rmdTable.TryGetValue(year, out var rmd))
                return null;
            return rmd;

        }
        public void LogCapitalGain(LocalDateTime earnedDate, decimal amount)
        {
            _capitalGains.Add((earnedDate, amount));
            if (_corePackage.DebugMode == true)
            {
                _bank.AddReconLine(
                    earnedDate,
                    ReconciliationLineItemType.Credit,
                    amount,
                    "Capital gain logged"
                );
            }
        }
        public void LogIncome(LocalDateTime earnedDate, decimal amount)
        {
            _ordinaryIncome.Add((earnedDate, amount));
            if (_corePackage.DebugMode == true)
            {
                _bank.AddReconLine(
                    earnedDate,
                    ReconciliationLineItemType.Credit,
                    amount,
                    "Income logged"
                );
            }
        }
        public void LogInvestmentSale(LocalDateTime saleDate, McInvestmentPosition position,
            McInvestmentAccountType accountType)
        {
            switch(accountType)
            {
                case McInvestmentAccountType.ROTH_401_K:
                case McInvestmentAccountType.ROTH_IRA:
                case McInvestmentAccountType.HSA:
                    // these are completely tax free
                    break; 
                case McInvestmentAccountType.TAXABLE_BROKERAGE:
                    // taxed on growth only
                    LogCapitalGain(saleDate, position.CurrentValue - position.InitialCost);
                    break;
                case McInvestmentAccountType.TRADITIONAL_401_K:
                case McInvestmentAccountType.TRADITIONAL_IRA:
                    // tax deferred. everything is counted as income
                    LogIncome(saleDate, position.CurrentValue);
                    break;
                case McInvestmentAccountType.PRIMARY_RESIDENCE:
                case McInvestmentAccountType.CASH:
                    // these should not be "sold"
                    throw new InvalidDataException();
            }
        }
        public void LogSocialSecurityIncome(LocalDateTime earnedDate, decimal amount)
        {
            _socialSecurityIncome.Add((earnedDate, amount));
            if (_corePackage.DebugMode == true)
            {
                _bank.AddReconLine(
                    earnedDate,
                    ReconciliationLineItemType.Credit,
                    amount,
                    "Social security income logged"
                );
            }
        }

        #endregion public methods



        #region private methods

        private decimal CalculateEarnedIncomeForYear(int year)
        {
            return 
                CalculateOrdinaryIncomeForYear(year) +
                CalculateTaxableSocialSecurityIncomeForYear(year) -
                _standardDeduction;
        }
        private decimal CalculateCapitalGainsForYear(int year)
        {
            return _capitalGains
                .Where(x => x.earnedDate.Year == year)
                .Sum(x => x.amount);
        }
        private decimal CalculateOrdinaryIncomeForYear(int year)
        {
            return _ordinaryIncome
                    .Where(x => x.earnedDate.Year == year)
                    .Sum(x => x.amount);
        }
        /// <summary>
        /// Assumes that all of my social security and income benifit will add
        /// up to enough to be maximally taxable, which is 85% of the total
        /// benefit
        /// </summary>
        private decimal CalculateTaxableSocialSecurityIncomeForYear(int year)
        {
            return (_socialSecurityIncome
                .Where(x => x.earnedDate.Year == year)
                .Sum(x => x.amount)) * .85M;
        }
        /// <summary>
        /// sets the income target for next year based on this year's social
        /// security income
        /// </summary>
        private void UpdateIncomeTarget(int year)
        {
            // update the income target for next year
            var ceiling = _incomeTaxBrackets[1].max;
            var expectedSocialSecurityIncome =
                CalculateTaxableSocialSecurityIncomeForYear(year);
            var expectedTaxableIncome = expectedSocialSecurityIncome - 
                _standardDeduction;
            _incomeTarget = ceiling - expectedTaxableIncome;
        }

        #endregion private methods
    }
}