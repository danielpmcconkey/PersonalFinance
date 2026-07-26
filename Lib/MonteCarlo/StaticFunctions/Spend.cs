using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Spend
{
    #region calculation functions

    /// <summary>
    /// used for rebalancing functions to determine how much cash should be on hand. This is always based on current age
    /// </summary>
    public static decimal CalculateCashNeedForNMonths(DataTypes.MonteCarlo.Model model, PgPerson person, BookOfAccounts accounts,
        LocalDateTime currentDate, int nMonths,
        decimal cumulativeCpiMultiplier = 1m, decimal currentCpiGrowthRate = 0m)
    {
        var cashNeeded = 0m;
        for (var i = 0; i < nMonths; i++)
        {
            var futureDate = currentDate.PlusMonths(i);
            // compound the multiplier forward: month 0 uses current multiplier, month i compounds i times
            var iterationMultiplier = cumulativeCpiMultiplier * (decimal)Math.Pow((double)(1m + currentCpiGrowthRate), i);
            var fun = CalculateMonthlyFunSpend(model, person, futureDate, iterationMultiplier);
            var required = CalculateMonthlyRequiredSpend(model, person, futureDate, accounts, iterationMultiplier);
            var totalThisMonth = fun + required.TotalSpend;
            cashNeeded += totalThisMonth;
        }
        return cashNeeded;
    }

    public static decimal CalculateFunPointsForSpend(decimal funSpend, PgPerson person,
        LocalDateTime currentDate, decimal cumulativeCpiMultiplier = 1m)
    {
        /*
         * the younger I am, the more I'd enjoy spending money. Start with each dollar equaling 1 fun point and go
         * downward, based on age, until a dollar spent at 90 yields only 0.5 fun points 
         */
        var age = currentDate.Year - person.BirthDate.Year;
        const decimal oneToOneAge = 50m;
        const decimal oneToOneHalfAge = 90m;
        const decimal noPenalty = 1.0m;
        const decimal fullPenalty = 0.5m;
        var minFunPoints = 0.5m * funSpend;
        var maxFunPoints = funSpend;
        
        var distanceBetweenPenaltyLevels = noPenalty - fullPenalty; // 0.5
        var distanceBetweenAgeLevels = oneToOneHalfAge - oneToOneAge; // 40
        
        var penaltyPerYear = distanceBetweenPenaltyLevels / distanceBetweenAgeLevels; // 0.0125
        var numPenaltyYears = age - oneToOneAge;
        var totalPenaltyAmount = penaltyPerYear * numPenaltyYears;
        var funPointsPerDollar = noPenalty - totalPenaltyAmount;
        var funPoints = funPointsPerDollar * funSpend;
        funPoints = Math.Max(funPoints, minFunPoints); // cap the penalty at 1/2
        funPoints = Math.Min(funPoints, maxFunPoints); // no extra bucks for being younger than 50
        // discount by accumulated inflation: same dollar buys less fun as prices rise
        return funPoints / cumulativeCpiMultiplier;
    }
    public static decimal CalculateMonthlyFunSpend(DataTypes.MonteCarlo.Model model, PgPerson person, LocalDateTime currentDate,
        decimal cumulativeCpiMultiplier = 1m)
    {
        /*
         * pre-retirement, just use the DesiredMonthlySpendPreRetirement value.
         *
         * post-retirment, we'll start out with the full amount until age 66. Then, for every year, we'll decline our
         * spending until, at age 88, we've reached 0, because we're in assisted living and our fun time is over.
         */
        if (currentDate < model.RetirementDate)
        {
            return model.DesiredMonthlySpendPreRetirement * cumulativeCpiMultiplier;
        }

        var age = currentDate.Year - person.BirthDate.Year;
        if (age < 66)
        {
            return model.DesiredMonthlySpendPostRetirement * cumulativeCpiMultiplier;
        }

        if (age >= 88)
        {
            return 0;
        }

        var declineAmountPerYear = model.DesiredMonthlySpendPostRetirement / (88 - 65);
        var howManyYearsAbove65 = age - 65;
        var declineAmount = declineAmountPerYear * howManyYearsAbove65;
        return (model.DesiredMonthlySpendPostRetirement - declineAmount) * cumulativeCpiMultiplier;
    }
    public static decimal CalculateMonthlyHealthSpend(DataTypes.MonteCarlo.Model model, PgPerson person, LocalDateTime currentDate,
        decimal cumulativeCpiMultiplier = 1m)
    {
        /*
         * if we're not yet retired, Dan's primary employer will provide healthcare, so we can return immediately
         *
         * If we're retired, but before age 65, we'll need to pay the entirety of health costs out of pocket
         *
         * If we're age 88 or over, we're gonna go back to paying the full amount times 2, simulating the need to pay for
         * assisted living.
         *
         * Otherwise, ages 65 - 88, we'll enjoy Medicare, and that spend will change with age
         *
         * medicare costs taken from
         * https://www.medicare.gov/basics/get-started-with-medicare/medicare-basics/what-does-medicare-cost
         *
         * Part A costs
         * Assume our number of hospital stays will increase with age and their average duration will as well. Start
         * with 1.5 stays per year, covering both partners. For every decade in age, increase the number of hospital
         * stays by 1. So, at age 75, we'll have 2.5 stays per year between the 2 of us. While the number of days each
         * stay will last will increase with age, Medicare will pay 100% after the deductible unless you stay over 60
         * days. We won't be modeling stays longer than 60 days
         *
         * Part B costs
         * We'll each pay $185 / month premium and there's a $257 deductible before they'll pay anything. This seems to
         * not change with age
         *
         * Part D costs
         * Part D is privatized. It seems the average plan premium costs $46.50 per month each, with individual
         * prescriptions costing more. We just have to guess with this one
         */
        
        // before retirement, primary employmentt will fund healthcare and doesn't need to be tracked separately
        if (currentDate < model.RetirementDate) return 0;

        // before age 65 (if retired), we have no medicare and need to pay for everything out of pocket
        var age = currentDate.Year - person.BirthDate.Year;
        if (age < 65) return person.RequiredMonthlySpendHealthCare * cumulativeCpiMultiplier;

        // between ages of 88 and 90 we simulate assisted living
        if (age >= 88) return person.RequiredMonthlySpendHealthCare * 2m * cumulativeCpiMultiplier;

        // medicare time, set up the constants (scaled for inflation)
        const decimal partAPremiumAnnual = 0m;
        var partADeductiblePerAdmission = 1676m * cumulativeCpiMultiplier;
        const decimal age65NumberOfHospitalAdmissionsPerYear = 1.5m;
        const decimal numberOfHospitalAdmissionsIncreaseByDecade = 1m;
        var partBPremiumMonthly = 370m * cumulativeCpiMultiplier; // 185 each, assuming we don't go over 220k income;
        var partBAnnualDeductible = 514m * cumulativeCpiMultiplier; // 257 each
        var partDPremiumMonthly = 93m * cumulativeCpiMultiplier; // 46.5 each based on https://www.nerdwallet.com/article/insurance/medicare/how-much-does-medicare-part-d-cost
        var partDAverageMonthlyDrugCost = 150m * cumulativeCpiMultiplier; // total SWAG
        
        // calculate medicare part A costs
        var yearsOver65 = age - 65;
        var decadesOver65 = yearsOver65 / 10m;
        var numHospitalAdmissionsPerYear = age65NumberOfHospitalAdmissionsPerYear +
                (decadesOver65 * numberOfHospitalAdmissionsIncreaseByDecade);
        var partADeductible = partADeductiblePerAdmission * numHospitalAdmissionsPerYear;
        var totalPartACostPerYear  = partAPremiumAnnual + partADeductible;
        var totalPartACostPerMonth = totalPartACostPerYear / 12m;
        
        // calculate medicare part B costs
        var totalPartBCostPerMonth = partBPremiumMonthly + (partBAnnualDeductible / 12m);
        
        // Part D costs
        var totalPartDCostPerMonth = partDPremiumMonthly + partDAverageMonthlyDrugCost;
        
        return totalPartACostPerMonth + totalPartBCostPerMonth + totalPartDCostPerMonth;
    }
    public static (decimal TotalSpend, decimal HealthSpend, decimal debtSpend)
        CalculateMonthlyRequiredSpend(DataTypes.MonteCarlo.Model model, PgPerson person, LocalDateTime currentDate,
            BookOfAccounts accounts, decimal cumulativeCpiMultiplier = 1m)
    {
        var standardSpend = person.RequiredMonthlySpend * cumulativeCpiMultiplier;
        var healthCareSpend = CalculateMonthlyHealthSpend(model, person, currentDate, cumulativeCpiMultiplier);
        var debtSpend = accounts.DebtAccounts
            .SelectMany(x => x.Positions
                .Where(y => y.IsOpen))
            .Sum(x => x.MonthlyPayment); // debt payments are fixed per BR-4 — no CPI adjustment
        return (standardSpend + healthCareSpend + debtSpend, healthCareSpend, debtSpend);
    }
    /// <summary>
    /// the CalculateMonthlyRequiredSpend will take debts into account for purposes of rebalancing and investing extra
    /// cash. it does so to ensure adequate cash on hand. this function passes an empty book of accouts on hand to get
    /// the required spend without debt payments. this is here in support of the Simulation.PayForStuff method that only
    /// wants the actual required spend, assuming that PayDownDebt will be taking care of the debt payment
    /// </summary>
    public static (decimal TotalSpend, decimal HealthSpend, decimal debtSpend) CalculateMonthlyRequiredSpendWithoutDebt(DataTypes.MonteCarlo.Model model, PgPerson person,
        LocalDateTime currentDate, decimal cumulativeCpiMultiplier = 1m)
    {
        // create an empty book of accounts
        var accounts = Account.CreateBookOfAccounts(
            [],
            [new McDebtAccount(){Id = Guid.NewGuid(), Name = "empty", Positions = []}]
            );
        return CalculateMonthlyRequiredSpend(model, person, currentDate, accounts, cumulativeCpiMultiplier);
    }

    /// <summary>
    /// if we're currently in a recession or extreme austerity measures, then temper the fun spend. if we livin' large,
    /// go nuts
    /// </summary>
    public static decimal CalculateSpendOverride(
        DataTypes.MonteCarlo.Model simParameters, decimal standardSpendAmount, RecessionStats recessionStats)
    {
        // if we livin' large, it doesn't matter if we're in a recession or not. we livin' LARGE
        if (recessionStats.AreWeInLivinLargeMode) return standardSpendAmount * simParameters.LivinLargeRatio;
        
        if (recessionStats.AreWeInExtremeAusterityMeasures) 
            return standardSpendAmount * simParameters.ExtremeAusterityRatio;
        if (recessionStats.AreWeInARecession) return standardSpendAmount * simParameters.AusterityRatio;

        return standardSpendAmount;
    }
    

    #endregion calculation functions

    #region copy functions

    
    public static LifetimeSpend CopyLifetimeSpend(LifetimeSpend lifetimeSpend)
    {
        var spend = new LifetimeSpend()
        {
            TotalSpendLifetime = lifetimeSpend.TotalSpendLifetime,
            TotalInvestmentAccrualLifetime = lifetimeSpend.TotalInvestmentAccrualLifetime,
            TotalDebtAccrualLifetime = lifetimeSpend.TotalDebtAccrualLifetime,
            TotalSocialSecurityWageLifetime = lifetimeSpend.TotalSocialSecurityWageLifetime,
            TotalDebtPaidLifetime = lifetimeSpend.TotalDebtPaidLifetime,
            TotalFunPointsLifetime = lifetimeSpend.TotalFunPointsLifetime,
            TotalLifetimeHealthCareSpend = lifetimeSpend.TotalLifetimeHealthCareSpend,
            TotalLifetimeFunSpend = lifetimeSpend.TotalLifetimeFunSpend,
            TotalLifetimeRequiredSpend = lifetimeSpend.TotalLifetimeRequiredSpend,
        };
        return spend;
    }

    #endregion

    #region Record functions
    
    /// <summary>
    /// adds to existing totals when a value is present. otherwise, uses the existing totals
    /// </summary>
    public static (LifetimeSpend spend, List<ReconciliationMessage> messages) RecordMultiSpend(
        LifetimeSpend lifetimeSpend, LocalDateTime currentDate, 
        decimal? totalSpendLifetime,
        decimal? totalInvestmentAccrualLifetime,
        decimal? totalDebtAccrualLifetime,
        decimal? totalSocialSecurityWageLifetime,
        decimal? totalDebtPaidLifetime,
        decimal? totalFunPointsLifetime,
        decimal? totalLifetimeHealthCareSpend,
        decimal? totalLifetimeFunSpend,
        decimal? totalLifetimeRequiredSpend)
    {
        (LifetimeSpend spend, List<ReconciliationMessage> messages) result = (new LifetimeSpend(), []);
        result.spend.TotalSpendLifetime = (totalSpendLifetime is null) ? lifetimeSpend.TotalSpendLifetime : lifetimeSpend.TotalSpendLifetime + (decimal)totalSpendLifetime;
        result.spend.TotalInvestmentAccrualLifetime = (totalInvestmentAccrualLifetime is null) ? lifetimeSpend.TotalInvestmentAccrualLifetime : lifetimeSpend.TotalInvestmentAccrualLifetime + (decimal)totalInvestmentAccrualLifetime;
        result.spend.TotalDebtAccrualLifetime = (totalDebtAccrualLifetime is null) ? lifetimeSpend.TotalDebtAccrualLifetime : lifetimeSpend.TotalDebtAccrualLifetime + (decimal)totalDebtAccrualLifetime;
        result.spend.TotalSocialSecurityWageLifetime = (totalSocialSecurityWageLifetime is null) ? lifetimeSpend.TotalSocialSecurityWageLifetime : lifetimeSpend.TotalSocialSecurityWageLifetime + (decimal)totalSocialSecurityWageLifetime;
        result.spend.TotalDebtPaidLifetime = (totalDebtPaidLifetime is null) ? lifetimeSpend.TotalDebtPaidLifetime : lifetimeSpend.TotalDebtPaidLifetime + (decimal)totalDebtPaidLifetime;
        result.spend.TotalFunPointsLifetime = (totalFunPointsLifetime is null) ? lifetimeSpend.TotalFunPointsLifetime : lifetimeSpend.TotalFunPointsLifetime + (decimal)totalFunPointsLifetime;
        result.spend.TotalLifetimeHealthCareSpend = (totalLifetimeHealthCareSpend is null) ? lifetimeSpend.TotalLifetimeHealthCareSpend : lifetimeSpend.TotalLifetimeHealthCareSpend + (decimal)totalLifetimeHealthCareSpend;
        result.spend.TotalLifetimeFunSpend = (totalLifetimeFunSpend is null) ? lifetimeSpend.TotalLifetimeFunSpend : lifetimeSpend.TotalLifetimeFunSpend + (decimal)totalLifetimeFunSpend;
        result.spend.TotalLifetimeRequiredSpend = (totalLifetimeRequiredSpend is null) ? lifetimeSpend.TotalLifetimeRequiredSpend : lifetimeSpend.TotalLifetimeRequiredSpend + (decimal)totalLifetimeRequiredSpend;
        if (!MonteCarloConfig.DebugMode) return result;
        
        result.messages.Add(new ReconciliationMessage(currentDate,null, "Multi spend recorded."));
        
        return result;
    }
    
    
    #endregion

}