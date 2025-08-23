with childless as (
	select
	  p.id,
	  count(c1.id) as num_sons,
	  count(c2.id) as num_daughters
	from
	  personalfinance.montecarlomodel p 
	  left outer join personalfinance.montecarlomodel c1 on c1.parenta = p.id
	  left outer join personalfinance.montecarlomodel c2 on c2.parentb = p.id
	  where p.modelcreateddate <= CURRENT_DATE - 1
	group by
	  p.id
	having
	  count(c1.id) = 0 and count(c2.id) = 0
)
delete from personalfinance.singlemodelrunresult
where modelid in (select id from childless)


with childless as (
	select
	  p.id,
	  count(c1.id) as num_sons,
	  count(c2.id) as num_daughters
	from
	  personalfinance.montecarlomodel p 
	  left outer join personalfinance.montecarlomodel c1 on c1.parenta = p.id
	  left outer join personalfinance.montecarlomodel c2 on c2.parentb = p.id
	  where p.modelcreateddate <= CURRENT_DATE - 1
	group by
	  p.id
	having
	  count(c1.id) = 0 and count(c2.id) = 0
)
delete from personalfinance.montecarlomodel
where id in (select id from childless)
