INSERT INTO dbo.Orders
(
    Id,
    UserId,
    OrderTotal,
    OrderPlaced,
    OrderPaid,
    Message
)
SELECT
    CAST(Id AS uniqueidentifier),
    CAST(UserId AS uniqueidentifier),
    CAST(OrderTotal AS int),
    CAST(OrderPlaced AS datetime2(7)),
    CAST(OrderPaid AS bit),
    Message
FROM dbo.Orders_Temp;

SELECT COUNT(*) FROM dbo.Orders;
SELECT TOP 10 * FROM dbo.Orders;

SELECT *
FROM Orders o
LEFT JOIN Customers c ON o.UserId = c.CustomerId
WHERE c.CustomerId IS NULL;

DROP TABLE dbo.Orders_Temp;