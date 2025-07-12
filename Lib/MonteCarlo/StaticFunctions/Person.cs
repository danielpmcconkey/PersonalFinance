using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Person
{
    public static McPerson GetPersonById(Guid personId)
    {
        using var context = new PgContext();
        var pgperson = context.PgPeople.FirstOrDefault(x => x.Id == personId);
        if (pgperson is null) throw new InvalidDataException();
                
        var person = new McPerson()
        {
            Id = pgperson.Id,
            Name = pgperson.Name,
            BirthDate = pgperson.BirthDate,
            AnnualSalary = (pgperson.AnnualSalary),
            AnnualBonus = (pgperson.AnnualBonus),
            MonthlyFullSocialSecurityBenefit = (pgperson.MonthlyFullSocialSecurityBenefit),
            Annual401kMatchPercent = (pgperson.Annual401kMatchPercent),
            InvestmentAccounts = AccountDbRead.FetchDbInvestmentAccountsByPersonId(pgperson.Id),
            DebtAccounts = AccountDbRead.FetchDbDebtAccountsByPersonId(pgperson.Id),
        };
        return person;
    }

    public static decimal CalculateMonthlySocialSecurityWage(McPerson person, LocalDateTime benefitElectionStart)
    {
        const int fullRetirementAge = 67;
        const int maxMonthsEarly = 59;
        const int maxMonthsLate = 36;

        var fullRetirementDate = person.BirthDate.PlusYears(fullRetirementAge);
        if (benefitElectionStart == fullRetirementDate) return person.MonthlyFullSocialSecurityBenefit;

        if (benefitElectionStart < fullRetirementDate)
        {
            // we're taking it early and getting a credit
            var timeSpanEarly = (fullRetirementDate - benefitElectionStart);
            var monthsEarly = (timeSpanEarly.Years * 12) + timeSpanEarly.Months;

            if (monthsEarly > maxMonthsEarly)
                throw new InvalidDataException("can't claim social security wage before age 62 and 1 month");
            if (monthsEarly == 0)
                return person.MonthlyFullSocialSecurityBenefit; // this could be because the birthdate is on the 18th of the month, but the election start date is the 1st
            if (monthsEarly < 0)
                throw new InvalidOperationException("months early should never be negative");
            
            var maxWage = person.MonthlyFullSocialSecurityBenefit;

            decimal penalty = 0.0M;
            if (monthsEarly <= 36)
            {
                penalty += 0.01M * (5M / 9M) * monthsEarly;
            }

            else
            {
                penalty += 0.01M * (5M / 9M) * 36;
                penalty += 0.01M * (5M / 12M) * (monthsEarly - 36);
            }

            penalty = Math.Max(penalty, 0M); // don't want to add on to max if I made a date math error
            return maxWage - (maxWage * penalty);;
        }

        // we're taking it late and getting a credit for it
        var timeSpanLate = (benefitElectionStart - fullRetirementDate);
        var monthsLate = (timeSpanLate.Years * 12) + timeSpanLate.Months;
        
        if (monthsLate > maxMonthsLate)
            throw new InvalidDataException("shouldn't claim social security wage after age 70");
        if (monthsLate == 0)
            return person.MonthlyFullSocialSecurityBenefit; // this could be because the birthdate is on the 18th of the month, but the election start date is the 1st
        if (monthsLate < 0)
            throw new InvalidOperationException("months late should never be negative");
        
        var minWage = person.MonthlyFullSocialSecurityBenefit;
        decimal credit = (0.08m / 12m) * monthsLate;
        credit = Math.Max(credit, 0M); // don't want to subtract from min if I made a date math error
        return minWage + (minWage * credit);
    }

    public static decimal CalculateMonthly401kMatch(McPerson person)
    {
        return person.AnnualSalary * person.Annual401kMatchPercent / 12;
    }
    /// <summary>
    /// Used to create a new object with the same characteristics as the original so we don't have to worry about one
    /// sim run updating another's stats. Also reset calculated fields like MonthlySocialSecurityWage, IsRetired, etc.
    /// </summary>
    public static McPerson CopyPerson(McPerson originalPerson)
    {
        var newInvestmentAccounts = AccountCopy.CopyInvestmentAccounts(originalPerson.InvestmentAccounts);
        var newDebtAccounts = AccountCopy.CopyDebtAccounts(originalPerson.DebtAccounts);
        var newPerson = new McPerson()
        {
            Id = new (),
            Name = originalPerson.Name,
            BirthDate = originalPerson.BirthDate,
            AnnualSalary = originalPerson.AnnualSalary,
            AnnualBonus = originalPerson.AnnualBonus,
            MonthlyFullSocialSecurityBenefit = originalPerson.MonthlyFullSocialSecurityBenefit,
            Annual401kMatchPercent = originalPerson.Annual401kMatchPercent,
            InvestmentAccounts = newInvestmentAccounts,
            DebtAccounts = newDebtAccounts,
            IsRetired = false,
            IsBankrupt = false,
            MonthlySocialSecurityWage = 0M,
            Monthly401kMatch = 0M,
        };
        return newPerson;
    }
}