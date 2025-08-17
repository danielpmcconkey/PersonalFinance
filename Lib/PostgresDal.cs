using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using System.Configuration;
using Lib.DataTypes;

namespace Lib
{
    public static class PostgresDal
    {
        #region CoreFunctions
        private const int DbMaxRetries = 3;
        private const int DbRetrySleepMilliseconds = 1000;
        public static NpgsqlDataReader ExecuteReader(NpgsqlCommand cmd, int retries = 0)
        {
            try
            {
                return (NpgsqlDataReader)cmd.ExecuteReader();
            }
            catch (Exception ex)
            {
                if (retries < DbMaxRetries)
                {
                    Console.WriteLine($"Exception in executeReader_async. Retrying. Total retries so far {++retries}", ex);
                    System.Threading.Thread.Sleep(DbRetrySleepMilliseconds);
                    return ExecuteReader(cmd, retries);
                }
                else throw;
            }
        }
        public static object? ExecuteScalar(NpgsqlCommand cmd, int retries = 0)
        {
            try
            {
                return cmd.ExecuteScalar();
            }
            catch (Exception ex)
            {
                if (retries < DbMaxRetries)
                {
                    Console.WriteLine($"Exception in execute scalar. Retrying. Total retries so far {++retries}", ex);
                    System.Threading.Thread.Sleep(DbRetrySleepMilliseconds);
                    return ExecuteScalar(cmd, retries);
                }
                else throw;
            }
        }
        
        private static string GetConnectionString()
        {
            string? pgPassHex = Environment.GetEnvironmentVariable("PGPASS");
            if(pgPassHex == null) throw new InvalidDataException("PGPASS environment variable not found");
            var converted = Convert.FromHexString(pgPassHex);
            string passNew = System.Text .Encoding.Unicode.GetString(converted);
            
            var connectionString = $"Host=localhost;Username=dansdev;Password='{passNew}';Database=householdbudget;" +
                                   "Timeout=15;Command Timeout=300;";
            return connectionString;
        }
        public static NpgsqlConnection GetConnection()
        {

            string connectionString = GetConnectionString();
            NpgsqlConnection conn = new NpgsqlConnection(connectionString);
            return conn;
        }
        public static void OpenConnection(NpgsqlConnection conn, int retries = 0)
        {
            try
            {
                conn.Open();
            }
            catch (PostgresException ex)
            {
                if (ex.SqlState == "53300") // too_many_connections
                {
                    if (retries >= DbMaxRetries)
                    {
                        Console.WriteLine("PostgresException: Too many retries exceeded. Throwing.");
                        throw;
                    }
                    else
                    {
                        Console.WriteLine("PostgresException: Too many DB connections. Retrying after sleep period");
                        System.Threading.Thread.Sleep(DbRetrySleepMilliseconds);
                        OpenConnection(conn, ++retries);
                    }
                }
                else
                {
                    if (retries < DbMaxRetries)
                    {
                        Console.WriteLine("PostgresException exception on query. Retrying.");
                        System.Threading.Thread.Sleep(DbRetrySleepMilliseconds);
                        OpenConnection(conn, ++retries);
                    }
                    else throw;
                }
            }
            catch (Exception ex)
            {
                if (retries < DbMaxRetries)
                {
                    Console.WriteLine("Exception in open connection. Retrying.", ex);
                    System.Threading.Thread.Sleep(DbRetrySleepMilliseconds);
                    OpenConnection(conn, ++retries);
                }
                else throw;
            }
        } 
        #endregion

        #region generic get functions

