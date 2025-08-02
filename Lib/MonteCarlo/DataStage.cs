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
        // todo: get an actual model from the DB. code is below when we're ready
        return Lib.MonteCarlo.StaticFunctions.Model.CreateRandomModel(person.BirthDate);
        
        using var context = new PgContext();
        // foreach (var champion in champions)
        // {
        //     context.McModels.Add(champion);
        //     context.SaveChanges();
        // }
        var champ = context.McModels
                        .Where(x => x.Id == Guid.Parse("7f28b9cd-2d01-48b5-a131-7d01ba7d0c65"))
                        .FirstOrDefault() ??
                    throw new InvalidDataException();
        return champ;
    }
    
    
    
    

    
    
    

    
    

    
}