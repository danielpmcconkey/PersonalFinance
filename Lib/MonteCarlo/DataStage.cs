using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
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
                        .Where(x => x.Id == Guid.Parse("eee991c1-04dc-4fa7-870f-60ddb4840501"))
                        .FirstOrDefault() ??
                    throw new InvalidDataException();
        return champ;
    }
    
    
    
    

    
    
    

    
    

    
}