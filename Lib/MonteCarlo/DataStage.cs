using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.StaticConfig;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NodaTime;

namespace Lib.MonteCarlo;

public static class DataStage
{
    
    public static McModel GetModelChampion(PgPerson person)
    {
        
        using var context = new PgContext();
        var champ = context.McModels
                        .FirstOrDefault(x => x.Id == Guid.Parse(MonteCarloConfig.ChampionModelId)) ??
                    throw new InvalidDataException();
        return champ;
    }
    
    
    
    

    
    
    

    
    

    
}