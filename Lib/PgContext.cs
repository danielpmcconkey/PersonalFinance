using Lib.DataTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
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
        public DbSet<DataTypes.MonteCarlo.McModel> McModels { get; set; }
        
        
        
        
        
        
        
        
        // public DbSet<InvestmentAccount> InvestmentAccounts { get; set; }
        // public DbSet<DebtAccount> DebtAccounts { get; set; }
        // public DbSet<InvestmentPosition> InvestmentPositions { get; set; }
        // public DbSet<DebtPosition> DebtPositions { get; set; }
        public DbSet<PgCategory> Categories { get; set; }

        private readonly string _connectionstring;
        private static NpgsqlDataSource dataSource;
        private static bool hasDataSourceBeenBuilt = false;


        #endregion

        public PgContext()
        {
            _connectionstring = GetConnectionString();
            if (hasDataSourceBeenBuilt) return;
            
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionstring);
            dataSourceBuilder.UseNodaTime();
            dataSource = dataSourceBuilder.Build();
            hasDataSourceBeenBuilt = true;

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
            

            options.UseNpgsql(dataSource, o => o
                .SetPostgresVersion(17, 4)
                .UseNodaTime()
                    );
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<McModel>(e =>
            {
                e.HasKey(m => m.Id);
                e.HasOne(m => m.ParentA)
                    .WithMany(mm => mm.ChildrenA)
                    .HasForeignKey(m => m.ParentAId);
                e.HasOne(m => m.ParentB)
                    .WithMany(mm => mm.ChildrenB)
                    .HasForeignKey(m => m.ParentBId);
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
                e.HasOne(f => f.FundType1)
                    .WithMany(ft => ft.FundsOfType1)
                    .HasForeignKey(f => f.FundType1Id);
                e.HasOne(f => f.FundType2)
                    .WithMany(ft => ft.FundsOfType2)
                    .HasForeignKey(f => f.FundType2Id);
                e.HasOne(f => f.FundType3)
                    .WithMany(ft => ft.FundsOfType3)
                    .HasForeignKey(f => f.FundType3Id);
                e.HasOne(f => f.FundType4)
                    .WithMany(ft => ft.FundsOfType4)
                    .HasForeignKey(f => f.FundType4Id);
                e.HasOne(f => f.FundType5)
                    .WithMany(ft => ft.FundsOfType5)
                    .HasForeignKey(f => f.FundType5Id);
            });
            modelBuilder.Entity<PgFundType>(e =>
            {
                e.HasKey(ft => ft.Id);
                e.HasMany(ft => ft.FundsOfType1)
                    .WithOne(f => f.FundType1)
                    .HasForeignKey(f => f.FundType1Id);
                e.HasMany(ft => ft.FundsOfType2)
                    .WithOne(f => f.FundType2)
                    .HasForeignKey(f => f.FundType2Id);
                e.HasMany(ft => ft.FundsOfType3)
                    .WithOne(f => f.FundType3)
                    .HasForeignKey(f => f.FundType3Id);
                e.HasMany(ft => ft.FundsOfType4)
                    .WithOne(f => f.FundType4)
                    .HasForeignKey(f => f.FundType4Id);
                e.HasMany(ft => ft.FundsOfType5)
                    .WithOne(f => f.FundType5)
                    .HasForeignKey(f => f.FundType5Id);
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
            //     modelBuilder.Entity<DataTypes.MonteCarlo.Person>(e =>
            //     {
            //         e.HasKey(p => p.Id);
            //         e.HasMany(p => p.InvestmentAccounts)
            //             .WithOne(a => a.Person)
            //             .HasForeignKey(a => a.PersonId);
            //         e.HasMany(p => p.DebtAccounts)
            //             .WithOne(a => a.Person)
            //             .HasForeignKey(a => a.PersonId);
            //         e.HasMany(p => p.Models)
            //             .WithOne(m => m.Person)
            //             .HasForeignKey(m => m.PersonId);
            //     });
            //     modelBuilder.Entity<DataTypes.MonteCarlo.Model>(e =>
            //     {
            //         e.HasKey(m => m.Id);
            //         e.HasOne(m => m.Person)
            //             .WithMany(p => p.Models)
            //             .HasForeignKey(m => m.PersonId)
            //             .OnDelete(DeleteBehavior.Cascade);
            //         e.HasMany(m => m.BatchResults)
            //             .WithOne(br => br.Model)
            //             .HasForeignKey(br => br.ModelId);
            //         e.Property(m => m.RebalanceFrequency).HasConversion<int>();
            //     });
            //     modelBuilder.Entity<DataTypes.MonteCarlo.InvestmentAccount>(e =>
            //     {
            //         e.HasKey(a => a.Id);
            //         e.HasMany(a => a.Positions)
            //             .WithOne(p => p.InvestmentAccount)
            //             .HasForeignKey(p => p.InvestmentAccountId);
            //         e.Property(a => a.AccountType).HasConversion<int>();
            //     });
            //     modelBuilder.Entity<DataTypes.MonteCarlo.DebtAccount>(e =>
            //     {
            //         e.HasKey(a => a.Id);
            //         e.HasMany(a => a.Positions)
            //             .WithOne(p => p.DebtAccount)
            //             .HasForeignKey(p => p.DebtAccountId);
            //     });
            //     modelBuilder.Entity<DataTypes.MonteCarlo.DebtPosition>(e =>
            //     {
            //         e.HasKey(p => p.Id);
            //         e.HasOne(p => p.DebtAccount)
            //             .WithMany(a => a.Positions)
            //             .HasForeignKey(p => p.DebtAccountId)
            //             .OnDelete(DeleteBehavior.Cascade);
            //     });
            //     modelBuilder.Entity<DataTypes.MonteCarlo.InvestmentPosition>(e =>
            //     {
            //         e.HasKey(p => p.Id);
            //         e.HasOne(p => p.InvestmentAccount)
            //             .WithMany(a => a.Positions)
            //             .HasForeignKey(p => p.InvestmentAccountId)
            //             .OnDelete(DeleteBehavior.Cascade);
            //         e.Property(a => a.InvenstmentPositionType).HasConversion<int>();
            //     });
        }
    }
}
