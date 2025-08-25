using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lib.DataTypes;
using Lib.DataTypes.Postgres;

namespace PersonalFinance
{
    internal class AreaChart
    {
        public int Ordinal { get; set; }
        public string? JavascriptId { get; set; }
        public string? JavascriptFunctionName { get; set; }
        public string? Title { get; set; }
        public string? VAxisTitle { get; set; }
        public string? Description { get; set; }
        public Func<PgPosition, string, bool>? FuncCatMatch { get; set; }
        public List<string> Categories { get; set; } = [];
    }
}
