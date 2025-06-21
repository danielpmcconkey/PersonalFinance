using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NodaTime;

namespace Lib.MonteCarlo;

public static class DataStage
{
    public static decimal[] GetSAndP500HistoricalTrends()
    {
        using var context = new PgContext();
        /*
         * this is an array of month over month growth of the S&P500 going
         * back to 1980. we use 1980 because the 50 years prior don't 
         * reflect more modern behavior. I hope.
         * */
        var  historicalGrowthRates = context.HistoricalGrowthRates
            .Where(x => x.Year >= 1980)
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Select(x => x.InflationAdjustedGrowth)
            .ToArray();
        return historicalGrowthRates;
    }
    public static McModel GetModelChampion()
    {
        // var startDate = new  LocalDateTime(2025, 3, 1, 0, 0);
        // var endDate = new LocalDateTime(2065, 2, 1, 0, 0);
        // McModel[] champions = [
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("060fd34c-a554-4590-98b7-d400def0e19d"), ParentAId = Guid.Parse("cd2ad63d-e8d3-4d9a-8b5c-93afebce4e05"),ParentBId = Guid.Parse("0c7d7f13-4358-49d7-a4b4-b168a7fd496e"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 4:43:21 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 5,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("7c26aa95-eebe-4d6a-b077-58037c10fbe4"), ParentAId = Guid.Parse("11111111-1111-1111-1111-111111111127"),ParentBId = Guid.Parse("22222222-2222-2222-0000-000000000016"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2025 12:00:00 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 12137M,AusterityRatio = 0.96M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("d38ef633-7643-4cc7-bca1-ec64cce29e32"), ParentAId = Guid.Parse("11111111-1111-1111-1111-111111111128"),ParentBId = Guid.Parse("22222222-2222-2222-0000-000000000017"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2025 12:00:00 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("02/01/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.42M,ExtremeAusterityNetWorthTrigger = 693009M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("11/1/2040 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},            
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("219cf72b-9f61-4cc7-a4fc-de5785e1e0b1"), ParentAId = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"),ParentBId = Guid.Parse("4bb9f30f-5d16-4143-833f-e1fda4806b46"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:48:22 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.42M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("9/1/2039 12:00:00 AM")),MonthlyInvest401kRoth = 2171M,MonthlyInvest401kTraditional = 412M,MonthlyInvestBrokerage = 1150.08M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 30,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1.07M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("f489bc21-b721-4050-9b3c-d3d375b79847"), ParentAId = Guid.Parse("11111111-1111-1111-1111-111111111129"),ParentBId = Guid.Parse("22222222-2222-2222-0000-000000000018"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2025 12:00:00 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 582494M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("10/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 48,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("febe85c0-f3af-46e8-bb8c-fdf85c275bdd"), ParentAId = Guid.Parse("901051ce-1bb3-4c3d-8891-86b115406b81"),ParentBId = Guid.Parse("cd2ad63d-e8d3-4d9a-8b5c-93afebce4e05"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 3:13:42 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.43M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("11/1/2040 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("f1f14f65-a4e3-4d30-a4b5-26af7532bd73"), ParentAId = Guid.Parse("901051ce-1bb3-4c3d-8891-86b115406b81"),ParentBId = Guid.Parse("1d11a65a-0d7e-4bd7-89a5-7da7ba7abb8d"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 3:19:15 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 624594M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("9/1/2039 12:00:00 AM")),MonthlyInvest401kRoth = 376M,MonthlyInvest401kTraditional = 2207M,MonthlyInvestBrokerage = 1440.87M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 20,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("c7d3a6c9-e571-4677-934d-b2abe5d93978"), ParentAId = Guid.Parse("990beb2e-7efb-4b86-88ef-15a4bb2f1694"),ParentBId = Guid.Parse("990beb2e-7efb-4b86-88ef-15a4bb2f1694"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 5:14:25 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.45M,ExtremeAusterityNetWorthTrigger = 574285M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("11/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 376M,MonthlyInvest401kTraditional = 2207M,MonthlyInvestBrokerage = 1440.87M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 48,NumMonthsPriorToRetirementToBeginRebalance = 66,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("bd7e0b7b-86df-4cc2-ae6e-407b01284b7b"), ParentAId = Guid.Parse("655f7945-eab0-45f2-88fc-316432697cdd"),ParentBId = Guid.Parse("4bb9f30f-5d16-4143-833f-e1fda4806b46"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:56:27 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.42M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("5/1/2039 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 5,NumMonthsPriorToRetirementToBeginRebalance = 23,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("fea7d6b3-eb0f-40e5-a039-50e77abfdba3"), ParentAId = Guid.Parse("990beb2e-7efb-4b86-88ef-15a4bb2f1694"),ParentBId = Guid.Parse("1d11a65a-0d7e-4bd7-89a5-7da7ba7abb8d"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 5:19:18 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.42M,ExtremeAusterityNetWorthTrigger = 574285M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("9/1/2039 12:00:00 AM")),MonthlyInvest401kRoth = 376M,MonthlyInvest401kTraditional = 2207M,MonthlyInvestBrokerage = 1440.87M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 50,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("8eb4c912-7f20-4695-b48e-b096960ed96c"), ParentAId = Guid.Parse("901051ce-1bb3-4c3d-8891-86b115406b81"),ParentBId = Guid.Parse("f489bc21-b721-4050-9b3c-d3d375b79847"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 3:22:53 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 582494M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("10/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 20,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("ccca794e-b740-43a1-8908-dd0663c07d2a"), ParentAId = Guid.Parse("4bb9f30f-5d16-4143-833f-e1fda4806b46"),ParentBId = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:30:41 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.42M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("4/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("8ffdbe64-8d7c-4cb7-98d3-aacc7033dc37"), ParentAId = Guid.Parse("d46c48fb-31de-41be-8990-94155df849c8"),ParentBId = Guid.Parse("b768228a-45d9-4c7a-b263-8627091b4cc2"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 10:46:57 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.42M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("4/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 376M,MonthlyInvest401kTraditional = 2207M,MonthlyInvestBrokerage = 1440.87M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 5,NumMonthsPriorToRetirementToBeginRebalance = 47,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("327d8f6b-3f8d-4da9-a09c-e82dfa6ef2a6"), ParentAId = Guid.Parse("c7d3a6c9-e571-4677-934d-b2abe5d93978"),ParentBId = Guid.Parse("127211d6-df97-44c6-a831-08751a11074b"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 2:18:07 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.46M,ExtremeAusterityNetWorthTrigger = 574285M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("11/1/2040 12:00:00 AM")),MonthlyInvest401kRoth = 376M,MonthlyInvest401kTraditional = 2207M,MonthlyInvestBrokerage = 1440.87M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 53,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("595d2086-6d7a-48fd-ab24-8d4597348db2"), ParentAId = Guid.Parse("fea7d6b3-eb0f-40e5-a039-50e77abfdba3"),ParentBId = Guid.Parse("c7d3a6c9-e571-4677-934d-b2abe5d93978"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 2:37:09 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.45M,ExtremeAusterityNetWorthTrigger = 574285M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("9/1/2039 12:00:00 AM")),MonthlyInvest401kRoth = 376M,MonthlyInvest401kTraditional = 2207M,MonthlyInvestBrokerage = 1440.87M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 65,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("53c5403a-4e1f-4640-bd91-33250a99792e"), ParentAId = Guid.Parse("b768228a-45d9-4c7a-b263-8627091b4cc2"),ParentBId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 5:14:51 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.48M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 376M,MonthlyInvest401kTraditional = 2207M,MonthlyInvestBrokerage = 1440.87M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 47,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1.08M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("a4e204d7-b821-4d8a-a42c-bfaf48736a97"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:28:03 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 1027M,MonthlyInvest401kTraditional = 1556M,MonthlyInvestBrokerage = 1335.41M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 83,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("127211d6-df97-44c6-a831-08751a11074b"), ParentAId = Guid.Parse("901051ce-1bb3-4c3d-8891-86b115406b81"),ParentBId = Guid.Parse("6a5726f7-c9f3-4174-aa13-bf773ab67ca7"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 3:10:12 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.46M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("11/1/2040 12:00:00 AM")),MonthlyInvest401kRoth = 2213M,MonthlyInvest401kTraditional = 370M,MonthlyInvestBrokerage = 1143.27M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 5,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("f30b7c6f-8247-4d73-ae67-2f24609a3987"), ParentAId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ParentBId = Guid.Parse("cd2ad63d-e8d3-4d9a-8b5c-93afebce4e05"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 8:27:17 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.43M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 10,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("1483e5e0-6054-4db0-bab1-3434a90f3e4c"), ParentAId = Guid.Parse("f489bc21-b721-4050-9b3c-d3d375b79847"),ParentBId = Guid.Parse("febe85c0-f3af-46e8-bb8c-fdf85c275bdd"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 12:58:40 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("11/1/2040 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 8,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"), ParentAId = Guid.Parse("f30b7c6f-8247-4d73-ae67-2f24609a3987"),ParentBId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 4:03:18 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.43M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("4/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 2171M,MonthlyInvest401kTraditional = 412M,MonthlyInvestBrokerage = 1150.08M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("45721b19-1629-4e12-a9f9-4ede4d78c945"), ParentAId = Guid.Parse("b8a61032-5af4-483b-8536-ab55ead95c64"),ParentBId = Guid.Parse("b768228a-45d9-4c7a-b263-8627091b4cc2"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 4:22:22 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 376M,MonthlyInvest401kTraditional = 2207M,MonthlyInvestBrokerage = 1440.87M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 50,NumMonthsPriorToRetirementToBeginRebalance = 66,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("4bb9f30f-5d16-4143-833f-e1fda4806b46"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("fea7d6b3-eb0f-40e5-a039-50e77abfdba3"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:18:30 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.42M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("9/1/2039 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 30,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("24d578e9-8621-40fc-8613-2541af6882a8"), ParentAId = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"),ParentBId = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:45:40 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.43M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("4/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 2171M,MonthlyInvest401kTraditional = 412M,MonthlyInvestBrokerage = 1150.08M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("2795b65f-488d-4753-8829-cf3415be9a1e"), ParentAId = Guid.Parse("45721b19-1629-4e12-a9f9-4ede4d78c945"),ParentBId = Guid.Parse("45721b19-1629-4e12-a9f9-4ede4d78c945"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:10:15 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 376M,MonthlyInvest401kTraditional = 2207M,MonthlyInvestBrokerage = 1440.87M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 50,NumMonthsPriorToRetirementToBeginRebalance = 66,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("655f7945-eab0-45f2-88fc-316432697cdd"), ParentAId = Guid.Parse("e112d1a1-34f6-4222-9fba-47f499feb75a"),ParentBId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 11:38:38 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 976M,MonthlyInvest401kTraditional = 1607M,MonthlyInvestBrokerage = 1343.67M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("b8a61032-5af4-483b-8536-ab55ead95c64"), ParentAId = Guid.Parse("222948a9-9fd1-4601-b9da-bd2faf078fe7"),ParentBId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 2:13:51 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 50,NumMonthsPriorToRetirementToBeginRebalance = 66,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("40b4a97e-9f0d-4207-adcc-8ef9c4520ee1"), ParentAId = Guid.Parse("45721b19-1629-4e12-a9f9-4ede4d78c945"),ParentBId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:18:13 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 66,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("974d3504-9eab-4e50-9bb0-a066706c6f1d"), ParentAId = Guid.Parse("7c26aa95-eebe-4d6a-b077-58037c10fbe4"),ParentBId = Guid.Parse("97821d76-257a-40aa-b12b-61970e9b62cb"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 7:10:55 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 13533M,AusterityRatio = 0.81M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 54,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("b768228a-45d9-4c7a-b263-8627091b4cc2"), ParentAId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ParentBId = Guid.Parse("222948a9-9fd1-4601-b9da-bd2faf078fe7"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 8:18:46 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("4/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 376M,MonthlyInvest401kTraditional = 2207M,MonthlyInvestBrokerage = 1440.87M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 50,NumMonthsPriorToRetirementToBeginRebalance = 47,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("73fc3530-ad7e-4f81-b037-86bcef0db8ea"), ParentAId = Guid.Parse("febe85c0-f3af-46e8-bb8c-fdf85c275bdd"),ParentBId = Guid.Parse("7c26aa95-eebe-4d6a-b077-58037c10fbe4"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 1:20:05 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("83ccc5a2-ee19-4302-bc22-d6fd137e8368"), ParentAId = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"),ParentBId = Guid.Parse("1cbd1c16-880d-4636-964b-af9bdab2374d"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:06:31 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.43M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 35,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("7ce01b1c-7d8f-4d78-b4e8-78e6c6b51f9e"), ParentAId = Guid.Parse("4bb9f30f-5d16-4143-833f-e1fda4806b46"),ParentBId = Guid.Parse("bb5f246a-4bf2-4006-8d9c-e209d1de017c"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:49:51 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 14,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("deba653f-5180-4772-a20f-cd6a4112b97e"), ParentAId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ParentBId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 8:38:21 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 11,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"), ParentAId = Guid.Parse("11111111-1111-1111-1111-111111111130"),ParentBId = Guid.Parse("22222222-2222-2222-0000-000000000019"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2025 12:00:00 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("51fd6802-0188-4d0f-8ba2-3e1afc426668"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("deba653f-5180-4772-a20f-cd6a4112b97e"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:26:50 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("ebe158eb-b3a0-4696-8816-e462e2b9d5b0"), ParentAId = Guid.Parse("45721b19-1629-4e12-a9f9-4ede4d78c945"),ParentBId = Guid.Parse("deba653f-5180-4772-a20f-cd6a4112b97e"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:16:59 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 82,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("641e2be1-12f8-431c-b5f6-dcfec6e2211d"), ParentAId = Guid.Parse("7c26aa95-eebe-4d6a-b077-58037c10fbe4"),ParentBId = Guid.Parse("e112d1a1-34f6-4222-9fba-47f499feb75a"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 12:05:18 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.48M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 43,NumMonthsPriorToRetirementToBeginRebalance = 5,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("69d9557c-9f63-47ed-96ec-280557ae0bbb"), ParentAId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ParentBId = Guid.Parse("f30b7c6f-8247-4d73-ae67-2f24609a3987"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 5:56:24 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 10,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("d318dc1e-94fd-4b19-907d-6779b4f76d7c"), ParentAId = Guid.Parse("b8a61032-5af4-483b-8536-ab55ead95c64"),ParentBId = Guid.Parse("deba653f-5180-4772-a20f-cd6a4112b97e"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 4:23:37 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 50,NumMonthsPriorToRetirementToBeginRebalance = 11,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"), ParentAId = Guid.Parse("cd2ad63d-e8d3-4d9a-8b5c-93afebce4e05"),ParentBId = Guid.Parse("1d11a65a-0d7e-4bd7-89a5-7da7ba7abb8d"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/24/2025 4:55:51 PM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("c7a3b9ef-4211-4619-9e88-cf55defa5809"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("8eb4c912-7f20-4695-b48e-b096960ed96c"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:19:45 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("e4de6950-78cb-44c0-8846-461a545f2f36"), ParentAId = Guid.Parse("4bb9f30f-5d16-4143-833f-e1fda4806b46"),ParentBId = Guid.Parse("73fc3530-ad7e-4f81-b037-86bcef0db8ea"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:38:50 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("fcc68971-bce4-46df-afbc-d6be794e1a85"), ParentAId = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"),ParentBId = Guid.Parse("bb5f246a-4bf2-4006-8d9c-e209d1de017c"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:04:16 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 59,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("bb5f246a-4bf2-4006-8d9c-e209d1de017c"), ParentAId = Guid.Parse("b8a61032-5af4-483b-8536-ab55ead95c64"),ParentBId = Guid.Parse("8bd6d0a3-e52d-4d25-9243-d506fe46674b"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 4:05:16 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 14,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("6d72ece2-602d-48db-a55a-605e3fd3e686"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("8bd6d0a3-e52d-4d25-9243-d506fe46674b"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:06:53 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 47,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("1cbd1c16-880d-4636-964b-af9bdab2374d"), ParentAId = Guid.Parse("2f8e8783-ddad-4d5e-98a5-694b0b4acaef"),ParentBId = Guid.Parse("d38ef633-7643-4cc7-bca1-ec64cce29e32"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 6:12:21 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 35,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("07b6db01-aa15-47ef-b15e-b63619f5a4cc"), ParentAId = Guid.Parse("45721b19-1629-4e12-a9f9-4ede4d78c945"),ParentBId = Guid.Parse("1cbd1c16-880d-4636-964b-af9bdab2374d"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:28:13 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.69M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 518M,MonthlyInvest401kTraditional = 2065M,MonthlyInvestBrokerage = 1417.86M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 24,NumMonthsPriorToRetirementToBeginRebalance = 57,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("25fcf260-666f-4e2e-ade1-5b272ce2d919"), ParentAId = Guid.Parse("7c26aa95-eebe-4d6a-b077-58037c10fbe4"),ParentBId = Guid.Parse("246fcb2d-c749-460b-85d9-de6afa2dff87"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 12:25:10 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 12137M,AusterityRatio = 0.96M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("3/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 0M,MonthlyInvest401kTraditional = 2583.33M,MonthlyInvestBrokerage = 1501.83M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 3,NumMonthsMidBucketOnHand = 31,NumMonthsPriorToRetirementToBeginRebalance = 18,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1M},
        //         new McModel() {PersonId = Guid.Parse("2ef4fa9e-b33b-4678-9d63-e589d914c886"),Id = Guid.Parse("7f28b9cd-2d01-48b5-a131-7d01ba7d0c65"), ParentAId = Guid.Parse("45721b19-1629-4e12-a9f9-4ede4d78c945"),ParentBId = Guid.Parse("72f1cce9-4fbe-48fb-8b9c-9437334e2d37"),ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Parse("2/25/2025 7:09:04 AM")),SimStartDate = startDate,SimEndDate = endDate,RetirementDate = LocalDateTime.FromDateTime(DateTime.Parse("2/1/2037 12:00:00 AM")),DesiredMonthlySpend = 15002M,AusterityRatio = 0.77M,ExtremeAusterityRatio = 0.47M,ExtremeAusterityNetWorthTrigger = 507803M,SocialSecurityStart = LocalDateTime.FromDateTime(DateTime.Parse("4/1/2041 12:00:00 AM")),MonthlyInvest401kRoth = 376M,MonthlyInvest401kTraditional = 2207M,MonthlyInvestBrokerage = 1440.87M,MonthlyInvestHSA = 712.5M,RebalanceFrequency = RebalanceFrequency.YEARLY,NumMonthsCashOnHand = 4,NumMonthsMidBucketOnHand = 45,NumMonthsPriorToRetirementToBeginRebalance = 66,RecessionCheckLookBackMonths = 13,RecessionRecoveryPointModifier = 1.08M},
        // ];
        using var context = new PgContext();
        // foreach (var champion in champions)
        // {
        //     context.McModels.Add(champion);
        //     context.SaveChanges();
        // }
        var champ = context.McModels
                        .Where(x => x.Id == Guid.Parse("7f28b9cd-2d01-48b5-a131-7d01ba7d0c65"))
                        .FirstOrDefault() ??
                    throw new InvalidDataException();
        return champ;
    }
    public static McPerson GetPerson()
    {
        using var context = new PgContext();
        var danPg = context.PgPeople
                        .FirstOrDefault(x => x.Name == "Dan") ?? 
                    throw new InvalidDataException();
        var danMc = new McPerson()
        {
            Id = danPg.Id,
            Name = "Dan",
            BirthDate = danPg.BirthDate,
            AnnualSalary = danPg.AnnualSalary,
            AnnualBonus = danPg.AnnualBonus,
            MonthlyFullSocialSecurityBenefit = danPg.MonthlyFullSocialSecurityBenefit,
            Annual401kMatchPercent = danPg.Annual401kMatchPercent,
            InvestmentAccounts = GetInvestmentAccounts(),
            DebtAccounts = GetDebtAccounts(),
        };
        return danMc;
    }
    
    
    

    private static List<McDebtAccount> GetDebtAccounts()
    {
        List<McDebtAccount> accounts = [];
        using var context = new PgContext();
        var accountsPg = (
            context.PgDebtAccounts
            ?? throw new InvalidDataException()
        ).ToList();
        foreach (var accountPg in accountsPg)
        {
            var positionsPg = GetOpenDebtPositionsByAccountId(
                accountPg.Id, accountPg.AnnualPercentageRate, accountPg.MonthlyPayment);
            if (!positionsPg.Any()) continue;
            accounts.Add(new McDebtAccount()
            {
                Id = Guid.NewGuid(),
                Name = accountPg.Name,
                Positions = positionsPg,
            });
        }

        return accounts;
    }

    private static List<McInvestmentAccount> GetInvestmentAccounts()
    {
        List<McInvestmentAccount> accounts = [];
        using var context = new PgContext();
        var accountsPg = context.PgInvestmentAccounts
            ?? throw new InvalidDataException();
        foreach (var accountPg in accountsPg)
        {
            var positionsPg = GetOpenInvestmentPositionsByAccountId(accountPg.Id);
            if (!positionsPg.Any()) continue;
            accounts.Add(new McInvestmentAccount()
            {
                Id = Guid.NewGuid(),
                Name = accountPg.Name,
                AccountType = GetAccountType(accountPg.TaxBucketId, accountPg.InvestmentAccountGroupId),
                Positions = positionsPg,
            });
        }
        // the Monte Carlo sim considers cash accounts as investment
        // positions with a type of CASH so add any cash here 
        var currentCash = GetCurrentCashTotal();
        accounts.Add(new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Cash",
            AccountType = McInvestmentAccountType.CASH,
            Positions = [new McInvestmentPosition()
            {
                Id = Guid.NewGuid(),
                IsOpen  = true,
                Name = "cash",
                Entry = new LocalDateTime(2025, 2, 1, 0, 0), // entry date doesn't matter here
                InvenstmentPositionType = McInvestmentPositionType.SHORT_TERM,
                InitialCost = currentCash,
                Quantity  = currentCash,
                Price  = 1,
            }],
        });
        return accounts;
    }
    
    private static decimal GetCurrentCashTotal()
    {
        using var context = new PgContext();
        decimal currentCash = 0.0M;
        
        // get the max date by symbol
        var maxDateByAccount = (
            from p in context.PgCashPositions
            group p by p.CashAccountId
            into g
            select new { g.Key, maxdate = g.Max(x => x.PositionDate) }
            ).ToList();
        // get any positions at max date
        foreach (var maxDate in maxDateByAccount)
        {
            var positionAtMaxDate = context.PgCashPositions
                .Where(x => 
                    x.PositionDate == maxDate.maxdate && 
                    x.CashAccountId == maxDate.Key)
                .OrderByDescending(x => x.CurrentBalance)
                .FirstOrDefault()
                ??  throw new InvalidDataException();
            if (positionAtMaxDate.CurrentBalance >= 0)
            {
                currentCash += positionAtMaxDate.CurrentBalance;
            }
        }
        
        return currentCash;
    }

    private static List<McDebtPosition> GetOpenDebtPositionsByAccountId(int accountId, decimal apr, decimal payment)
    {
        List<McDebtPosition> positions = [];
        using var context = new PgContext();
        
        var latestPosition = context.PgDebtPositions
            .Where(x => x.DebtAccountId == accountId)
            .OrderByDescending(x => x.PositionDate)
            .FirstOrDefault()
            ??  throw new InvalidDataException();
        if (latestPosition == null || latestPosition.CurrentBalance <= 0) return positions;
        
            
                positions.Add(new McDebtPosition()
                {
                    Id = Guid.NewGuid(),
                    IsOpen = true,
                    Name = $"Position {latestPosition.CurrentBalance}",
                    Entry = latestPosition.PositionDate,
                    CurrentBalance = latestPosition.CurrentBalance,
                    MonthlyPayment = payment,
                    AnnualPercentageRate = apr
                });
            
        
        
        return positions;
    }
    private static List<McInvestmentPosition> GetOpenInvestmentPositionsByAccountId(int accountId)
    {
        List<McInvestmentPosition> positions = [];
        using var context = new PgContext();
        
        // get the max date by symbol
        var maxDateBySymbol = (
            from p in context.PgPositions
            where p.InvestmentAccountId == accountId
            group p by p.Symbol
            into g
            select new { g.Key, maxdate = g.Max(x => x.PositionDate) })
        .ToList();
        foreach (var maxDate in maxDateBySymbol)
        {
            // order by totalQuantity and pick the first so that anytime we have a
            // positive quantity and a zero quantity, we'll know we picked the right
            // one (the zero means we closed it out)
            var positionAtMaxDate =
                context.PgPositions
                    .Where(x =>
                        x.PositionDate == maxDate.maxdate &&
                        x.InvestmentAccountId == accountId &&
                        x.Symbol == maxDate.Key)
                    .OrderBy(x => x.TotalQuantity)
                    .FirstOrDefault()
                ?? throw new InvalidDataException();
            if (positionAtMaxDate.TotalQuantity > 0)
            {
                positions.Add(new McInvestmentPosition()
                {
                    Id = Guid.NewGuid(),
                    IsOpen = true,
                    Name = $"Position {maxDate.Key}",
                    Entry = positionAtMaxDate.PositionDate,
                    InvenstmentPositionType = GetInvestmentPositionType(maxDate.Key),
                    InitialCost = positionAtMaxDate.CostBasis,
                    Quantity = positionAtMaxDate.TotalQuantity,
                    Price = positionAtMaxDate.Price,
                });
            }
        }
        
        return positions;
    }

    private static McInvestmentPositionType GetInvestmentPositionType(string symbol)
    {
        if (symbol == "SCHD") return McInvestmentPositionType.MID_TERM;
        return McInvestmentPositionType.LONG_TERM;
    }
    private static McInvestmentAccountType GetAccountType(int taxBucket, int accountGroup)
    {
        // tax buckets
        // 1	"Tax deferred"
        // 2	"Tax free HSA"
        // 3	"Tax free Roth"
        // 4	"Tax on capital gains"

        // account groups
        // 1	"Dan's 401(k)"
        // 2	"Dan's IRAs"
        // 3	"Jodi's IRAs"
        // 4	"Brokerage Account"
        // 5	"Home Equity"
        // 6	"Health Equity"

        // McInvestmentAccountType
        // TAXABLE_BROKERAGE = 0,
        // TRADITIONAL_401_K = 1,
        // ROTH_401_K = 2,
        // TRADITIONAL_IRA = 3,
        // ROTH_IRA = 4,
        // HSA = 5,
        // PRIMARY_RESIDENCE = 6,
        // CASH = 7,
        
        if (taxBucket == 2) return McInvestmentAccountType.HSA;
        if (taxBucket == 1 && accountGroup == 1) return McInvestmentAccountType.TRADITIONAL_401_K;
        if (taxBucket == 3 && accountGroup == 1) return McInvestmentAccountType.ROTH_401_K;
        if (taxBucket == 3 && accountGroup == 2) return McInvestmentAccountType.ROTH_IRA;
        if (taxBucket == 1 && accountGroup == 2) return McInvestmentAccountType.TRADITIONAL_IRA;
        if (taxBucket == 4 && accountGroup == 4) return McInvestmentAccountType.TAXABLE_BROKERAGE;
        if (taxBucket == 4 && accountGroup == 5) return McInvestmentAccountType.PRIMARY_RESIDENCE;
        if (taxBucket == 1 && accountGroup == 3) return McInvestmentAccountType.TRADITIONAL_IRA;
        return McInvestmentAccountType.CASH;
    }
}