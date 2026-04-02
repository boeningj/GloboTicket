INSERT INTO dbo.OrderLines
(
    OrderLineId,
    OrderId,
    Price,
    TicketAmount,
    EventId,
    EventName,
    EventDate,
    VenueName,
    VenueCity,
    VenueCountry,
    Message
)
SELECT
    CAST(OrderLineId AS uniqueidentifier),
    CAST(OrderId AS uniqueidentifier),
    CAST(Price AS int),
    CAST(TicketAmount AS int),
    CAST(EventId AS uniqueidentifier),
    EventName,
    CAST(EventDate AS datetime2(7)),
    VenueName,
    VenueCity,
    VenueCountry,
    Message
FROM dbo.OrderLines_Temp;

SELECT COUNT(*) FROM dbo.OrderLines;
SELECT TOP 10 * FROM dbo.OrderLines;

--Check foreign key integrity (should return 0 rows)
SELECT *
FROM OrderLines ol
LEFT JOIN Orders o ON ol.OrderId = o.Id
WHERE o.Id IS NULL;

DROP TABLE dbo.OrderLines_Temp;