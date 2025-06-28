

using NodaTime;

namespace Lib.DataTypes.MonteCarlo
{
    
    public record McPerson
    {
        public  Guid Id { get; set; }
        public  string Name { get; set; }
        public  LocalDateTime BirthDate {  get; set; }
        public  long AnnualSalary { get; set; }
        public  long AnnualBonus { get; set; }
        public  long MonthlyFullSocialSecurityBenefit { get; set; }
        public long Annual401kMatchPercent { get; set; }
        public List<McInvestmentAccount> InvestmentAccounts { get; set; } = [];
        public List<McDebtAccount> DebtAccounts { get; set; } = [];
        public List<McModel> Models { get; set; } = [];
    }
}
