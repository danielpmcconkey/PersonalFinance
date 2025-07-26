using NodaTime;

namespace Lib.DataTypes.MonteCarlo
{
    
    public record McPerson
    {
        public  Guid Id { get; set; }
        public  string Name { get; set; }
        public  LocalDateTime BirthDate {  get; set; }

        
        public  decimal AnnualSalary { get; set; }
        public  decimal AnnualBonus { get; set; } // includes AIP and RSU payout
        public decimal Annual401KContribution { get; set; }
        public decimal Annual401KMatchPercent { get; set; }
        public decimal AnnualHsaContribution { get; set; }
        public decimal AnnualHsaEmployerContribution { get; set; }
        public decimal FederalAnnualWithholding { get; set; }
        public decimal StateAnnualWithholding { get; set; }
        public decimal PreTaxHealthDeductions { get; set; }
        public decimal PostTaxInsuranceDeductions { get; set; }
        public  decimal MonthlyFullSocialSecurityBenefit { get; set; }
      

        #region Calculated and reference fields

        public bool IsRetired { get; set; } = false;
        public bool IsBankrupt { get; set; } = false;
        /// <summary>
        /// This is the calculated amount used within the sim that will be based on the retirement date
        /// </summary>
        public decimal AnnualSocialSecurityWage = 0M;
        public decimal Annual401KPreTax = 0M;
        public decimal Annual401KPostTax = 0M;

        #endregion
        
        /// <summary>
        /// This is the pre-determined "full" SSN benefit, used to calculate the actual monthly benefit that will be
        /// used within the sim
        /// </summary>
        
       
        public List<McInvestmentAccount> InvestmentAccounts { get; set; } = [];
        public List<McDebtAccount> DebtAccounts { get; set; } = [];
        public List<McModel> Models { get; set; } = [];
        
    }
}