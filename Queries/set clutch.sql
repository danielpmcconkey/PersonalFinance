

update personalfinance.configvalue set configvalue = 'true' where configkey = 'MonteCarloClutch';
SELECT id, configkey, configvalue FROM personalfinance.configvalue where configkey = 'MonteCarloClutch';


update personalfinance.configvalue set configvalue = 'false' where configkey = 'MonteCarloClutch';
SELECT id, configkey, configvalue FROM personalfinance.configvalue where configkey = 'MonteCarloClutch';