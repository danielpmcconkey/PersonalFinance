using Lib.DataTypes.Postgres;
using Microsoft.EntityFrameworkCore;
using Lib.DataTypes.MonteCarlo;
using Npgsql;

namespace Lib
{
    public class PgContext : DbContext
    {
        #region DBSets
        public DbSet<HistoricalGrowthRate> HistoricalGrowthRates { get; set; }
        public DbSet<PgCashAccount> PgCashAccounts { get; set; }
        public DbSet<PgCashPosition> PgCashPositions { get; set; }
        public DbSet<PgCategory> PgCategories { get; set; }
        public DbSet<PgDebtAccount> PgDebtAccounts { get; set; }
        public DbSet<PgDebtPosition> PgDebtPositions { get; set; }
        public DbSet<PgFund> PgFunds { get; set; }
        public DbSet<PgFundType> PgFundTypes { get; set; }
        public DbSet<PgInvestmentAccount> PgInvestmentAccounts { get; set; }
        public DbSet<PgInvestmentAccountGroup> PgInvestmentAccountGroups { get; set; }
        public DbSet<PgPerson> PgPeople { get; set; }
        public DbSet<PgPosition> PgPositions { get; set; }
        public DbSet<PgTaxBucket> PgTaxBuckets { get; set; }
        public DbSet<PgTransaction> PgTransactions { get; set; }
        public DbSet<DataTypes.MonteCarlo.Model> McModels { get; set; }
        public DbSet<SingleModelRunResult> SingleModelRunResults { get; set; }
        
        
        
        // public DbSet<InvestmentAccount> InvestmentAccounts { get; set; }
        // public DbSet<DebtAccount> DebtAccounts { get; set; }
        // public DbSet<InvestmentPosition> InvestmentPositions { get; set; }
        // public DbSet<DebtPosition> DebtPositions { get; set; }
        public DbSet<PgCategory> Categories { get; set; }

        private static NpgsqlDataSource? _dataSource;
        private static bool _hasDataSourceBeenBuilt = false;


        #endregion

        public PgContext()
        {
            var connectionString = GetConnectionString();
            if (_hasDataSourceBeenBuilt) return;
            
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseNodaTime();
            _dataSource = dataSourceBuilder.Build();
            _hasDataSourceBeenBuilt = true;

        }

