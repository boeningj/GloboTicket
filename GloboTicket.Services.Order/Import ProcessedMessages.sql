INSERT INTO dbo.ProcessedMessages
(
    MessageId,
    ProcessedAt
)
SELECT
    MessageId,
    CAST(ProcessedAt AS datetime2(7))
FROM dbo.ProcessedMessages_Temp;

SELECT COUNT(*) FROM dbo.ProcessedMessages;
SELECT TOP 10 * FROM dbo.ProcessedMessages;

DROP TABLE dbo.ProcessedMessages_Temp;