        public static bool GetBool(NpgsqlDataReader reader, string fieldName)
        {
            if (!reader.IsDBNull(reader.GetOrdinal(fieldName)))
            {
                return reader.GetBoolean(reader.GetOrdinal(fieldName));
            }
            return false;
        }
        public static DateTime GetDateTime(NpgsqlDataReader reader, string fieldName)
        {
            if (!reader.IsDBNull(reader.GetOrdinal(fieldName)))
            {
                return reader.GetDateTime(reader.GetOrdinal(fieldName));
                
            }
            return DateTime.MinValue;
        }
        public static decimal getDecimal(NpgsqlDataReader reader, string fieldName)
        {
            if (!reader.IsDBNull(reader.GetOrdinal(fieldName)))
            {
                return (decimal)reader.GetDecimal(reader.GetOrdinal(fieldName));
            }
            return 0;
        }
        public static double GetDouble(NpgsqlDataReader reader, string fieldName)
        {
            if (!reader.IsDBNull(reader.GetOrdinal(fieldName)))
            {
                return reader.GetDouble(reader.GetOrdinal(fieldName));
            }
            return 0;
        }
        public static Guid GetGuid(NpgsqlDataReader reader, string fieldName)
        {
            if (!reader.IsDBNull(reader.GetOrdinal(fieldName)))
            {
                return reader.GetGuid(reader.GetOrdinal(fieldName));
            }
            return Guid.NewGuid();
        }
        
        public static int GetInt(NpgsqlDataReader reader, string fieldName)
        {
            if (!reader.IsDBNull(reader.GetOrdinal(fieldName)))
            {
                return reader.GetInt32(reader.GetOrdinal(fieldName));
            }
            return 0;
        }
        public static short GetShort(NpgsqlDataReader reader, string fieldName)
        {
            if (!reader.IsDBNull(reader.GetOrdinal(fieldName)))
            {
                return reader.GetInt16(reader.GetOrdinal(fieldName));
            }
            return 0;
        }
        public static long GetLong(NpgsqlDataReader reader, string fieldName)
        {
            if (!reader.IsDBNull(reader.GetOrdinal(fieldName)))
            {
                return reader.GetInt64(reader.GetOrdinal(fieldName));
            }
            return 0;
        }
        public static decimal? GetNullableDecimal(NpgsqlDataReader reader, string fieldName)
        {
            if (!reader.IsDBNull(reader.GetOrdinal(fieldName)))
            {
                return (decimal?)reader.GetDecimal(reader.GetOrdinal(fieldName));
            }
            return null;
        }
        public static double? GetNullableDouble(NpgsqlDataReader reader, string fieldName)
        {
            if (!reader.IsDBNull(reader.GetOrdinal(fieldName)))
            {
                return reader.GetDouble(reader.GetOrdinal(fieldName));
            }
            return null;
        }
        private static string GetString(NpgsqlDataReader reader, string fieldName)
        {
            return !reader.IsDBNull(reader.GetOrdinal(fieldName)) ? 
                reader.GetString(reader.GetOrdinal(fieldName)) : 
                string.Empty;
        }
        public static TimeSpan GetTimeSpan(NpgsqlDataReader reader, string fieldName)
        {
            if (reader.IsDBNull(reader.GetOrdinal(fieldName))) return new TimeSpan(0);
            var dbVal = reader.GetInt64(reader.GetOrdinal(fieldName));
            return new TimeSpan(dbVal);
        }
        #endregion generic get functions

