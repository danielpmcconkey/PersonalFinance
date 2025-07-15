namespace Lib.MonteCarlo.TaxForms.Federal;

public class QualifiedDividendsAndCapitalGainTaxWorksheet
{
    private ScheduleD _scheduleD;
    private decimal _fed1040Line3A = 0m;
    private decimal _fed1040Line15 = 0m;

    public QualifiedDividendsAndCapitalGainTaxWorksheet(
        ScheduleD scheduleD, decimal fed1040Line3A, decimal fed1040Line15)
    {
        _scheduleD = scheduleD;
        _fed1040Line3A = fed1040Line3A;
        _fed1040Line15 = fed1040Line15;
    }
    public decimal CalculateTaxOwed()
    {

        /*
         * https://www.irs.gov/pub/irs-pdf/i1040gi.pdf?os=wtmbzegmu5hwrefapp&ref=app
         * page 36
         */

        var line1 = _fed1040Line15;
        var line2 = _fed1040Line3A;
        var line3 = 
            (_scheduleD.Line15NetLongTermCapitalGain <= 0m || _scheduleD.Line16CombinedCapitalGains <= 0m) 
                ? 0m
                : Math.Min(_scheduleD.Line15NetLongTermCapitalGain, _scheduleD.Line16CombinedCapitalGains);
        var line4 = line2 + line3;
        var line5 = line1 - line4;
        var line6 = 94050m;
        var line7 = Math.Min(line1, line6);
        var line8 = Math.Min(line5, line7);
        var line9 = line7 - line8; // taxed at 0%
        var line10 = Math.Min(line1, line4);
        var line11 = line9;
        var line12 = line10 - line11;
        var line13 = 583750m;
        var line14 = Math.Min(line1, line13);
        var line15 = line5 + line9;
        var line16 = Math.Max(0m, line14 - line15);
        var line17 = Math.Min(line12, line16);
        var line18 = line17 * 0.15m;
        var line19 = line9 + line17;
        var line20 = line10 - line19;
        var line21 = line20 * 0.20m;
        var line22 = (line5 < 100000) 
            ? TaxTable.CalculateTaxOwed(line5)
            : TaxComputationWorksheet.CalculateTaxOwed(line5);
        var line23 = line18 + line21 + line22;
        var line24 = (line1 < 100000) 
            ? TaxTable.CalculateTaxOwed(line1)
            : TaxComputationWorksheet.CalculateTaxOwed(line1);
        var line25 = Math.Min(line23, line24);
        return line25;
    }
}