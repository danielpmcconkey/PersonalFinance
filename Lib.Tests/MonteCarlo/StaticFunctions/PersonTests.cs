using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Lib.DataTypes;
using Lib.MonteCarlo.StaticFunctions;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class PersonTests
{
    private PgPerson CreateTestPerson()
    {
        return new PgPerson
        {
            Id = Guid.NewGuid(),
            Name = "Test Person",
            BirthDate = LocalDateTime.FromDateTime(DateTime.Now.AddYears(-30)),
            AnnualSalary = 50000M,
            AnnualBonus = 5000M,
            MonthlyFullSocialSecurityBenefit = 2000M,
            Annual401KMatchPercent = 0.05M,
            IsRetired = true,
            IsBankrupt = true,
            AnnualSocialSecurityWage = 1500M * 12m,
            Annual401KContribution = 1,
            AnnualHsaContribution = 2,
            AnnualHsaEmployerContribution = 3,
            FederalAnnualWithholding = 4,
            StateAnnualWithholding = 5,
            PreTaxHealthDeductions = 12,
            PostTaxInsuranceDeductions = 13,
            RequiredMonthlySpend = 14,
            RequiredMonthlySpendHealthCare = 15,
        };
    }

    [Fact]
    public void GetPersonById_WhenPersonDoesNotExist_ThrowsInvalidDataException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => Person.GetPersonById(nonExistentId));
    }

    [Theory]
    // all data assumes a birthdate of Mar 7, 1976
    // used https://www.ssa.gov/OACT/quickcalc/early_late.html to pull these examples
    [InlineData(2038, 4, 70.42)]
    [InlineData(2039, 5, 75.83)]
    [InlineData(2039, 8, 77.08)]
    [InlineData(2040, 1, 79.17)]
    [InlineData(2040, 11, 84.44)]
    [InlineData(2041, 9, 90)]
    [InlineData(2042, 7, 95.56)]
    [InlineData(2043, 2, 99.44)]
    [InlineData(2043, 3, 100)]
    [InlineData(2043, 4, 100.67)]
    [InlineData(2044, 10, 112.67)]
    [InlineData(2045, 6, 118)]
    [InlineData(2046, 3, 124)]
    public void CalculateMonthlySocialSecurityWage_CalculatesCorrectly(
        int year, int month, decimal percentModifier)
    {
        // Arrange
        const decimal fullWage = 2500m;
        var percentModifierDecimal = percentModifier / 100; // the SSA calc gives "percentages" as 85.66%
        var expected = Math.Round(fullWage * percentModifierDecimal, 0); // round to the nearest dollar because decimal math

        var person = CreateTestPerson();
        person.BirthDate = new(1976, 3, 7, 0, 0, 0);
        person.MonthlyFullSocialSecurityBenefit = fullWage;
        var benefitElectionStart = new LocalDateTime(year, month, person.BirthDate.Day, 0, 0, 0);

        // Act
        var result = Person.CalculateMonthlySocialSecurityWage(person, benefitElectionStart);
        var actual = Math.Round(result, 0); // round to the nearest dollar because decimal math
        
        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void CalculateMonthlySocialSecurityWage_WhenElectionDateIsBeforeAge62AndOneMonth_ThrowsInvalidDataException()
    {
        // Arrange
        var person = CreateTestPerson();
        person.BirthDate = new(1976, 3, 7, 0, 0, 0);

        var benefitElectionStart = person.BirthDate.PlusYears(62);
        

        // Act
        Assert.Throws<InvalidDataException>(() => 
            Person.CalculateMonthlySocialSecurityWage(person, benefitElectionStart));
    }

    
    [Fact]
    public void CalculateMonthlySocialSecurityWage_WhenElectionDateIsAfterAge70_ThrowsInvalidDataException()
    {
        // Arrange
        var person = CreateTestPerson();
        person.BirthDate = new(1976, 3, 7, 0, 0, 0);

        var benefitElectionStart = person.BirthDate.PlusYears(70).PlusMonths(1);
        

        // Act
        Assert.Throws<InvalidDataException>(() => 
            Person.CalculateMonthlySocialSecurityWage(person, benefitElectionStart));
    }
    
   [Fact]
    public void CopyPerson_CreatesCopyWithTrue_CopiesCalculatedStats()
    {
        /*
         * make sure we're resetting "calculated" fields like MonthlySocialSecurityWage and setting bankruptcy stuff to
         * a clean slate
         */
        // Arrange
        var originalPerson = CreateTestPerson();
        originalPerson.IsRetired = true;
        originalPerson.IsBankrupt = true;
        originalPerson.AnnualSocialSecurityWage = 34m;
        originalPerson.Annual401KPreTax = 123m;
        originalPerson.Annual401KPostTax = 456m;

        // Act
        var copiedPerson = Person.CopyPerson(originalPerson, true);

        // Assert
        Assert.Equal(originalPerson.Id, copiedPerson.Id);
        Assert.Equal(originalPerson.Name, copiedPerson.Name);
        Assert.Equal(originalPerson.BirthDate, copiedPerson.BirthDate);
        Assert.Equal(originalPerson.AnnualSalary, copiedPerson.AnnualSalary);
        Assert.Equal(originalPerson.AnnualBonus, copiedPerson.AnnualBonus);
        Assert.Equal(originalPerson.Annual401KMatchPercent, copiedPerson.Annual401KMatchPercent);
        Assert.Equal(originalPerson.MonthlyFullSocialSecurityBenefit, copiedPerson.MonthlyFullSocialSecurityBenefit);
        Assert.Equal(originalPerson.Annual401KContribution, copiedPerson.Annual401KContribution);
        Assert.Equal(originalPerson.AnnualHsaContribution, copiedPerson.AnnualHsaContribution);
        Assert.Equal(originalPerson.AnnualHsaEmployerContribution, copiedPerson.AnnualHsaEmployerContribution);
        Assert.Equal(originalPerson.FederalAnnualWithholding, copiedPerson.FederalAnnualWithholding);
        Assert.Equal(originalPerson.StateAnnualWithholding, copiedPerson.StateAnnualWithholding);
        Assert.Equal(originalPerson.PreTaxHealthDeductions, copiedPerson.PreTaxHealthDeductions);
        Assert.Equal(originalPerson.PostTaxInsuranceDeductions, copiedPerson.PostTaxInsuranceDeductions);
        Assert.Equal(originalPerson.RequiredMonthlySpend, copiedPerson.RequiredMonthlySpend);
        Assert.Equal(originalPerson.RequiredMonthlySpendHealthCare, copiedPerson.RequiredMonthlySpendHealthCare);
        // calculated fields
        Assert.True(copiedPerson.IsRetired);
        Assert.True(copiedPerson.IsBankrupt);
        Assert.Equal(originalPerson.AnnualSocialSecurityWage, copiedPerson.AnnualSocialSecurityWage);
        Assert.Equal(originalPerson.Annual401KPreTax, copiedPerson.Annual401KPreTax);
        Assert.Equal(originalPerson.Annual401KPostTax, copiedPerson.Annual401KPostTax);
    }

    [Fact]
    public void CopyPerson_CreatesCopyWithFalse_ResetCalculatedStats()
    {
        /*
         * make sure we're resetting "calculated" fields like MonthlySocialSecurityWage and setting bankruptcy stuff to
         * a clean slate
         */
        // Arrange
        var originalPerson = CreateTestPerson();

        // Act
        var copiedPerson = Person.CopyPerson(originalPerson, false);

        // Assert
        Assert.False(copiedPerson.IsRetired);
        Assert.False(copiedPerson.IsBankrupt);
        Assert.Equal(0M, copiedPerson.AnnualSocialSecurityWage);
        Assert.Equal(0M, copiedPerson.Annual401KPreTax);
        Assert.Equal(0M, copiedPerson.Annual401KPostTax);
    }
    
}