        #region GetDataLists
        public static List<Position> GetCashPositions()
        {
            List<Position> positions = new List<Position>();
            StringBuilder yearsUnion = new StringBuilder();
            for (int i = 2024; i <= DateTime.Now.Year; i++)
            {
                yearsUnion.Append($"select {i} as year");
                if (i != DateTime.Now.Year) yearsUnion.Append(" union all");
                yearsUnion.Append(Environment.NewLine);
            }
            string query = $"""
                SET search_path TO personalfinance;
 
                with years as (
                    {yearsUnion.ToString()} 
                )
                , months as (
                    select 'January' as month, 'Jan' as abbreviation, 1 as sortorder union all
                    select 'Februray' as month, 'Feb' as abbreviation, 2 as sortorder union all
                    select 'March' as month, 'Mar' as abbreviation, 3 as sortorder union all
                    select 'April' as month, 'Apr' as abbreviation, 4 as sortorder union all
                    select 'May' as month, 'May' as abbreviation, 5 as sortorder union all
                    select 'June' as month, 'Jun' as abbreviation, 6 as sortorder union all
                    select 'July' as month, 'Jul' as abbreviation, 7 as sortorder union all
                    select 'August' as month, 'Aug' as abbreviation, 8 as sortorder union all
                    select 'September' as month, 'Sep' as abbreviation, 9 as sortorder union all
                    select 'October' as month, 'Oct' as abbreviation, 10 as sortorder union all
                    select 'November' as month, 'Nov' as abbreviation, 11 as sortorder union all
                    select 'December' as month, 'Dec' as abbreviation, 12 as sortorder 
                )
                , dates as (
                    select 
                        y.year
                        , m.month
                        , concat(m.abbreviation, '-', y.year) as month_abbreviation
                        , m.sortorder as month_sort
                        , (TO_DATE(concat(y.year, case when m.sortorder < 10 then concat('0', cast(m.sortorder as char(1)))
                                else cast(m.sortorder as char(2)) end, '01') , 'YYYYMMDD') + interval '1 month') - interval '1 day' as last_date_of_month
                    from years y cross join months m
                )
                , monthly_positions as (
                    select 
                          d.last_date_of_month
                        , d.month_abbreviation
                        , a.id as account_id
                        , a.name as account
                        , a.type as account_type
                        , row_number() over (partition by p.cashaccount, d.month_abbreviation order by p.position_date desc) as position_ordinal
                        , p.current_balance
                    from dates d
                    cross join cashaccount a
                    left join cashposition p on p.cashaccount = a.id
                    where last_date_of_month <= NOW()
                    and p.position_date <= d.last_date_of_month
                )
                select 
                      mp.last_date_of_month
                    , mp.month_abbreviation
                    , mp.account_id
                    , mp.account
                    , mp.account_type
                    , mp.current_balance
                from monthly_positions mp
                where position_ordinal = 1
                order by mp.last_date_of_month, mp.current_balance desc
                ;
                """;


            using (NpgsqlConnection conn = PostgresDal.GetConnection())
            {
                PostgresDal.OpenConnection(conn);
                using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
                {
                    using (NpgsqlDataReader reader = PostgresDal.ExecuteReader(cmd))
                    {
                        while (reader.Read())
                        {
                            Position position = new Position()
                            {
                                PositionDate = PostgresDal.GetDateTime(reader, "last_date_of_month"),
                                MonthAbbreviation = PostgresDal.GetString(reader, "month_abbreviation"),
                                AccountId = PostgresDal.GetInt(reader, "account_id"),
                                AccountName = PostgresDal.GetString(reader, "account"),
                                AccountGroup = PostgresDal.GetString(reader, "account_type"),
                                ValueAtTime = PostgresDal.getDecimal(reader, "current_balance"),
                                Price = 1m,
                                TotalQuantity = PostgresDal.getDecimal(reader, "current_balance"),
                                TaxBucket = "Cash",
                                Symbol = "Cash"
                            };
                            positions.Add(position);
                        }
                    }
                }
            }
            return positions;

        }
        public static List<Position> GetDebtPositions()
        {
            List<Position> positions = new List<Position>();
            StringBuilder yearsUnion = new StringBuilder();
            for (int i = 2024; i <= DateTime.Now.Year; i++)
            {
                yearsUnion.Append($"select {i} as year");
                if (i != DateTime.Now.Year) yearsUnion.Append(" union all");
                yearsUnion.Append(Environment.NewLine);
            }
            string query = $"""
                SET search_path TO personalfinance;
 
                with years as (
                    {yearsUnion.ToString()} 
                )
                , months as (
                    select 'January' as month, 'Jan' as abbreviation, 1 as sortorder union all
                    select 'Februray' as month, 'Feb' as abbreviation, 2 as sortorder union all
                    select 'March' as month, 'Mar' as abbreviation, 3 as sortorder union all
                    select 'April' as month, 'Apr' as abbreviation, 4 as sortorder union all
                    select 'May' as month, 'May' as abbreviation, 5 as sortorder union all
                    select 'June' as month, 'Jun' as abbreviation, 6 as sortorder union all
                    select 'July' as month, 'Jul' as abbreviation, 7 as sortorder union all
                    select 'August' as month, 'Aug' as abbreviation, 8 as sortorder union all
                    select 'September' as month, 'Sep' as abbreviation, 9 as sortorder union all
                    select 'October' as month, 'Oct' as abbreviation, 10 as sortorder union all
                    select 'November' as month, 'Nov' as abbreviation, 11 as sortorder union all
                    select 'December' as month, 'Dec' as abbreviation, 12 as sortorder 
                )
                , dates as (
                    select 
                        y.year
                        , m.month
                        , concat(m.abbreviation, '-', y.year) as month_abbreviation
                        , m.sortorder as month_sort
                        , (TO_DATE(concat(y.year, case when m.sortorder < 10 then concat('0', cast(m.sortorder as char(1)))
                                else cast(m.sortorder as char(2)) end, '01') , 'YYYYMMDD') + interval '1 month') - interval '1 day' as last_date_of_month
                    from years y cross join months m
                )
                , monthly_positions as (
                    select 
                          d.last_date_of_month
                        , d.month_abbreviation
                        , a.id as account_id
                        , a.name as account
                        , a.type as account_type
                        , row_number() over (partition by p.debtaccount, d.month_abbreviation order by p.position_date desc) as position_ordinal
                        , p.current_balance
                    from dates d
                    cross join debtaccount a
                    left join debtposition p on p.debtaccount = a.id
                    where last_date_of_month <= NOW()
                    and p.position_date <= d.last_date_of_month
                )
                select 
                      mp.last_date_of_month
                    , mp.month_abbreviation
                    , mp.account_id
                    , mp.account
                    , mp.account_type
                    , mp.current_balance
                from monthly_positions mp
                where position_ordinal = 1
                order by mp.last_date_of_month, mp.current_balance desc
                ;
                """;


            using (NpgsqlConnection conn = PostgresDal.GetConnection())
            {
                PostgresDal.OpenConnection(conn);
                using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
                {
                    using (NpgsqlDataReader reader = PostgresDal.ExecuteReader(cmd))
                    {
                        while (reader.Read())
                        {
                            Position position = new Position()
                            {
                                PositionDate = PostgresDal.GetDateTime(reader, "last_date_of_month"),
                                MonthAbbreviation = PostgresDal.GetString(reader, "month_abbreviation"),
                                AccountId = PostgresDal.GetInt(reader, "account_id"),
                                AccountName = PostgresDal.GetString(reader, "account"),
                                AccountGroup = PostgresDal.GetString(reader, "account_type"),
                                ValueAtTime = PostgresDal.getDecimal(reader, "current_balance"),
                                TaxBucket = "Debt",
                                Symbol = "Debt",
                                Price = 1m,
                                TotalQuantity = PostgresDal.getDecimal(reader, "current_balance")
                            };

                            positions.Add(position);
                        }
                    }
                }
            }
            return positions;
        }
        // public static List<PgCategory> GetCategories()
        // {
        //     List<PgCategory> categories = new List<PgCategory>();
        //     string query = @"
        //         SET search_path TO personalfinance;
        //         select 
	       //          id 
	       //          , parent_id
	       //          , display_name
	       //          , ordinal_within_parent
        //             , show_in_report
        //         from category
        //         where show_in_report = true
	       //      order by ordinal_within_parent 
        //         ;
        //         ";
        //
        //
        //     using (NpgsqlConnection conn = PostgresDAL.getConnection())
        //     {
        //         PostgresDAL.openConnection(conn);
        //         using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
        //         {
        //             using (NpgsqlDataReader reader = PostgresDAL.executeReader(cmd))
        //             {
        //                 while (reader.Read())
        //                 {
        //                     PgCategory pgCategory = new PgCategory();
        //                     pgCategory.Id = PostgresDAL.getString(reader, "id");
        //                     pgCategory.ParentId = PostgresDAL.getString(reader, "parent_id");
        //                     pgCategory.DisplayName = PostgresDAL.getString(reader, "display_name");
        //                     pgCategory.OrdinalWithinParent = PostgresDAL.getShort(reader, "ordinal_within_parent");
        //                     pgCategory.ShowInReport = PostgresDAL.getBool(reader, "show_in_report");
        //                     categories.Add(pgCategory);
        //                 }
        //             }
        //         }
        //     }
        //     return categories;
        // }
        public static List<BudgetPosition> GetBudgetPositions(DateTime from, DateTime to)
        {
            List<BudgetPosition> positions = new List<BudgetPosition>();
            StringBuilder yearsUnion = new StringBuilder();
            for (int i = 2023; i <= DateTime.Now.Year; i++)
            {
                yearsUnion.Append($"select {i} as year");
                if (i != DateTime.Now.Year) yearsUnion.Append(" union all");
                yearsUnion.Append(Environment.NewLine);
            }
            string query = $"""
                SET search_path TO personalfinance;
 
                with years as (
                    {yearsUnion.ToString()} 
                )
                , months as (
                    select 'January' as month, 'Jan' as abbreviation, 1 as sortorder union all
                    select 'Februray' as month, 'Feb' as abbreviation, 2 as sortorder union all
                    select 'March' as month, 'Mar' as abbreviation, 3 as sortorder union all
                    select 'April' as month, 'Apr' as abbreviation, 4 as sortorder union all
                    select 'May' as month, 'May' as abbreviation, 5 as sortorder union all
                    select 'June' as month, 'Jun' as abbreviation, 6 as sortorder union all
                    select 'July' as month, 'Jul' as abbreviation, 7 as sortorder union all
                    select 'August' as month, 'Aug' as abbreviation, 8 as sortorder union all
                    select 'September' as month, 'Sep' as abbreviation, 9 as sortorder union all
                    select 'October' as month, 'Oct' as abbreviation, 10 as sortorder union all
                    select 'November' as month, 'Nov' as abbreviation, 11 as sortorder union all
                    select 'December' as month, 'Dec' as abbreviation, 12 as sortorder 
                )
                , dates as (
                    select 
                        y.year
                        , m.month
                        , concat(m.abbreviation, '-', y.year) as month_abbreviation
                        , m.sortorder as month_sort
                        , TO_DATE(concat(y.year, case when m.sortorder < 10 then concat('0', cast(m.sortorder as char(1)))
                                 else cast(m.sortorder as char(2)) end, '01') , 'YYYYMMDD') as first_date_of_month
                        , (TO_DATE(concat(y.year, case when m.sortorder < 10 then concat('0', cast(m.sortorder as char(1)))
                                 else cast(m.sortorder as char(2)) end, '01') , 'YYYYMMDD') + interval '1 month') - interval '1 second' as last_date_of_month
                    from years y cross join months m
                )
                , transactions as (
                    select 
                          d. month_abbreviation
                        , d.first_date_of_month
                        , d.last_date_of_month
                        , c.id as category_id
                        , c.parent_id as category_parent
                        , c.display_name
                        , c.ordinal_within_parent as cat_sort_order
                        , t.amount
                        , c.show_in_report
                    from dates d
                    cross join category c
                    left join tran t 
                        on c.id = t.category_id
                        and t.transactiondate >= d.first_date_of_month 
                        and t.transactiondate <= d.last_date_of_month
                )
                select 
                      month_abbreviation
                    , first_date_of_month
                    , last_date_of_month
                    , category_id
                    , category_parent
                    , display_name
                    , cat_sort_order
                    , sum(amount)
                    , show_in_report
                from transactions
                where first_date_of_month <= :to
                and first_date_of_month >= :from
                group by 
                      month_abbreviation
                    , first_date_of_month
                    , last_date_of_month
                    , category_id
                    , category_parent
                    , display_name
                    , cat_sort_order
                    , show_in_report
                order by first_date_of_month, cat_sort_order
                ;

                """;


            using (NpgsqlConnection conn = PostgresDal.GetConnection())
            {
                PostgresDal.OpenConnection(conn);
                using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue(":from", from);
                    cmd.Parameters.AddWithValue(":to", to);
                    using (NpgsqlDataReader reader = PostgresDal.ExecuteReader(cmd))
                    {
                        while (reader.Read())
                        {
                            BudgetPosition position = new BudgetPosition()
                            {
                                PositionDate = PostgresDal.GetDateTime(reader, "last_date_of_month"),
                                MonthAbbreviation = PostgresDal.GetString(reader, "month_abbreviation"),
                                CategoryId = PostgresDal.GetString(reader, "category_id"),
                                CategoryName = PostgresDal.GetString(reader, "display_name"),
                                ParentCategoryId = PostgresDal.GetString(reader, "category_parent"),
                                SumTotal = PostgresDal.getDecimal(reader, "sum"),
                                Ordinal = PostgresDal.GetInt(reader, "cat_sort_order"),
                                ShowInReport = PostgresDal.GetBool(reader, "show_in_report"),
                            };
                            positions.Add(position);
                        }
                    }
                }
            }
            return positions;
        }

