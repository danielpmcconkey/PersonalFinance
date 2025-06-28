using Lib.DataTypes.MonteCarlo;
using Lib.Utils;

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
            AnnualSalary = CurrencyConverter.ConvertFromCurrency(pgperson.AnnualSalary),
            AnnualBonus = CurrencyConverter.ConvertFromCurrency(pgperson.AnnualBonus),
            MonthlyFullSocialSecurityBenefit = CurrencyConverter.ConvertFromCurrency(pgperson.MonthlyFullSocialSecurityBenefit),
            Annual401kMatchPercent = CurrencyConverter.ConvertFromCurrency(pgperson.Annual401kMatchPercent),
            InvestmentAccounts = Account.FetchDbInvestmentAccountsByPersonId(pgperson.Id),
            DebtAccounts = Account.FetchDbDebtAccountsByPersonId(pgperson.Id),
        };
        return person;
    }
}