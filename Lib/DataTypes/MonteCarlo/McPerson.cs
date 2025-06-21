

using NodaTime;

namespace Lib.DataTypes.MonteCarlo
{
    
    public record McPerson
    {
        public  Guid Id { get; set; }
        public  string Name { get; set; }
        public  LocalDateTime BirthDate {  get; set; }
        public  decimal AnnualSalary { get; set; }
        public  decimal AnnualBonus { get; set; }
        public  decimal MonthlyFullSocialSecurityBenefit { get; set; }
        public decimal Annual401kMatchPercent { get; set; }
        public List<McInvestmentAccount> InvestmentAccounts { get; set; } = [];
        public List<McDebtAccount> DebtAccounts { get; set; } = [];
        public List<McModel> Models { get; set; } = [];
    }
}