        public static List<Position> GetWealthPositions()
        {
            List<Position> positions = new List<Position>();
            StringBuilder yearsUnion = new StringBuilder();
            for (int i = 2020; i <= DateTime.Now.Year; i++)
            {
                yearsUnion.Append($"select {i} as year");
                if (i != DateTime.Now.Year) yearsUnion.Append(" union all");
                yearsUnion.Append(Environment.NewLine);
            }
            string query = $"""
                SET search_path TO personalfinance;
 
                with years as (
                    {yearsUnion.ToString()}
                            )
                , months as (
                    select 'January' as month, 'Jan' as abbreviation, 1 as sortorder union all
                    select 'Februray' as month, 'Feb' as abbreviation, 2 as sortorder union all
                    select 'March' as month, 'Mar' as abbreviation, 3 as sortorder union all
                    select 'April' as month, 'Apr' as abbreviation, 4 as sortorder union all
                    select 'May' as month, 'May' as abbreviation, 5 as sortorder union all
                    select 'June' as month, 'Jun' as abbreviation, 6 as sortorder union all
                    select 'July' as month, 'Jul' as abbreviation, 7 as sortorder union all
                    select 'August' as month, 'Aug' as abbreviation, 8 as sortorder union all
                    select 'September' as month, 'Sep' as abbreviation, 9 as sortorder union all
                    select 'October' as month, 'Oct' as abbreviation, 10 as sortorder union all
                    select 'November' as month, 'Nov' as abbreviation, 11 as sortorder union all
                    select 'December' as month, 'Dec' as abbreviation, 12 as sortorder 
                )
                , dates as (
                    select 
                        y.year
                        , m.month
                        , concat(m.abbreviation, '-', y.year) as month_abbreviation
                        , m.sortorder as month_sort
                        , (TO_DATE(concat(y.year, case when m.sortorder < 10 then concat('0', cast(m.sortorder as char(1)))
                                else cast(m.sortorder as char(2)) end, '01') , 'YYYYMMDD') + interval '1 month') - interval '1 day' as last_date_of_month
                    from years y cross join months m
                )
                , monthly_positions as (
                    select 
                          d.last_date_of_month
                        , d.month_abbreviation
                        , a.id as account_id
                        , a.name as account
                        , ag.name as account_group
                        , tb.name as tax_bucket
                        , row_number() over (partition by p.symbol, p.investmentaccount, d.month_abbreviation order by p.position_date desc) as position_ordinal
                        , f.symbol
                        , p.price
                        , p.total_quantity
                        , p.current_value
                        , p.cost_basis
                        , ft1.name as fundtype1
                        , ft2.name as fundtype2
                        , ft3.name as fundtype3
                        , ft4.name as fundtype4
                        , ft5.name as fundtype5
                    from dates d
                    cross join investmentaccount a
                    left join taxbucket tb on a.taxbucket = tb.id
                    left join investmentaccountgroup ag on a.investmentaccountgroup = ag.id
                    cross join fund f
                    left join fundtype ft1 on f.fundtype1 = ft1.id
                    left join fundtype ft2 on f.fundtype2 = ft2.id
                    left join fundtype ft3 on f.fundtype3 = ft3.id
                    left join fundtype ft4 on f.fundtype4 = ft4.id
                    left join fundtype ft5 on f.fundtype5 = ft5.id
                    left join position p on p.symbol = f.symbol and p.investmentaccount = a.id
                    where last_date_of_month <= NOW()
                    and p.position_date <= d.last_date_of_month
                )
                , total_wealth_by_month as (
                    select 
                          month_abbreviation
                        , sum(current_value) as total_wealth
                    from monthly_positions
                    where position_ordinal = 1
                    group by month_abbreviation
    
                )
                select 
                      mp.last_date_of_month
                    , mp.month_abbreviation
                    , mp.account_id
                    , mp.account
                    , mp.account_group
                    , mp.tax_bucket
                    , mp.symbol
                    , mp.price
                    , mp.total_quantity
                    , mp.current_value
                    , mp.cost_basis
                    , twbm.total_wealth
                    , mp.current_value - mp.cost_basis as investment_gain
                    , mp.current_value / twbm.total_wealth as percent_total_wealth
                    , mp.fundtype1
                    , mp.fundtype2
                    , mp.fundtype3
                    , mp.fundtype4
                    , mp.fundtype5
                from monthly_positions mp
                left join total_wealth_by_month twbm on mp.month_abbreviation = twbm.month_abbreviation
                where position_ordinal = 1
                order by account, symbol, last_date_of_month
                ;
                """;


            using (NpgsqlConnection conn = PostgresDal.GetConnection())
            {
                PostgresDal.OpenConnection(conn);
                using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
                {
                    using (NpgsqlDataReader reader = PostgresDal.ExecuteReader(cmd))
                    {
                        while (reader.Read())
                        {
                            Position position = new Position()
                            {
                                PositionDate = PostgresDal.GetDateTime(reader, "last_date_of_month"),
                                MonthAbbreviation = PostgresDal.GetString(reader, "month_abbreviation"),
                                AccountId = PostgresDal.GetInt(reader, "account_id"),
                                AccountName = PostgresDal.GetString(reader, "account"),
                                AccountGroup = PostgresDal.GetString(reader, "account_group"),
                                TaxBucket = PostgresDal.GetString(reader, "tax_bucket"),
                                Symbol = PostgresDal.GetString(reader, "symbol"),
                                FundType1 = PostgresDal.GetString(reader, "fundtype1"),
                                FundType2 = PostgresDal.GetString(reader, "fundtype2"),
                                FundType3 = PostgresDal.GetString(reader, "fundtype3"),
                                FundType4 = PostgresDal.GetString(reader, "fundtype4"),
                                FundType5 = PostgresDal.GetString(reader, "fundtype5"),
                                Price = PostgresDal.getDecimal(reader, "price"),
                                TotalQuantity = PostgresDal.getDecimal(reader, "total_quantity"),
                                ValueAtTime = PostgresDal.getDecimal(reader, "current_value"),
                                CostBasis = PostgresDal.getDecimal(reader, "cost_basis"),
                                TotalWealthAtTime = PostgresDal.getDecimal(reader, "total_wealth"),
                                InvestmentGain = PostgresDal.getDecimal(reader, "investment_gain"),
                                PercentOfWealth = PostgresDal.getDecimal(reader, "percent_total_wealth")
                            };

                            positions.Add(position);
                        }
                    }
                }
            }
            return positions;
        }
        // public static List<Transaction> GetTransactions()
        // {
        //     List<Transaction> transactions = new List<Transaction>();
        //     string query = @"
        //         SET search_path TO personalfinance;
        //         select 
	       //            id,
        //               category_id,
        //               transactiondate,
        //               description,
        //               amount 
        //         from tran
        //         ;
        //         ";
        //
        //
        //     using (NpgsqlConnection conn = PostgresDAL.getConnection())
        //     {
        //         PostgresDAL.openConnection(conn);
        //         using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
        //         {
        //             using (NpgsqlDataReader reader = PostgresDAL.executeReader(cmd))
        //             {
        //                 while (reader.Read())
        //                 {
        //                     Transaction transaction = new Transaction();
        //                     transaction.Id = PostgresDAL.getInt(reader, "id");
        //                     transaction.CategoryId = PostgresDAL.getString(reader, "category_id");
        //                     transaction.DateTime = PostgresDAL.getDateTime(reader, "transactiondate");
        //                     transaction.Description = PostgresDAL.getString(reader, "description");
        //                     transaction.Amount = PostgresDAL.getDecimal(reader, "amount");
        //                     transactions.Add(transaction);
        //                 }
        //             }
        //         }
        //     }
        //     return transactions;
        // }
        #endregion
    }
}
