using Lib.DataTypes.MonteCarlo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using NodaTime;

namespace Lib.MonteCarlo
{
    public class BatchManager
    {
        private Logger _logger;
        private int _numGenerations;
        private McPerson _mcPerson;
        private McModel _mcModel;
        //private decimal[] _sAndP500HistoricalTrends;
        private Dictionary<LocalDateTime, Decimal>[] _hypotheticalPrices;
        private List<McInvestmentAccount> _investmentAccounts;
        private List<McDebtAccount> _debtAccounts;
        private bool _shouldRunParallel;
        private CorePackage _corePackage;

        public BatchManager(CorePackage corePackage,int numGenerations,McPerson mcPerson,
            McModel mcModel, Dictionary<LocalDateTime, Decimal>[] hypotheticalPrices) 
        {
            _corePackage = corePackage;
            _logger = _corePackage.Log;
            _shouldRunParallel= _corePackage.ShouldRunParallel;
            _numGenerations = numGenerations; 
            _mcPerson = mcPerson;
            _mcModel = mcModel;
            _debtAccounts = mcPerson.DebtAccounts;
            _investmentAccounts = mcPerson.InvestmentAccounts;
            _hypotheticalPrices = hypotheticalPrices;
        }
        public List<BatchResult> Run()
        {
            List<NetWorthMeasurement>[] runs = new List<NetWorthMeasurement>[_numGenerations];
            if(_shouldRunParallel) Parallel.For(0, _numGenerations, i =>
            {
                List<McInvestmentAccount> investmentAccounts = CopyInvestmentAccounts();
                List<McDebtAccount> debtAccounts = CopyDebtAccounts();
                Simulator sim = new(_corePackage,_mcModel, _mcPerson, investmentAccounts,
                    debtAccounts, _hypotheticalPrices[i]);
                runs[i] = sim.Run();
            });
            else for(int i = 0; i < _numGenerations; i++)
            {
                List<McInvestmentAccount> investmentAccounts = CopyInvestmentAccounts();
                List<McDebtAccount> debtAccounts = CopyDebtAccounts();
                Simulator sim = new(_corePackage, _mcModel, _mcPerson, investmentAccounts, debtAccounts,
                    _hypotheticalPrices[i]);
                runs[i] = sim.Run();
                //Logger.Info($"total spend: {runs[i][479].TotalSpend.ToString("C")}");
            }
            List<NetWorthMeasurement> allMeasurements = [];
            for (int i = 0; i < _numGenerations; i++) 
                allMeasurements.AddRange(runs[i]);
            return GetResults(allMeasurements);
        }
        private List<BatchResult> GetResults(List<NetWorthMeasurement> allMeasurements)
        {
            List<BatchResult> batchResults = [];
            var minDate = allMeasurements.Min(x => x.MeasuredDate);
            var maxDate = allMeasurements.Max(x => x.MeasuredDate);
            LocalDateTime dateCursor = minDate;
            int totalBankruptcies = 0;
            while (dateCursor <= maxDate)
            {
                // get all the total spend measurements for this date
                NetWorthMeasurement[] valuesAtDate = allMeasurements
                    .Where(x => x.MeasuredDate == dateCursor)
                    .OrderBy(x => x.TotalSpend)
                    .ToArray();

                // total bankruptcies is a running list of all bankruptcies so
                // far. it will grow as the date cursor moves forward
                totalBankruptcies += valuesAtDate.Where(x => x.NetWorth <= 0).Count();
                var simAt90PercentileSpend = GetPercentileValue(valuesAtDate, 0.9M);
                var simAt75PercentileSpend = GetPercentileValue(valuesAtDate, 0.75M);
                var simAt50PercentileSpend = GetPercentileValue(valuesAtDate, 0.5M);
                var simAt25PercentileSpend = GetPercentileValue(valuesAtDate, 0.25M);
                var simAt10PercentileSpend = GetPercentileValue(valuesAtDate, 0.1M);
                batchResults.Add(new BatchResult()
                {
                    Id = Guid.NewGuid(),
                    ModelId = _mcModel.Id,
                    MeasuredDate = dateCursor,
                    NetWorthAt90thPercentile = simAt90PercentileSpend.NetWorth,
                    NetWorthAt75thPercentile = simAt75PercentileSpend.NetWorth,
                    NetWorthAt50thPercentile = simAt50PercentileSpend.NetWorth,
                    NetWorthAt25thPercentile = simAt25PercentileSpend.NetWorth,
                    NetWorthAt10thPercentile = simAt10PercentileSpend.NetWorth,
                    SpendAt90thPercentile = simAt90PercentileSpend.TotalSpend,
                    SpendAt75thPercentile = simAt75PercentileSpend.TotalSpend,
                    SpendAt50thPercentile = simAt50PercentileSpend.TotalSpend,
                    SpendAt25thPercentile = simAt25PercentileSpend.TotalSpend,
                    SpendAt10thPercentile = simAt10PercentileSpend.TotalSpend,
                    TaxesAt90thPercentile = simAt90PercentileSpend.TotalTax,
                    TaxesAt75thPercentile = simAt75PercentileSpend.TotalTax,
                    TaxesAt50thPercentile = simAt50PercentileSpend.TotalTax,
                    TaxesAt25thPercentile = simAt25PercentileSpend.TotalTax,
                    TaxesAt10thPercentile = simAt10PercentileSpend.TotalTax,
                    BankruptcyRate = (1.0M * totalBankruptcies) / (1.0M * allMeasurements.Count),
                });
                dateCursor = dateCursor.PlusMonths(1);
            }
            return batchResults;
        }
        private NetWorthMeasurement GetPercentileValue(NetWorthMeasurement[] sequence,
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
        private List<McInvestmentAccount> CopyInvestmentAccounts()
        {
            Func<List<McInvestmentPosition>, Guid, List<McInvestmentPosition>>
                CopyPositions = (positions, newAccountId) =>
                {
                    List<McInvestmentPosition> newList = [];
                    foreach (McInvestmentPosition p in positions)
                    {
                        newList.Add(new McInvestmentPosition()
                        {
                            Id = Guid.NewGuid(),
                            // InvestmentAccountId = newAccountId,
                            IsOpen = p.IsOpen,
                            Name = p.Name,
                            Entry = p.Entry,
                            InvestmentPositionType = p.InvestmentPositionType,
                            InitialCost = p.InitialCost,
                            Quantity = p.Quantity,
                            Price = p.Price,
                        });
                    }
                    return newList;
                };
            List<McInvestmentAccount> newList = [];
            foreach (McInvestmentAccount a in _investmentAccounts)
            {
                var newAccountId = Guid.NewGuid();
                newList.Add(new()
                {
                    Id = Guid.NewGuid(),
                    // PersonId = a.PersonId,
                    Name = a.Name,
                    AccountType = a.AccountType,
                    Positions = CopyPositions(a.Positions, newAccountId),
                });
            }
            return newList;
        }
        private List<McDebtAccount> CopyDebtAccounts()
        {
            Func<List<McDebtPosition>, Guid, List<McDebtPosition>>
                CopyPositions = (positions, newAccountId) =>
                {
                    List<McDebtPosition> newList = [];
                    foreach (McDebtPosition p in positions)
                    {
                        newList.Add(new McDebtPosition()
                        {
                            Id = Guid.NewGuid(),
                            IsOpen = p.IsOpen,
                            Name = p.Name,
                            Entry = p.Entry,
                            AnnualPercentageRate = p.AnnualPercentageRate,
                            MonthlyPayment = p.MonthlyPayment,
                            CurrentBalance = p.CurrentBalance,
                        });
                    }
                    return newList;
                };
            List<McDebtAccount> newList = [];
            foreach (McDebtAccount a in _debtAccounts)
            {
                var newAccountId = Guid.NewGuid();
                newList.Add(new()
                {
                    Id = Guid.NewGuid(),
                    Name = a.Name,
                    Positions = CopyPositions(a.Positions, newAccountId),
                });
            }
            return newList;
        }

    }
}