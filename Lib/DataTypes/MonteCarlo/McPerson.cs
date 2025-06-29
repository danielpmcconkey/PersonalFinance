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
        /// <summary>
        /// This is the pre-determined "full" SSN benefit, used to calculate the actual monthly benefit that will be
        /// used within the sim
        /// </summary>
        public  decimal MonthlyFullSocialSecurityBenefit { get; set; }
        public decimal Annual401kMatchPercent { get; set; }
        public List<McInvestmentAccount> InvestmentAccounts { get; set; } = [];
        public List<McDebtAccount> DebtAccounts { get; set; } = [];
        public List<McModel> Models { get; set; } = [];
        public bool IsRetired { get; set; } = false;
        public bool IsBankrupt { get; set; } = false;
        /// <summary>
        /// This is the calculated amount used within the sim that will be based on the retirement date
        /// </summary>
        public decimal MonthlySocialSecurityWage = 0M;
        public decimal Monthly401kMatch = 0M;
    }
}