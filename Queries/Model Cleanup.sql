with all_runs as (
	select 
		m.id
		, r.majorversion
		, r.minorversion
		, max(r.funpointsatendofsim50) as funpointsatendofsim50
		, min(r.bankruptcyrateatendofsim) as bankruptcyrateatendofsim
	from personalfinance.singlemodelrunresult r
	left join personalfinance.montecarlomodel m on r.modelid = m.id
	where m.id is not null
	and r.bankruptcyrateatendofsim <= 0.1
	group by 
		m.id
		, r.majorversion
		, r.minorversion
), ordered_runs as (
	select 
	* 
		, row_number() over(
				partition by majorversion, minorversion
				order by funpointsatendofsim50 desc, bankruptcyrateatendofsim asc) 
				as row_num
	from all_runs
), keepers as (
	select id from ordered_runs where row_num <= 20
)
/*
-- uncomment this section to delete unneeded run results
delete
--select count(*)
from personalfinance.singlemodelrunresult
where modelid not in (select id from keepers)
*/


/*
-- uncomment this section to delete unneeded models
--delete
select count(*)
from personalfinance.montecarlomodel
where id not in (select id from keepers)
*/
