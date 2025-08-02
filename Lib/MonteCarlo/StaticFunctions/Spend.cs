using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Spend
{
    #region calculation functions

    /// <summary>
    /// used for rebalancing functions to determine how much cash should be on hand. This is always based on current age
    /// </summary>
    public static decimal CalculateCashNeedForNMonths(McModel simParams, PgPerson person, LocalDateTime currentDate, int nMonths)
    {
        var cashNeeded = 0m;
        for (var i = 0; i < nMonths; i++)
        {
            var futureDate = currentDate.PlusMonths(i);
            cashNeeded += CalculateMonthlyFunSpend(simParams, person, futureDate) +
                          CalculateMonthlyRequiredSpend(simParams, person, futureDate);
        }
        return cashNeeded;
    }

    public static decimal CalculateFunPointsForSpend(decimal funSpend, PgPerson person,
        LocalDateTime currentDate)
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
        return funPoints;
    }
    public static decimal CalculateMonthlyFunSpend(McModel simParams, PgPerson person, LocalDateTime currentDate)
    {
        /*
         * pre-retirement, just use the DesiredMonthlySpendPreRetirement value.
         *
         * post-retirment, we'll start out with the full amount until age 66. Then, for every year, we'll decline our
         * spending until, at age 88, we've reached 0, because we're in assisted living and our fun time is over.
         */
        if (currentDate < simParams.RetirementDate) return simParams.DesiredMonthlySpendPreRetirement;
        
        var age = currentDate.Year - person.BirthDate.Year;
        if (age < 66) return simParams.DesiredMonthlySpendPostRetirement;
        if (age >= 88) return 0;
        
        var declineAmountPerYear = simParams.DesiredMonthlySpendPostRetirement / (88 - 65); 
        var howManyYearsAbove65 = age - 65;
        var declineAmount = declineAmountPerYear * howManyYearsAbove65;
        return simParams.DesiredMonthlySpendPostRetirement - declineAmount;
    }
    public static decimal CalculateMonthlyHealthSpend(McModel simParams, PgPerson person, LocalDateTime currentDate)
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
        if (currentDate < simParams.RetirementDate) return 0; 
        
        // before age 65 (if retired), we have no medicare and need to pay for everything out of pocket
        var age = currentDate.Year - person.BirthDate.Year;
        if (age < 65) return person .RequiredMonthlySpendHealthCare;
        
        // between ages of 88 and 90 we simulate assisted living
        if (age >= 88) return person.RequiredMonthlySpendHealthCare * 2m;
        
        // medicare time, set up the constants
        
        const decimal partAPremiumAnnual = 0m;
        const decimal partADeductiblePerAdmission = 1676m;
        const decimal age65NumberOfHospitalAdmissionsPerYear = 1.5m;
        const decimal numberOfHospitalAdmissionsIncreaseByDecade = 1m;
        const decimal partBPremiumMonthly = 370m; // 185 each, assuming we don't go over 220k income;
        const decimal partBAnnualDeductible = 514m; // 257 each
        const decimal partDPremiumMonthly = 93m; // 46.5 each based on https://www.nerdwallet.com/article/insurance/medicare/how-much-does-medicare-part-d-cost
        const decimal partDAverageMonthlyDrugCost = 150m; // total SWAG
        
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
    public static decimal CalculateMonthlyRequiredSpend(McModel simParams, PgPerson person, LocalDateTime currentDate)
    {
        var standardSpend = person.RequiredMonthlySpend;
        var healthCareSpend = CalculateMonthlyHealthSpend(simParams, person, currentDate);
        return standardSpend + healthCareSpend;
    }

    /// <summary>
    /// if we're currently in a recession or extreme austerity measures, then temper the fun spend
    /// </summary>
    public static decimal CalculateRecessionSpendOverride(
        McModel simParameters, decimal standardSpendAmount, RecessionStats recessionStats)
    {
        
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
        };
        return spend;
    }

    #endregion

    #region Record functions

    public static (LifetimeSpend spend, List<ReconciliationMessage> messages) RecordDebtAccrual(
        LifetimeSpend lifetimeSpend, decimal amount, LocalDateTime currentDate)
    {
        (LifetimeSpend spend, List<ReconciliationMessage> messages) result = (CopyLifetimeSpend(lifetimeSpend), []);
        result.spend.TotalDebtAccrualLifetime += amount;
        if (MonteCarloConfig.DebugMode)
        {
            result.messages.Add(new ReconciliationMessage(currentDate,amount * -1, $"Debt accrual recorded: {amount}"));
        }
        return result;
    }
    
    public static (LifetimeSpend spend, List<ReconciliationMessage> messages) RecordDebtPayment(
        LifetimeSpend lifetimeSpend, decimal amount, LocalDateTime currentDate)
    {
        (LifetimeSpend spend, List<ReconciliationMessage> messages) result = (CopyLifetimeSpend(lifetimeSpend), []);
        result.spend.TotalDebtPaidLifetime += amount;
        if (MonteCarloConfig.DebugMode)
        {
            
            result.messages.Add(new ReconciliationMessage(currentDate,amount * -1, $"Debt payment recorded: {amount}"));
        }
        return result;
    }

    public static (LifetimeSpend spend, List<ReconciliationMessage> messages) RecordFunPoints(LifetimeSpend lifetimeSpend, decimal funPoints, LocalDateTime currentDate)
    {
        (LifetimeSpend spend, List<ReconciliationMessage> messages) result = (CopyLifetimeSpend(lifetimeSpend), []);
        result.spend.TotalFunPointsLifetime += funPoints;
        if (MonteCarloConfig.DebugMode)
        {
            result.messages.Add(new ReconciliationMessage(currentDate,0, $"Fun points recorded: {funPoints}"));
        }
        return result;
    }
    
    public static (LifetimeSpend spend, List<ReconciliationMessage> messages) RecordInvestmentAccrual(LifetimeSpend lifetimeSpend, decimal amount, LocalDateTime currentDate)
    {
        (LifetimeSpend spend, List<ReconciliationMessage> messages) result = (CopyLifetimeSpend(lifetimeSpend), []);
        result.spend.TotalInvestmentAccrualLifetime += amount;
        if (MonteCarloConfig.DebugMode)
        {
            result.messages.Add(new ReconciliationMessage(currentDate,amount, $"Investment accrual recorded: {amount}"));
        }
        return result;
    }
    
    public static (LifetimeSpend spend, List<ReconciliationMessage> messages) RecordSocialSecurityWage(LifetimeSpend lifetimeSpend, decimal amount, LocalDateTime currentDate)
    {
        (LifetimeSpend spend, List<ReconciliationMessage> messages) result = (CopyLifetimeSpend(lifetimeSpend), []);
        result.spend.TotalSocialSecurityWageLifetime += amount;
        if (MonteCarloConfig.DebugMode)
        {
            result.messages.Add(new ReconciliationMessage(currentDate,amount, $"Social security wage recorded: {amount}"));
        }
        return result;
    }
    
    public static (LifetimeSpend spend, List<ReconciliationMessage> messages) RecordSpend(LifetimeSpend lifetimeSpend, decimal amount, LocalDateTime currentDate)
    {
        (LifetimeSpend spend, List<ReconciliationMessage> messages) result = (CopyLifetimeSpend(lifetimeSpend), []);
        result.spend.TotalSpendLifetime += amount;
        if (MonteCarloConfig.DebugMode)
        {
            result.messages.Add(new ReconciliationMessage(currentDate,amount * -1, $"Spend recorded: {amount}"));
        }
        return result;
    }
    
    public static (LifetimeSpend spend, List<ReconciliationMessage> messages) RecordHealthcareSpend(LifetimeSpend lifetimeSpend, decimal amount, LocalDateTime currentDate)
    {
        // todo: unit test RecordHealthcareSpend
        (LifetimeSpend spend, List<ReconciliationMessage> messages) result = (CopyLifetimeSpend(lifetimeSpend), []);
        result.spend.TotalLifetimeHealthCareSpend += amount;
        if (MonteCarloConfig.DebugMode)
        {
            result.messages.Add(new ReconciliationMessage(currentDate,amount * -1, $"Healthcare spend recorded: {amount}"));
        }
        return result;
    }
    
    #endregion

}