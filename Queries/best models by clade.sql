with ranked_by_model as (
	select 
		  m.id as model_id
		, m.parenta
		, m.parentb
		, m.withdrawalstrategy
		, m.clade
		, r.majorversion
		, r.minorversion
		, round(r.funpointsatendofsim50, 2) as funpointsatendofsim50
		, round(r.networthatendofsim50, 2) as networthatendofsim50
		, round(r.funpointsatendofsim90, 2) as funpointsatendofsim90
		, round(r.networthatendofsim90, 2) as networthatendofsim90
		, m.retirementdate
		, m.socialsecuritystart
		, m.austerityratio
		, m.extremeausterityratio
		, round (m.desiredmonthlyspendpreretirement, 2) as desiredmonthlyspendpreretirement
		, round(m.desiredmonthlyspendpostretirement, 2) as desiredmonthlyspendpostretirement
		, m.percent401ktraditional
		, m.sixtyfortylong
		, r.averageincomeinflectionage
		, m.nummonthscashonhand
		, m.nummonthsmidbucketonhand
		, m.recessionchecklookbackmonths
		, m.recessionrecoverypointmodifier
		, m.livinlargeratio
		, m.livinlargenetworthtrigger
		, m.rebalancefrequency
		, m.extremeausteritynetworthtrigger
		, row_number() over(
			partition by m.id, m.clade, r.majorversion, r.minorversion 
			order by r.funpointsatendofsim50 desc, 
                 r.networthatendofsim50 desc, 
                 r.funpointsatendofsim90 desc,
                 r.networthatendofsim90 desc 
			) as rank_in_model
	from personalfinance.singlemodelrunresult r
	left join personalfinance.montecarlomodel m on r.modelid = m.id
	where m.id is not null
	and r.majorversion = 0
	and r.minorversion = 20
	and r.bankruptcyrateatendofsim <= 0
), best_runs_by_model as (
	select * from ranked_by_model where rank_in_model = 1
), ranked_models as (
	select 
		  model_id
		--, parenta
		--, parentb
		, withdrawalstrategy
		, clade
		, majorversion
		, minorversion
		, desiredmonthlyspendpreretirement
		, desiredmonthlyspendpostretirement
		, funpointsatendofsim50
		, averageincomeinflectionage
		, networthatendofsim50
		, funpointsatendofsim90
		, networthatendofsim90
		, retirementdate
		, socialsecuritystart
		, austerityratio
		, extremeausterityratio
		, percent401ktraditional
		, sixtyfortylong
		, nummonthscashonhand
		, nummonthsmidbucketonhand
		, recessionchecklookbackmonths
		, recessionrecoverypointmodifier
		, livinlargeratio
		, livinlargenetworthtrigger
		, rebalancefrequency
		, extremeausteritynetworthtrigger
		, row_number() over(
			partition by clade 
			order by funpointsatendofsim50 desc, 
                 networthatendofsim50 desc, 
                 funpointsatendofsim90 desc,
                 networthatendofsim90 desc 
			) as rank_in_clade
	from best_runs_by_model
)
select * from ranked_models 
where rank_in_clade <= 5 
order by 
	funpointsatendofsim50 desc, 
	networthatendofsim50 desc, 
	funpointsatendofsim90 desc,
	networthatendofsim90 desc 
limit 100



	