        private string GetConnectionString()
        {
            string? pgPassHex = Environment.GetEnvironmentVariable("PGPASS");
            if(pgPassHex == null) throw new InvalidDataException("PGPASS environment variable not found");
            var converted = Convert.FromHexString(pgPassHex);
            string passNew = System.Text .Encoding.Unicode.GetString(converted);
            
            var connectionString = $"Host=localhost;Username=dansdev;Password='{passNew}';Database=householdbudget;" +
                          "Timeout=15;Command Timeout=300;";
            return connectionString;
        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (_dataSource is null) throw new InvalidOperationException("DataSource is null.");

            options.UseNpgsql(_dataSource, o => o
                .SetPostgresVersion(17, 4)
                .UseNodaTime()
                    );
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Model>(e =>
            {
                e.HasKey(m => m.Id);
                e.HasOne(m => m.ParentA)
                    .WithMany(mm => mm.ChildrenA)
                    .HasForeignKey(m => m.ParentAId);
                e.HasOne(m => m.ParentB)
                    .WithMany(mm => mm.ChildrenB)
                    .HasForeignKey(m => m.ParentBId);
                e.Property(m => m.WithdrawalStrategyType).HasConversion<int>();

            });
            modelBuilder.Entity<PgCashAccount>(e =>
            {
                e.HasKey(ca => ca.Id);
                e.HasMany(ca => ca.Positions)
                    .WithOne(po => po.CashAccount)
                    .HasForeignKey(po => po.CashAccountId);
            });
            modelBuilder.Entity<PgCashPosition>(e =>
            {
                e.HasKey(cp => cp.Id);
                e.HasOne(cp => cp.CashAccount)
                    .WithMany(ca => ca.Positions)
                    .HasForeignKey(cp => cp.CashAccountId);
            });
            modelBuilder.Entity<PgCategory>(e =>
            {
                e.HasKey(c => c.Id);
                e.HasOne(c => c.Parent)
                    .WithMany(p => p.ChildCategories)
                    .HasForeignKey(c => c.ParentId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasMany(c => c.Transactions)
                    .WithOne(t => t.Category)
                    .HasForeignKey(t => t.CategoryId);
            });
            modelBuilder.Entity<PgDebtAccount>(e =>
            {
                e.HasKey(da => da.Id);
                e.HasMany(da => da.Positions)
                    .WithOne(dp => dp.DebtAccount)
                    .HasForeignKey(dp => dp.DebtAccountId);
            });
            modelBuilder.Entity<PgDebtPosition>(e =>
            {
                e.HasKey(dp => dp.Id);
                e.HasOne(dp => dp.DebtAccount)
                    .WithMany(da => da.Positions)
                    .HasForeignKey(dp => dp.DebtAccountId);
            });
            modelBuilder.Entity<PgFund>(e =>
            {
                e.HasKey(f => f.Symbol);
                e.HasOne(f => f.InvestmentType)
                    .WithMany(ft => ft.FundsOfInvestmentType)
                    .HasForeignKey(f => f.InvestmentTypeId);
                e.HasOne(f => f.Size)
                    .WithMany(ft => ft.FundsOfSizeType)
                    .HasForeignKey(f => f.SizeId);
                e.HasOne(f => f.IndexOrIndividual)
                    .WithMany(ft => ft.FundsOfIndexOrIndividualType)
                    .HasForeignKey(f => f.IndexOrIndividualId);
                e.HasOne(f => f.Sector)
                    .WithMany(ft => ft.FundsOfSectorType)
                    .HasForeignKey(f => f.SectorId);
                e.HasOne(f => f.Region)
                    .WithMany(ft => ft.FundsOfRegionType)
                    .HasForeignKey(f => f.RegionId);
                e.HasMany(f => f.Positions)
                    .WithOne(p => p.Fund)
                    .HasForeignKey(p => p.Symbol);
            });
            modelBuilder.Entity<PgFundType>(e =>
            {
                e.HasKey(ft => ft.Id);
                e.HasMany(ft => ft.FundsOfInvestmentType)
                    .WithOne(f => f.InvestmentType)
                    .HasForeignKey(f => f.InvestmentTypeId);
                e.HasMany(ft => ft.FundsOfSizeType)
                    .WithOne(f => f.Size)
                    .HasForeignKey(f => f.SizeId);
                e.HasMany(ft => ft.FundsOfIndexOrIndividualType)
                    .WithOne(f => f.IndexOrIndividual)
                    .HasForeignKey(f => f.IndexOrIndividualId);
                e.HasMany(ft => ft.FundsOfSectorType)
                    .WithOne(f => f.Sector)
                    .HasForeignKey(f => f.SectorId);
                e.HasMany(ft => ft.FundsOfRegionType)
                    .WithOne(f => f.Region)
                    .HasForeignKey(f => f.RegionId);
                e.HasMany(ft => ft.FundsOfObjectiveType)
                    .WithOne(f => f.Objective)
                    .HasForeignKey(f => f.ObjectiveId);
            });
            modelBuilder.Entity<PgInvestmentAccount>(e =>
            {
                e.HasKey(ia => ia.Id);
                e.HasOne(ia => ia.TaxBucket)
                    .WithMany(tb => tb.InvestmentAccounts)
                    .HasForeignKey(ia => ia.TaxBucketId);
                e.HasOne(ia => ia.InvestmentAccountGroup)
                    .WithMany(iag => iag.InvestmentAccounts)
                    .HasForeignKey(ia => ia.InvestmentAccountGroupId);
                e.HasMany(ia => ia.Positions)
                    .WithOne(p => p.InvestmentAccount)
                    .HasForeignKey(p => p.InvestmentAccountId);
            });
            modelBuilder.Entity<PgInvestmentAccountGroup>(e =>
            {
                e.HasKey(ia => ia.Id);
                
                e.HasMany(iag => iag.InvestmentAccounts)
                    .WithOne(ia => ia.InvestmentAccountGroup)
                    .HasForeignKey(ia => ia.InvestmentAccountGroupId);
            });
            modelBuilder.Entity<PgPerson>(e =>
            {
                e.HasKey(p => p.Id);
            });
            modelBuilder.Entity<PgPosition>(e =>
            {
                e.HasKey(p => p.Id);
                e.HasOne(p => p.InvestmentAccount)
                    .WithMany(ia => ia.Positions)
                    .HasForeignKey(p => p.InvestmentAccountId);
                e.HasOne(p => p.Fund)
                    .WithMany(f => f.Positions)
                    .HasForeignKey(p => p.Symbol);
            });
            modelBuilder.Entity<PgTaxBucket>(e =>
            {
                e.HasKey(tb => tb.Id);
                e.HasMany(tb => tb.InvestmentAccounts)
                    .WithOne(ia => ia.TaxBucket)
                    .HasForeignKey(ia => ia.TaxBucketId);
            });
            modelBuilder.Entity<PgTransaction>(e =>
            {
                e.HasKey(t => t.Id);
                e.HasOne(t => t.Category)
                    .WithMany(c => c.Transactions)
                    .HasForeignKey(t => t.CategoryId);
            });
        }
    }
}
