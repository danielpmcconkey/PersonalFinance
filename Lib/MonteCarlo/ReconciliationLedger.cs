using Lib.DataTypes.MonteCarlo;

namespace Lib.MonteCarlo;

using Lib.StaticConfig;


public static class ReconciliationLedger
{
    public static List<ReconciliationLineItem>? _reconciliationLineItems;
    private static int _ordinal = 0;
    private static bool _debugMode;
    

    static ReconciliationLedger()
    {
        _debugMode = MonteCarloConfig.DebugMode;
        if(_debugMode) _reconciliationLineItems = [];
        else _reconciliationLineItems = null;
    }
    public static void AddLine(ReconciliationLineItem item)
    {
        if (_debugMode == false || _reconciliationLineItems is null) return;
        item.Ordinal = _ordinal;
        _reconciliationLineItems.Add(item);
        _ordinal++;
    }

    

    
}