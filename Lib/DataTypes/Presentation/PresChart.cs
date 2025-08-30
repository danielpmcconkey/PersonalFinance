using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lib.DataTypes;
using Lib.DataTypes.Postgres;

namespace Lib.DataTypes.Presentation;

public record PresChart
{
    public required int Ordinal { get; init; }
    public required string Title { get; init; }
    public required string VAxisTitle { get; init; }
    public required string Description { get; init; }
    public required ChartDataGrid ChartDataGrid { get; init; }
    public required PresChartType PresChartType { get; init; }
}

