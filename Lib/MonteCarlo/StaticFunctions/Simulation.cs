using System.Diagnostics;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Simulation
{
    public static NetWorthMeasurement CalculatePercentileValue(NetWorthMeasurement[] sequence,
        decimal percentile)
    {
        // assumes the list is already sorted
        /*
         * length of sequence = 15
         * percentile = .7
         * target row = 0.7 * 15 = 10.5
         * target row = 11 (rounded to nearest int)
         * target row = 10 (zero-indexed)
         *
         * */
        int numRows = sequence.Length;
        decimal targetRowDecimal = numRows * percentile;
        int targetRowInt = (int)(Math.Round(targetRowDecimal, 0));
        return sequence[targetRowInt];
    }
    
    /// <summary>
    /// reads the output from running all lives on a single model and creates statistical views
    /// </summary>
    public static SimulationAllLivesResult InterpretSimulationResults(List<NetWorthMeasurement>[] allMeasurements)
    {
        throw new NotImplementedException();
        // List<SimulationAllLivesResult> batchResults = [];
        // var minDate = allMeasurements.Min(x => x.MeasuredDate);
        // var maxDate = allMeasurements.Max(x => x.MeasuredDate);
        // LocalDateTime dateCursor = minDate;
        // int totalBankruptcies = 0;
        // while (dateCursor <= maxDate)
        // {
        //     // get all the total spend measurements for this date
        //     NetWorthMeasurement[] valuesAtDate = allMeasurements
        //         .Where(x => x.MeasuredDate == dateCursor)
        //         .OrderBy(x => x.TotalSpend)
        //         .ToArray();
        //
        //     // total bankruptcies is a running list of all bankruptcies so
        //     // far. it will grow as the date cursor moves forward
        //     totalBankruptcies += valuesAtDate.Where(x => x.NetWorth <= 0).Count();
        //     var simAt90PercentileSpend = CalculatePercentileValue(valuesAtDate, 0.9M);
        //     var simAt75PercentileSpend = CalculatePercentileValue(valuesAtDate, 0.75M);
        //     var simAt50PercentileSpend = CalculatePercentileValue(valuesAtDate, 0.5M);
        //     var simAt25PercentileSpend = CalculatePercentileValue(valuesAtDate, 0.25M);
        //     var simAt10PercentileSpend = CalculatePercentileValue(valuesAtDate, 0.1M);
        //     batchResults.Add(new SimulationAllLivesResult()
        //     {
        //         Id = Guid.NewGuid(),
        //         ModelId = _mcModel.Id,
        //         MeasuredDate = dateCursor,
        //         NetWorthAt90thPercentile = simAt90PercentileSpend.NetWorth,
        //         NetWorthAt75thPercentile = simAt75PercentileSpend.NetWorth,
        //         NetWorthAt50thPercentile = simAt50PercentileSpend.NetWorth,
        //         NetWorthAt25thPercentile = simAt25PercentileSpend.NetWorth,
        //         NetWorthAt10thPercentile = simAt10PercentileSpend.NetWorth,
        //         SpendAt90thPercentile = simAt90PercentileSpend.TotalSpend,
        //         SpendAt75thPercentile = simAt75PercentileSpend.TotalSpend,
        //         SpendAt50thPercentile = simAt50PercentileSpend.TotalSpend,
        //         SpendAt25thPercentile = simAt25PercentileSpend.TotalSpend,
        //         SpendAt10thPercentile = simAt10PercentileSpend.TotalSpend,
        //         TaxesAt90thPercentile = simAt90PercentileSpend.TotalTax,
        //         TaxesAt75thPercentile = simAt75PercentileSpend.TotalTax,
        //         TaxesAt50thPercentile = simAt50PercentileSpend.TotalTax,
        //         TaxesAt25thPercentile = simAt25PercentileSpend.TotalTax,
        //         TaxesAt10thPercentile = simAt10PercentileSpend.TotalTax,
        //         BankruptcyRate = (1.0M * totalBankruptcies) / (1.0M * allMeasurements.Count),
        //     });
        //     dateCursor = dateCursor.PlusMonths(1);
        // }
        // return batchResults;
    }
    
    /// <summary>
    /// Trigger the simulator for a single run after you've created all the default accounts, parameters and pricing.
    /// Can be used by RunSingle and Train.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="model"></param>
    /// <param name="personCopy">Assumes you created a new "blank" person because C# passes by copy and you want a clean
    /// slate when running multiple sims on the same person</param>
    /// <param name="hypotheticalPrices"></param>
    /// <returns></returns>
    public static List<NetWorthMeasurement> ExecuteSingleModelSingleLife(Logger logger, McModel model, McPerson personCopy, 
        Dictionary<LocalDateTime, Decimal> hypotheticalPrices)
    {
        LifeSimulator sim = new LifeSimulator(logger, model, personCopy, hypotheticalPrices);
        return sim.Run();
    }

    /// <summary>
    /// runs all simulation lives for a single model
    /// </summary>
    /// <returns>an array of NetWorthMeasurement lists. Each element in the array represents one simulated life</returns>
    public static List<NetWorthMeasurement>[] ExecuteSingleModelAllLives(Logger logger,
        McModel model, McPerson basePerson, Dictionary<LocalDateTime, decimal>[] allPricingDicts)
    {
        int numLivesPerModelRun = MonteCarloConfig.NumLivesPerModelRun;
        List<NetWorthMeasurement>[] runs = new List<NetWorthMeasurement>[numLivesPerModelRun];
        
        if(MonteCarloConfig.ShouldRunParallel) Parallel.For(0, numLivesPerModelRun, i =>
        {
            var newPerson = Person.CopyPerson(basePerson);
            LifeSimulator sim = new(logger, model, newPerson, allPricingDicts[i]);
            runs[i] = sim.Run();
        });
        else for(int i = 0; i < numLivesPerModelRun; i++)
        {
            var newPerson = Person.CopyPerson(basePerson);
            LifeSimulator sim = new(logger, model, newPerson, allPricingDicts[i]);
            runs[i] = sim.Run();
        }
        return runs;
    }

    
    /// <summary>
    /// provides results from a pre-determined model. It still runs that
    /// moddel numSimulations times, each over a different "randomized"
    /// set of price simulations
    /// </summary>
    public static SimulationAllLivesResult RunSingleModelSession(Logger logger,
        McModel simParams, McPerson person, decimal[] historicalPrices)
    {   
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.Debug("Creating simulation pricing");
        
        /*
         * create the pricing for all lives
         */
        var hypotheticalPrices = Pricing.CreateHypotheticalPricingForRuns(historicalPrices);
        
        stopwatch.Stop();
        var duration = stopwatch.Elapsed;
        logger.Debug(logger.FormatTimespanDisplay("Created simulation pricing", duration));
        
        
        stopwatch = Stopwatch.StartNew();
        logger.Debug("Running all lives for a single model");
        
        /*
         * run all sim lives
         */
        var allLivesRuns = ExecuteSingleModelAllLives(logger, simParams, person, hypotheticalPrices);
        
        stopwatch.Stop();
        logger.Debug(logger.FormatTimespanDisplay("Ran all lives for a single model", duration));
        
        /*
         * batch up the results into something meaningful
         */
        var results = InterpretSimulationResults(allLivesRuns);
        return results;
    }
    
    /// <summary>
    /// pull the recent champions from the DB, mate them to one another, write the best results back to the DB
    /// </summary>
    /// <returns></returns>
    public static void RunModelTrainingSession(Logger logger, McPerson person, decimal[] historicalPrices)
    {   
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.Debug("Creating simulation pricing");
        
        /*
         * create the pricing for all lives
         */
        var hypotheticalPrices = Pricing.CreateHypotheticalPricingForRuns(historicalPrices);
        
        stopwatch.Stop();
        var duration = stopwatch.Elapsed;
        logger.Debug(logger.FormatTimespanDisplay("Created simulation pricing", duration));
        
        
        /*
         * pull the current champs from the DB
         */
        throw new NotImplementedException();
        var startDate = MonteCarloConfig.MonteCarloSimStartDate;
        var endDate = MonteCarloConfig.MonteCarloSimEndDate;
        McModel[] currentChamps = [
                // todo: pull model champs from the database
                // pull these from google sheets "Montecarlo results" 
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("73fc3530-ad7e-4f81-b037-86bcef0db8ea"), ParentAId = Guid.Parse("febe85c0-f3af-46e8-bb8c-fdf85c275bdd"),ParentBId = Guid.Parse("7c26aa95-eebe-4d6a-b077-58037c10fbe4"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 1:20:05 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("83ccc5a2-ee19-4302-bc22-d6fd137e8368"), ParentAId = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"),ParentBId = Guid.Parse("1cbd1c16-880d-4636-964b-af9bdab2374d"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:06:31 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.43M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 35,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("7ce01b1c-7d8f-4d78-b4e8-78e6c6b51f9e"), ParentAId = Guid.Parse("4bb9f30f-5d16-4143-833f-e1fda4806b46"),ParentBId = Guid.Parse("bb5f246a-4bf2-4006-8d9c-e209d1de017c"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:49:51 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 14,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("deba653f-5180-4772-a20f-cd6a4112b97e"), ParentAId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ParentBId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 8:38:21 PM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 11,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"), ParentAId = Guid.Parse("11111111-1111-1111-1111-111111111130"),ParentBId = Guid.Parse("22222222-2222-2222-0000-000000000019"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2025 12:00:00 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("51fd6802-0188-4d0f-8ba2-3e1afc426668"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("deba653f-5180-4772-a20f-cd6a4112b97e"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:26:50 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("ebe158eb-b3a0-4696-8816-e462e2b9d5b0"), ParentAId = Guid.Parse("45721b19-1629-4e12-a9f9-4ede4d78c945"),ParentBId = Guid.Parse("deba653f-5180-4772-a20f-cd6a4112b97e"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:16:59 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 82,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("641e2be1-12f8-431c-b5f6-dcfec6e2211d"), ParentAId = Guid.Parse("7c26aa95-eebe-4d6a-b077-58037c10fbe4"),ParentBId = Guid.Parse("e112d1a1-34f6-4222-9fba-47f499feb75a"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 12:05:18 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.48M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 43,NumMonthsPriorToRetirementToBeginRebalance = 5,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("69d9557c-9f63-47ed-96ec-280557ae0bbb"), ParentAId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ParentBId = Guid.Parse("f30b7c6f-8247-4d73-ae67-2f24609a3987"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 5:56:24 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 10,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("d318dc1e-94fd-4b19-907d-6779b4f76d7c"), ParentAId = Guid.Parse("b8a61032-5af4-483b-8536-ab55ead95c64"),ParentBId = Guid.Parse("deba653f-5180-4772-a20f-cd6a4112b97e"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 4:23:37 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 50,NumMonthsPriorToRetirementToBeginRebalance = 11,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"), ParentAId = Guid.Parse("cd2ad63d-e8d3-4d9a-8b5c-93afebce4e05"),ParentBId = Guid.Parse("1d11a65a-0d7e-4bd7-89a5-7da7ba7abb8d"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 4:55:51 PM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("c7a3b9ef-4211-4619-9e88-cf55defa5809"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("8eb4c912-7f20-4695-b48e-b096960ed96c"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:19:45 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("e4de6950-78cb-44c0-8846-461a545f2f36"), ParentAId = Guid.Parse("4bb9f30f-5d16-4143-833f-e1fda4806b46"),ParentBId = Guid.Parse("73fc3530-ad7e-4f81-b037-86bcef0db8ea"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:38:50 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("fcc68971-bce4-46df-afbc-d6be794e1a85"), ParentAId = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"),ParentBId = Guid.Parse("bb5f246a-4bf2-4006-8d9c-e209d1de017c"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:04:16 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 59,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("bb5f246a-4bf2-4006-8d9c-e209d1de017c"), ParentAId = Guid.Parse("b8a61032-5af4-483b-8536-ab55ead95c64"),ParentBId = Guid.Parse("8bd6d0a3-e52d-4d25-9243-d506fe46674b"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 4:05:16 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("6d72ece2-602d-48db-a55a-605e3fd3e686"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("8bd6d0a3-e52d-4d25-9243-d506fe46674b"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:06:53 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 47,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("1cbd1c16-880d-4636-964b-af9bdab2374d"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("d38ef633-7643-4cc7-bca1-ec64cce29e32"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:12:21 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 35,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("07b6db01-aa15-47ef-b15e-b63619f5a4cc"), ParentAId = Guid.Parse("45721b19-1629-4e12-a9f9-4ede4d78c945"),ParentBId = Guid.Parse("1cbd1c16-880d-4636-964b-af9bdab2374d"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:28:13 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 518M,MonthlyInvest401kTraditional = 2065M,MonthlyInvestBrokerage = 1417.86M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 57,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("25fcf260-666f-4e2e-ade1-5b272ce2d919"), ParentAId = Guid.Parse("7c26aa95-eebe-4d6a-b077-58037c10fbe4"),ParentBId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 12:25:10 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 12137M,AusterityRatio = 0.96M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                // new McModel() {PersonId = person.Id, BatchResults = [], Id = Guid.Parse("7f28b9cd-2d01-48b5-a131-7d01ba7d0c65"), ParentAId = Guid.Parse("45721b19-1629-4e12-a9f9-4ede4d78c945"),ParentBId = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:09:04 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.77M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("4/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 376M,MonthlyInvest401kTraditional = 2207M,MonthlyInvestBrokerage = 1440.87M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 66,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1.08M},
            ];
        
        
        /*
         * breed and run
         */
        List<(McModel simParams, SimulationAllLivesResult result)> results = [];
        for(int i1 = 0; i1 < currentChamps.Length; i1++)
        {
            for (int i2 = 0; i2 < currentChamps.Length; i2++)
            {
                logger.Info($"running {i1} bred with {i2}");
                var offspring = Model.MateModels(currentChamps[i1], currentChamps[i2]);
                var allLivesRuns2 = ExecuteSingleModelAllLives(logger, offspring, person, hypotheticalPrices);
                var modelResults = InterpretSimulationResults(allLivesRuns2);
                
                results.Add((offspring, modelResults));
            }
        }
        
        /*
         * write results back to the db
         */
        throw new NotImplementedException();
    }
}