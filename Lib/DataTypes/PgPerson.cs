using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using NodaTime;

namespace Lib.DataTypes
{
    [Table("person", Schema = "personalfinance")]
    [PrimaryKey(nameof(Id))]
    public record PgPerson
    {
        [Column("id")]
        public required Guid Id { get; set; }
        
        [Column("name", TypeName = "varchar(100)")]
        public required string Name { get; set; }
        
        [Column("birthdate")]
        public required LocalDateTime BirthDate {  get; set; }
        
        [Column("annualsalary",TypeName = "numeric(12,2)")]
        public required decimal AnnualSalary { get; set; }
        
        /// <summary>
        ///  includes AIP and RSU payout
        /// </summary>
        [Column("annualbonus", TypeName = "numeric(12,2)")]
        public required decimal AnnualBonus { get; set; }
        
        [Column("annual401kmatchpercent",TypeName = "numeric(5,4)")]
        public required decimal Annual401KMatchPercent { get; set; }
        
        [Column("monthlyfullsocialsecuritybenefit",TypeName = "numeric(12,2)")]
        public required decimal MonthlyFullSocialSecurityBenefit { get; set; }
        
        [Column("annual401kcontribution", TypeName = "numeric(12,2)")]
        public required decimal Annual401KContribution { get; set; }
        
        [Column("annualhsacontribution", TypeName = "numeric(12,2)")] 
        public required decimal AnnualHsaContribution { get; set; }
        
        [Column("annualhsaemployercontribution", TypeName = "numeric(12,2)")] 
        public required decimal AnnualHsaEmployerContribution { get; set; }

        [Column("federalannualwithholding", TypeName = "numeric(12,2)")] 
        public required decimal FederalAnnualWithholding { get; set; }
        
        [Column("stateannualwithholding", TypeName = "numeric(12,2)")] 
        public required decimal StateAnnualWithholding { get; set; }

        [Column("pretaxhealthdeductions", TypeName = "numeric(12,2)")] 
        public required decimal PreTaxHealthDeductions { get; set; }
        
        [Column("posttaxinsurancedeductions", TypeName = "numeric(12,2)")] 
        public required decimal PostTaxInsuranceDeductions { get; set; }
        
        /// <summary>
        /// the amount you have to pay on basic things like groceries, bills, property tax. does not include any debt payments
        /// </summary>
        [Column("requiredmonthlyspend")]
        public required decimal RequiredMonthlySpend { get; set; }
        
        /// <summary>
        /// the amount you have to pay for health care once you retire. Changes based on age
        /// </summary>
        [Column("requiredmonthlyspendhealthcare")]
        public required decimal RequiredMonthlySpendHealthCare { get; set; }
        
        #region Calculated and reference fields

        [NotMapped]
        public bool IsRetired { get; set; } = false;
        [NotMapped]
        public bool IsBankrupt { get; set; } = false;
        /// <summary>
        /// This is the calculated amount used within the sim that will be based on the retirement date
        /// </summary>
        [NotMapped]
        public decimal AnnualSocialSecurityWage = 0M;
        [NotMapped]
        public decimal Annual401KPreTax = 0M;
        [NotMapped]
        public decimal Annual401KPostTax = 0M;
        #endregion
    }
}