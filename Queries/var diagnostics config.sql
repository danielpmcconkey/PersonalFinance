-- Inserts the two config values needed by VarDiagnosticsWriter into the shared config table.
-- Run this once. All projects default to GenerateVarDiagnostics=false (no-op).
-- To generate the diagnostic HTML, either:
--   (a) temporarily set GenerateVarDiagnostics=true in MonteCarloCLI/appsettings.json, or
--   (b) UPDATE this row to 'true', run MonteCarloCLI, then set it back to 'false'.

INSERT INTO personalfinance.configvalue (id, configkey, configvalue)
SELECT max(id)+1, 'GenerateVarDiagnostics', 'false'
FROM personalfinance.configvalue;

INSERT INTO personalfinance.configvalue (id, configkey, configvalue)
SELECT max(id)+1, 'VarDiagnosticsOutputPath', '/media/dan/fdrive/codeprojects/PersonalFinance/OutputFiles/var_diagnostic.html'
FROM personalfinance.configvalue;

-- Verify:
SELECT id, configkey, configvalue
FROM personalfinance.configvalue
WHERE configkey IN ('GenerateVarDiagnostics', 'VarDiagnosticsOutputPath');
