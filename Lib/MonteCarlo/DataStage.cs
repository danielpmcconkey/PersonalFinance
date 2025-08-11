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
                        .Where(x => x.Id == Guid.Parse("a10fd5ac-76c8-459f-8c66-8c4e9186f001"))
                        .FirstOrDefault() ??
                    throw new InvalidDataException();
        return champ;
    }
    
    
    
    

    
    
    

    
    

    
}