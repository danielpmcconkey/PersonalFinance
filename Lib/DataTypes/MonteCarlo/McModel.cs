using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NodaTime;

namespace Lib.DataTypes.MonteCarlo
{
    [Table("montecarlomodel", Schema = "personalfinance")]
    [PrimaryKey(nameof(Id))]
    public record McModel
    {
        [Column("id")]
        public required Guid Id { get; set; }
        
        [Column("personid")]
        public required Guid PersonId { get; set; }
        public McPerson? Person { get; set; }
        public List<BatchResult> BatchResults { get; set; } = [];
        
        [Column("parenta")]
        public required Guid ParentAId {  get; set; }
        public McModel? ParentA { get; set; }
        public List<McModel> ChildrenA { get; set; } = [];
        
        [Column("parentb")]
        public required Guid ParentBId { get; set; }
        public McModel? ParentB { get; set; }
        public List<McModel> ChildrenB { get; set; } = [];
        
        [Column("modelcreateddate")]
        public required LocalDateTime ModelCreatedDate { get; set; }
        
        [Column("simstartdate")]
        public required LocalDateTime SimStartDate { get; set; }
        
        [Column("simenddate")]
        public required LocalDateTime SimEndDate { get; set; }
        
        [Column("retirementdate")]
        public required LocalDateTime RetirementDate { get; set; }
        
        [Column("socialsecuritystart")]
        public required LocalDateTime SocialSecurityStart { get; set; }
        
        [Column("desiredmonthlyspend")]
        public required long DesiredMonthlySpend { get; set; }

        /// <summary>
        /// the percentage of monthly spend that you actually spend when times 
        /// are tough
        /// </summary>
        [Column("austerityratio")]
        public required long AusterityRatio { get; set; }
        /// <summary>
        /// the percentage of monthly spend that you actually spend when times 
        /// are really tough
        /// </summary>
        [Column("extremeausterityratio")]
        public required long ExtremeAusterityRatio { get; set; }
        /// <summary>
        /// If net worth is below this level, you go into extreme austerity
        /// </summary>
        [Column("extremeausteritynetworthtrigger")]
        public required long ExtremeAusterityNetWorthTrigger { get; set; }

        [Column("monthlyinvest401kroth")]
        public required long MonthlyInvest401kRoth { get; set; }

        [Column("monthlyinvest401ktraditional")]
        public required long MonthlyInvest401kTraditional { get; set; }

        [Column("monthlyinvestbrokerage")]
        public required long MonthlyInvestBrokerage { get; set; }

        [Column("monthlyinvesthsa")]
        public required long MonthlyInvestHSA { get; set; }
        
        [Column("rebalancefrequency")]
        public required RebalanceFrequency RebalanceFrequency { get; set; }
        
        [Column("nummonthscashonhand")]
        public required int NumMonthsCashOnHand { get; set; }
        
        [Column("nummonthsmidbucketonhand")]
        public required int NumMonthsMidBucketOnHand { get; set; }
        
        [Column("nummonthspriortoretirementtobeginrebalance")]
        public required int NumMonthsPriorToRetirementToBeginRebalance { get; set; }
        
        [Column("recessionchecklookbackmonths")]
        public required int RecessionCheckLookBackMonths { get; set; }

        [Column("recessionrecoverypointmodifier")]
        public required long RecessionRecoveryPointModifier { get; set; }

    }
}
