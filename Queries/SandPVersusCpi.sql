with cpi_and_lag as (
	SELECT 
		year
		, month
		, indexval
		, lag(indexval, 1) over (order by year, month) as prior_month_indexval
	FROM investmenttracker.consumerpriceindex		
), monthly_inflation as (
	select
		year
		, month
		, indexval
		, prior_month_indexval
		, round( (indexval - prior_month_indexval) / indexval, 6) as monthly_inflation_rate
	from cpi_and_lag
), sandp_and_lag as (
	SELECT 
		year
		, month
		, indexval
		, lag(indexval, 1) over (order by year, month) as prior_month_indexval
	FROM investmenttracker.sandpindex
), monthly_growth as (
	select
		year
		, month
		, indexval
		, prior_month_indexval
		, round( (indexval - prior_month_indexval) / indexval, 6) as monthly_growth_rate
	from sandp_and_lag
)
select 
	mi.year
	, mi.month
	, mi.indexval as cpi_indexval
	, monthly_inflation_rate
	, mg.indexval as sandp_indexval
	, mg.monthly_growth_rate
	, monthly_growth_rate - monthly_inflation_rate as inflation_adjusted_growth_rate
from monthly_inflation mi
join monthly_growth mg on mi.year = mg.year and mi.month = mg.month
where mi.year >= 1980
order by 
	mi.year desc
	, mi.month desc
--limit 100
;