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
                        .Where(x => x.Id == Guid.Parse("65dc4b1b-43ab-44ca-9997-10282ccda7d3"))
                        .FirstOrDefault() ??
                    throw new InvalidDataException();
        return champ;
    }
    
    
    
    

    
    
    

    
    

    
}