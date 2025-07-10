using Lib.DataTypes.MonteCarlo;

namespace Lib.MonteCarlo;

using Lib.StaticConfig;


public static class ReconciliationLedger
{
    public static List<ReconciliationLineItem>ReconciliationLineItems;
    private static int _ordinal = 0;
    private static bool _debugMode;
    

    static ReconciliationLedger()
    {
        _debugMode = MonteCarloConfig.DebugMode;
        if(_debugMode) ReconciliationLineItems = [];
        else ReconciliationLineItems = [];
    }
    public static void AddLine(ReconciliationLineItem item)
    {
        if (_debugMode == false || ReconciliationLineItems is null) return;
        item.Ordinal = _ordinal;
        ReconciliationLineItems.Add(item);
        _ordinal++;
    }

    

    
}