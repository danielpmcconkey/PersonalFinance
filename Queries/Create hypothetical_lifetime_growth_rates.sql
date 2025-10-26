--drop view personalfinance.vw_hypothetical_lifetime_growth_rates
create or replace view personalfinance.vw_hypothetical_lifetime_growth_rates as (
with ordered_growth as (
	select 
		cast(row_number() over (order by year , month ) -1 as int) as ordinal -- minus 1 to use with zero-indexed C# functions
		, year as historical_year
		, month as historical_month
		, sp_growth
		, cpi_growth
		, inflation_adjusted_growth
	from personalfinance.historicalgrowth
where year >= 1980 and sp_growth is not null
), distinct_blocks as (
	select ordinal as block_start
	from ordered_growth
	--where mod(ordinal-1, 12) = 0
), max_ordinal as (
	select max(ordinal) as max_ordinal_val from ordered_growth
), ordinal_pointers as (
	select
		block_start
		, ordinal
		, case 
			when block_start + ordinal > max_ordinal_val 
			then block_start + ordinal - max_ordinal_val 
			else block_start + ordinal
		  end as pointer
		, max_ordinal_val 
	from distinct_blocks db
	cross join ordered_growth
	cross join max_ordinal
), final_month_pointers as (
	select 
		op.block_start
		, op.ordinal as ordinal
		, op.pointer
		, og.ordinal as og_ordinal
		, og.sp_growth
		, og.cpi_growth
		, og.inflation_adjusted_growth
	from ordinal_pointers op
	left join ordered_growth og on op.pointer = og.ordinal
)
select * from final_month_pointers
)
/*
select * from personalfinance.vw_hypothetical_lifetime_growth_rates  
where 1=1
-- and og_ordinal is null
and ordinal = 0
order by block_start, ordinal
*/


select '[InlineData(', block_start, ', ', ordinal, ', ', inflation_adjusted_growth, ')]'
from personalfinance.vw_hypothetical_lifetime_growth_rates 
Where block_start = 143 and ordinal = 199
