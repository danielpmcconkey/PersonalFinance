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
        var maxWage = person.MonthlyFullSocialSecurityBenefit;
        var fullRetirementDate = person.BirthDate.PlusYears(67);
        var timeSpanEarly = (fullRetirementDate - benefitElectionStart);
        int monthsEarly = (int)Math.Round(
            timeSpanEarly.Days / 365.25 * 12, 0);
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
        var primaryWage = maxWage - (maxWage * penalty);
        return primaryWage;
    }

    public static decimal CalculateMonthly401kMatch(McPerson person)
    {
        return person.AnnualSalary * person.Annual401kMatchPercent / 12;
    }
    /// <summary>
    /// Used to create a new object with the same characteristics as the original so we don't have to worry about one
    /// sim run updating another's stats
    /// </summary>
    public static McPerson CopyPerson(McPerson originalPerson)
    {
        var newInvestmentAccounts = AccountCopy.CopyInvestmentAccounts(originalPerson.InvestmentAccounts);
        var newDebtAccounts = AccountCopy.CopyDebtAccounts(originalPerson.DebtAccounts);
        var newPerson = new McPerson()
        {
            Id = new Guid(),
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