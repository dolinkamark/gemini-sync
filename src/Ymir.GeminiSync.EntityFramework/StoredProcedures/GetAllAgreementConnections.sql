CREATE OR ALTER PROCEDURE dbo.GetAllAgreementConnections
    @CustomerId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ap.[CustomerId],
        ap.[AgreementId],
        a.[ExternalAgreementId],
        a.[Type] AS AgreementType,
        a.[GnrBnrFnrSnr],
        a.[Bid],
        a.[NrOfOccupancyUnits],
        ap.[PlaceNr],
        pt.[PlaceTypeId],
        pt.[Description] AS PlaceType,
        ap.[FromDate],
        a.[BuildingType],
        ap.[ToDate],
        ap.[CreatedAt],
        ap.[UpdatedAt]
    FROM dbo.AgreementPlaceHistory AS ap
    INNER JOIN dbo.Agreement AS a
        ON a.AgreementId = ap.AgreementId
       AND a.GPSLSCustomerId = ap.CustomerId
       AND a.PASystem = ap.PASystem
    INNER JOIN dbo.Place_PlaceType AS ppt
        ON ppt.CustomerId = ap.CustomerId
       AND ppt.PlaceNr = ap.PlaceNr
       AND ppt.PASystem = ap.PASystem
    INNER JOIN dbo.PlaceType AS pt
        ON pt.PlaceTypeId = ppt.PlaceTypeId
       AND pt.CustomerId = ppt.CustomerId
    WHERE ap.CustomerId = @CustomerId
       AND a.ExternalAgreementId IS NOT NULL
       AND ap.FromDate <= GETDATE()
END;
GO