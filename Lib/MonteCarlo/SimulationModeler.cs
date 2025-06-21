using Lib.DataTypes.MonteCarlo;
using System.Diagnostics;
using System.Text;
using NodaTime;

namespace Lib.MonteCarlo
{
    public class SimulationModeler
    {
        private Logger _logger;
        private int _numRunsPerBatch;
        /// <summary>
        /// all runs use the _maxRunsPerBatch to create the hypothetics pricing
        /// array. We build that array out to the max you'd ever want to run
        /// things at so that we know we always using the same "random" pricing
        /// for every run. Run 1 for every batch will use the same hypothetical
        /// pricing. Run 7 will use the same pricing. But run 7 will be 
        /// different from run 1
        /// </summary>
        private const int _maxRunsPerBatch = 10000;
        private McPerson _mcPerson;
        // private SimulationParameters _simParams;
        private decimal[] _sAndP500HistoricalTrends;
        Dictionary<LocalDateTime, Decimal>[] _hypotheticalPrices;
        private List<McInvestmentAccount> _investmentAccounts;
        private List<McDebtAccount> _debtAccounts;
        private bool _shouldRunParallel;
        private const decimal _maxBankruptcyRate = 0.0001M;
        private CorePackage _corePackage;


        public SimulationModeler(CorePackage corePackage, McPerson mcPerson,
            decimal[] sAndP500HistoricalTrends)
        {
            _corePackage = corePackage;
            _logger = _corePackage.Log;
            _shouldRunParallel = _corePackage.ShouldRunParallel;
            _mcPerson = mcPerson;
            // _simParams = simParams;
            _investmentAccounts = _mcPerson.InvestmentAccounts;
            _debtAccounts = _mcPerson.DebtAccounts;
            _sAndP500HistoricalTrends = sAndP500HistoricalTrends;
            _hypotheticalPrices = CreateHypotheticalPricingForRuns();
        }

