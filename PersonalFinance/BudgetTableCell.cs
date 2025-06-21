using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PersonalFinance
{
    internal struct BudgetTableCell
    {
        internal string Category;
        internal int Column; 
        internal int Row;
        internal string Label;
        internal decimal Value;
        internal string CssClass;
        
    }
}
