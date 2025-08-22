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
        //public McPerson? Person { get; set; }
        //public List<SimulationAllLivesResult> BatchResults { get; set; } = [];
        
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
        
        /// <summary>
        /// the percentage of desired monthly spend that you actually spend when in a recession 
        /// </summary>
        [Column("austerityratio")]
        public required decimal AusterityRatio { get; set; }
        
        /// <summary>
        /// the percentage of monthly spend that you actually spend when times 
        /// are really tough
        /// </summary>
        [Column("extremeausterityratio")]
        public required decimal ExtremeAusterityRatio { get; set; }
        
        /// <summary>
        /// If net worth is below this level, you go into extreme austerity
        /// </summary>
        [Column("extremeausteritynetworthtrigger")]
        public required decimal ExtremeAusterityNetWorthTrigger { get; set; }
        
        /// <summary>
        /// the percentage of desired monthly spend that you actually spend when in livin' large mode 
        /// </summary>
        [Column("livinlargeratio", TypeName = "numeric(5,4)")]
        public required decimal LivinLargeRatio { get; set; }
        
        /// <summary>
        /// If net worth is above this level, you go into livin' large mode
        /// </summary>
        [Column("livinlargenetworthtrigger", TypeName = "numeric(18,4)")]
        public required decimal LivinLargeNetWorthTrigger { get; set; }
        
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

        /// <summary>
        /// what percentage of the previous high water mark (recession recover point) you need the current prices to be
        /// before declaring yourself done with the recession eg:
        ///     isRecessionOver = (currentPrice > recessionRecoveryPoint * recessionRecoveryPointModifier) ? 
        ///          true : false
        /// </summary>
        [Column("recessionrecoverypointmodifier")]
        public required decimal RecessionRecoveryPointModifier { get; set; }
        
        /// <summary>
        /// the amount of fun bucks you get to blow pre retirement (above + beyond required spend)
        /// </summary>
        [Column("desiredmonthlyspendpreretirement")]
        public required decimal DesiredMonthlySpendPreRetirement { get; set; }
        
        /// <summary>
        /// the amount of fun bucks you get to blow post retirement (above + beyond required spend)
        /// </summary>
        [Column("desiredmonthlyspendpostretirement")]
        public required decimal DesiredMonthlySpendPostRetirement { get; set; }

        /// <summary>
        /// the percentage of teh overall employee contribution goes to teh traditional 401K account
        /// </summary>
        [Column("percent401ktraditional", TypeName = "numeric(5,4)")]
        public required decimal Percent401KTraditional { get; set; }
        
        [Column("generation")]
        public required int Generation { get; set; }

    }
}