        /// <summary>
        /// goes through many different batches with different parameters to 
        /// find the "best" set of parameters
        /// </summary>
        public void Train(int numRunsPerBatch, LocalDateTime startDate, LocalDateTime endDate)
        {
            _numRunsPerBatch = Math.Min(numRunsPerBatch, _maxRunsPerBatch);

            McModel[] currentChamps = [
                // pull these from google sheets "Montecarlo results" 
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("73fc3530-ad7e-4f81-b037-86bcef0db8ea"), ParentAId = Guid.Parse("febe85c0-f3af-46e8-bb8c-fdf85c275bdd"),ParentBId = Guid.Parse("7c26aa95-eebe-4d6a-b077-58037c10fbe4"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 1:20:05 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("83ccc5a2-ee19-4302-bc22-d6fd137e8368"), ParentAId = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"),ParentBId = Guid.Parse("1cbd1c16-880d-4636-964b-af9bdab2374d"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:06:31 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.43M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 35,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("7ce01b1c-7d8f-4d78-b4e8-78e6c6b51f9e"), ParentAId = Guid.Parse("4bb9f30f-5d16-4143-833f-e1fda4806b46"),ParentBId = Guid.Parse("bb5f246a-4bf2-4006-8d9c-e209d1de017c"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:49:51 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 14,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("deba653f-5180-4772-a20f-cd6a4112b97e"), ParentAId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ParentBId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 8:38:21 PM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 11,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"), ParentAId = Guid.Parse("11111111-1111-1111-1111-111111111130"),ParentBId = Guid.Parse("22222222-2222-2222-0000-000000000019"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2025 12:00:00 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("51fd6802-0188-4d0f-8ba2-3e1afc426668"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("deba653f-5180-4772-a20f-cd6a4112b97e"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:26:50 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("ebe158eb-b3a0-4696-8816-e462e2b9d5b0"), ParentAId = Guid.Parse("45721b19-1629-4e12-a9f9-4ede4d78c945"),ParentBId = Guid.Parse("deba653f-5180-4772-a20f-cd6a4112b97e"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:16:59 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 82,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("641e2be1-12f8-431c-b5f6-dcfec6e2211d"), ParentAId = Guid.Parse("7c26aa95-eebe-4d6a-b077-58037c10fbe4"),ParentBId = Guid.Parse("e112d1a1-34f6-4222-9fba-47f499feb75a"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 12:05:18 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.48M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 43,NumMonthsPriorToRetirementToBeginRebalance = 5,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("69d9557c-9f63-47ed-96ec-280557ae0bbb"), ParentAId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ParentBId = Guid.Parse("f30b7c6f-8247-4d73-ae67-2f24609a3987"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 5:56:24 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 10,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("d318dc1e-94fd-4b19-907d-6779b4f76d7c"), ParentAId = Guid.Parse("b8a61032-5af4-483b-8536-ab55ead95c64"),ParentBId = Guid.Parse("deba653f-5180-4772-a20f-cd6a4112b97e"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 4:23:37 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 50,NumMonthsPriorToRetirementToBeginRebalance = 11,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"), ParentAId = Guid.Parse("cd2ad63d-e8d3-4d9a-8b5c-93afebce4e05"),ParentBId = Guid.Parse("1d11a65a-0d7e-4bd7-89a5-7da7ba7abb8d"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 4:55:51 PM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("c7a3b9ef-4211-4619-9e88-cf55defa5809"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("8eb4c912-7f20-4695-b48e-b096960ed96c"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:19:45 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("e4de6950-78cb-44c0-8846-461a545f2f36"), ParentAId = Guid.Parse("4bb9f30f-5d16-4143-833f-e1fda4806b46"),ParentBId = Guid.Parse("73fc3530-ad7e-4f81-b037-86bcef0db8ea"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:38:50 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("fcc68971-bce4-46df-afbc-d6be794e1a85"), ParentAId = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"),ParentBId = Guid.Parse("bb5f246a-4bf2-4006-8d9c-e209d1de017c"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:04:16 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 59,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("bb5f246a-4bf2-4006-8d9c-e209d1de017c"), ParentAId = Guid.Parse("b8a61032-5af4-483b-8536-ab55ead95c64"),ParentBId = Guid.Parse("8bd6d0a3-e52d-4d25-9243-d506fe46674b"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 4:05:16 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("6d72ece2-602d-48db-a55a-605e3fd3e686"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("8bd6d0a3-e52d-4d25-9243-d506fe46674b"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:06:53 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 47,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("1cbd1c16-880d-4636-964b-af9bdab2374d"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("d38ef633-7643-4cc7-bca1-ec64cce29e32"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:12:21 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 35,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("07b6db01-aa15-47ef-b15e-b63619f5a4cc"), ParentAId = Guid.Parse("45721b19-1629-4e12-a9f9-4ede4d78c945"),ParentBId = Guid.Parse("1cbd1c16-880d-4636-964b-af9bdab2374d"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:28:13 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 518M,MonthlyInvest401kTraditional = 2065M,MonthlyInvestBrokerage = 1417.86M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 57,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("25fcf260-666f-4e2e-ade1-5b272ce2d919"), ParentAId = Guid.Parse("7c26aa95-eebe-4d6a-b077-58037c10fbe4"),ParentBId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 12:25:10 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 12137M,AusterityRatio = 0.96M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
                new McModel() {PersonId = _mcPerson.Id, BatchResults = [], Id = Guid.Parse("7f28b9cd-2d01-48b5-a131-7d01ba7d0c65"), ParentAId = Guid.Parse("45721b19-1629-4e12-a9f9-4ede4d78c945"),ParentBId = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:09:04 AM") ),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.77M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("4/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 376M,MonthlyInvest401kTraditional = 2207M,MonthlyInvestBrokerage = 1440.87M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 66,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1.08M},
            ];
            // cross breed everyone to everyone else. Take the results with 0
            // bankruptcies then sort them by spend at 50%. The best N survive
            // to breed again
            List<(McModel simParams, BatchResult result)> results = [];
            for(int i1 = 0; i1 < currentChamps.Length; i1++)
            {
                for (int i2 = 0; i2 < currentChamps.Length; i2++)
                {
                    _logger.Info($"running {i1} bred with {i2}");
                    var offspring = ModelBreeder.MateSimParameters(currentChamps[i1], currentChamps[i2]);
                    
                    var result = RunSingle(numRunsPerBatch, offspring, false);
                    if (result.BankruptcyRate > _maxBankruptcyRate) continue;
                    results.Add((offspring, result));
                }
            }
            _logger.Info("Printing best 20");
            var bestResults = results
                .OrderByDescending(x => x.result.SpendAt50thPercentile).Take(20);
            foreach(var result in bestResults)
            {
                PrintBatchResultAndParamsToConsole(result.simParams, result.result);
            }
        }
        /// <summary>
        /// provides results from a pre-determined model. It still runs that
        /// moddel numRunsPerBatch times, each over a different "randomized"
        /// set of price simulations
        /// </summary>
        public BatchResult RunSingle(int numRunsPerBatch,
            McModel simParams, bool shouldPrintResult = true)
        {
            //simParams.Person = _mcPerson; // add this here because the Bank class needs access to the person's birthdate
            
            _numRunsPerBatch = Math.Min(numRunsPerBatch, _maxRunsPerBatch);
            BatchManager batchManager = new BatchManager(_corePackage,
                _numRunsPerBatch, _mcPerson, simParams,
                _hypotheticalPrices);
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<BatchResult> batchResults = batchManager.Run();
            stopwatch.Stop();
            var duration = stopwatch.Elapsed;
            _logger.Debug(_logger.FormatTimespanDisplay("Single run duration", duration));
            
            // pull the result row with the latest date so we can print where this sim ended up
            var lastResult =
                batchResults.OrderByDescending(x => x.MeasuredDate).FirstOrDefault()
                ?? throw new InvalidDataException();
            if(shouldPrintResult) PrintBatchResultAndParamsToConsole(simParams, lastResult);
            return lastResult;
        }
        
        private Dictionary<LocalDateTime, decimal>[] CreateHypotheticalPricingForRuns()
        {
            _logger.Info(_logger.FormatHeading("Creating hypothetical pricing array"));
            Stopwatch stopwatch = Stopwatch.StartNew();

            var prices = new Dictionary<LocalDateTime, decimal>[_maxRunsPerBatch];
            // create first and last dates that will always be the same, even
            // if the simulation start and end dates change. this will allow
            // us to have an apples to apples comparison to models created years
            // apart
            var firstDateToCreate = new LocalDateTime(2025,2,1,0,0);
            var lastDateToCreate = new LocalDateTime(2125,2,1,0,0); // I'll be 150. If I live that long, I'll have figured out my finances by then
            var historyDataMonthsCount = _sAndP500HistoricalTrends.Length;
            int seed = 0;
            

            for (int i = 0; i < _maxRunsPerBatch; i++)
            {
                var dateCursor = firstDateToCreate;
                Dictionary<LocalDateTime, Decimal> thisRunsPrices = [];
                while(dateCursor <= lastDateToCreate)
                {
                    Random rand = new Random(seed);
                    int _historicalTrendsPointer = rand.Next(0, historyDataMonthsCount);
                    decimal thisPrice = _sAndP500HistoricalTrends[_historicalTrendsPointer];
                    thisRunsPrices[dateCursor] = thisPrice;
                    // Logger.Info($"{dateCursor}|{thisPrice}");
                    dateCursor = dateCursor.PlusMonths(1);
                    seed++;
                }
                prices[i] = thisRunsPrices;
            }
            var duration = stopwatch.Elapsed;
            _logger.Info(_logger.FormatTimespanDisplay("Hypothetical pricing creation duration",
                duration));
            return prices;
        }
        private void PrintBatchResultAndParamsToConsole(McModel simParams, BatchResult result)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{simParams.Id}\t");
            sb.Append($"{simParams.ParentA}\t");
            sb.Append($"{simParams.ParentB}\t");
            sb.Append($"{simParams.ModelCreatedDate}\t");
            sb.Append($"{result.SpendAt50thPercentile.ToString("C")}\t");
            sb.Append($"{result.BankruptcyRate.ToString("0.00000")}\t");
            sb.Append($"{result.NetWorthAt90thPercentile.ToString("C")}\t");
            sb.Append($"{result.SpendAt90thPercentile.ToString("C")}\t");
            sb.Append($"{result.TaxesAt90thPercentile.ToString("C")}\t");
            sb.Append($"{result.NetWorthAt75thPercentile.ToString("C")}\t");
            sb.Append($"{result.SpendAt75thPercentile.ToString("C")}\t");
            sb.Append($"{result.TaxesAt75thPercentile.ToString("C")}\t");
            sb.Append($"{result.NetWorthAt50thPercentile.ToString("C")}\t");
            sb.Append($"{result.TaxesAt50thPercentile.ToString("C")}\t");
            sb.Append($"{result.NetWorthAt25thPercentile.ToString("C")}\t");
            sb.Append($"{result.SpendAt25thPercentile.ToString("C")}\t");
            sb.Append($"{result.TaxesAt25thPercentile.ToString("C")}\t");
            sb.Append($"{result.NetWorthAt10thPercentile.ToString("C")}\t");
            sb.Append($"{result.SpendAt10thPercentile.ToString("C")}\t");
            sb.Append($"{result.TaxesAt10thPercentile.ToString("C")}\t");
            sb.Append($"{simParams.RetirementDate}\t");
            sb.Append($"{simParams.DesiredMonthlySpend.ToString("C")}\t");
            sb.Append($"{simParams.AusterityRatio}\t");
            sb.Append($"{simParams.ExtremeAusterityRatio}\t");
            sb.Append($"{simParams.ExtremeAusterityNetWorthTrigger}\t");
            sb.Append($"{simParams.SocialSecurityStart}\t");
            sb.Append($"{simParams.MonthlyInvest401kRoth.ToString("C")}\t");
            sb.Append($"{simParams.MonthlyInvest401kTraditional.ToString("C")}\t");
            sb.Append($"{simParams.MonthlyInvestBrokerage.ToString("C")}\t");
            sb.Append($"{simParams.MonthlyInvestHSA.ToString("C")}\t");
            sb.Append($"{simParams.RebalanceFrequency}\t");
            sb.Append($"{simParams.NumMonthsCashOnHand}\t");
            sb.Append($"{simParams.NumMonthsMidBucketOnHand}\t");
            sb.Append($"{simParams.NumMonthsPriorToRetirementToBeginRebalance}\t");
            sb.Append($"{simParams.RecessionCheckLookBackMonths}\t");
            sb.Append($"{simParams.RecessionRecoveryPointModifier.ToString("#.00000")}\t");
            _logger.Info(sb.ToString());
        }
        
    }
}