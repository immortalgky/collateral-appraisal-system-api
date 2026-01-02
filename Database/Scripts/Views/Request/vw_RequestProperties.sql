CREATE
OR ALTER
VIEW request.vw_RequestProperties AS
SELECT RequestId,
       PropertyType,
       BuildingType,
       SellingPrice
FROM request.RequestProperties