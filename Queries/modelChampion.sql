
insert into personalfinance.modelchampion (modelid,championdesignateddate) values
('ad45f4cd-b4d8-4108-a901-b00c6876e637', CURRENT_TIMESTAMP);

update personalfinance.configvalue 
set configvalue = 'ad45f4cd-b4d8-4108-a901-b00c6876e637'
where configkey = 'ChampionModelId'

select * from personalfinance.configvalue 