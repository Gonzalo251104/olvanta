-- Run in Olvanta with an account allowed to show query plans.
-- Use an isolated database with recorded cash movements for a representative sample.
DECLARE @CashSessionId int = (
    SELECT TOP (1) CashSessionId
    FROM [commercial].[Events]
    WHERE CashSessionId IS NOT NULL
    GROUP BY CashSessionId
    ORDER BY COUNT_BIG(*) DESC, CashSessionId
);

IF @CashSessionId IS NULL
    THROW 50001, 'Registre movimientos de caja antes de revisar el plan.', 1;

SET STATISTICS IO ON;
SET STATISTICS TIME ON;
SET STATISTICS XML ON;

SELECT COALESCE(SUM(CashDelta), 0) AS CashDelta
FROM [commercial].[Events]
WHERE CashSessionId = @CashSessionId;

SET STATISTICS XML OFF;
SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
