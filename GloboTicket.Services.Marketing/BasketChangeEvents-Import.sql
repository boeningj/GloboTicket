INSERT INTO dbo.BasketChangeEvents
(
    Id,
    UserId,
    EventId,
    InsertedAt,
    BasketChangeType
)
SELECT
    CAST(Id AS uniqueidentifier),
    CAST(UserId AS uniqueidentifier),
    CAST(EventId AS uniqueidentifier),
    CAST(InsertedAt AS datetimeoffset(7)),
    CAST(BasketChangeType AS int)
FROM dbo.BasketChangeEvents_Temp;

--Verify
SELECT COUNT(*) FROM dbo.BasketChangeEvents;
SELECT TOP 10 * FROM dbo.BasketChangeEvents;

DROP TABLE dbo.BasketChangeEvents_Temp;