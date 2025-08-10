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
                        .Where(x => x.Id == Guid.Parse("148b2aca-826b-4267-9592-ba3ca3bcd2ec"))
                        .FirstOrDefault() ??
                    throw new InvalidDataException();
        return champ;
    }
    
    
    
    

    
    
    

    
    

    
}