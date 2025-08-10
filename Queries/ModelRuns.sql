/*
select 
	m.id
	, personid
	, parenta
	, parentb
	, modelcreateddate
	, simstartdate
	, simenddate
	, retirementdate
	, socialsecuritystart
	, austerityratio
	, extremeausterityratio
	, extremeausteritynetworthtrigger
	, rebalancefrequency
	, nummonthscashonhand
	, nummonthsmidbucketonhand
	, nummonthspriortoretirementtobeginrebalance
	, recessionchecklookbackmonths
	, recessionrecoverypointmodifier
	, desiredmonthlyspendpreretirement
	, desiredmonthlyspendpostretirement
	, percent401ktraditional
from personalfinance.singlemodelrunresult r
left join personalfinance.montecarlomodel m on r.modelid = m.id
where m.id is not null
and majorversion = 0
and minorversion = 2
group by 
	m.id
	, personid
	, parenta
	, parentb
	, modelcreateddate
	, simstartdate
	, simenddate
	, retirementdate
	, socialsecuritystart
	, austerityratio
	, extremeausterityratio
	, extremeausteritynetworthtrigger
	, rebalancefrequency
	, nummonthscashonhand
	, nummonthsmidbucketonhand
	, nummonthspriortoretirementtobeginrebalance
	, recessionchecklookbackmonths
	, recessionrecoverypointmodifier
	, desiredmonthlyspendpreretirement
	, desiredmonthlyspendpostretirement
	, percent401ktraditional
order by max(r.funpointsatendofsim50) desc
limit 10


"31512df8-5c46-4f05-a216-35fbfe37e308"
"681160b4-608d-48f4-8f6f-17705f029b09"
"0acc07a1-85dc-4a67-bb41-d887239717fa"
"b958b1e7-1dbc-441e-a024-85cb0a14e3a2"
"a66435f5-0fb7-498b-ad92-8fb72d071614"
"9d7b05ed-b39a-44a2-a438-6122e0df07a9"
"c42ade3c-a3a8-4cc7-9e53-b6606b05e0b0"
"4093a49b-4820-41e7-bf71-e8b03a0424f5"
"44a8b577-4fb8-4bf1-b3ec-39764552e625"
"e773cadf-e7a7-469a-89ca-67bf3a562e72"

*/

select 
r.modelId
, r.rundate
, r.numlivesrun
, r.majorversion
, r.minorversion
, r.networthatendofsim50
, round(r.funpointsatendofsim50,2) as funpointsatendofsim50
, r.spendatendofsim50
, r.taxatendofsim50
, r.bankruptcyrateatendofsim
, m.retirementdate
, m.socialsecuritystart
, m.austerityratio
, m.extremeausterityratio
, m.desiredmonthlyspendpreretirement
, m.desiredmonthlyspendpostretirement
, m.percent401ktraditional
, *
from personalfinance.singlemodelrunresult r
left join personalfinance.montecarlomodel m on r.modelid = m.id
where m.id is not null
and majorversion = 0
and minorversion = 3
order by r.funpointsatendofsim50 desc