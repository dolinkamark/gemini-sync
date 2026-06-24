CREATE OR ALTER PROCEDURE dbo.GetAgreementConnectionsByCustomerId
    @CustomerId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ap.[CustomerId],
        a.[ExternalAgreementId],
        ap.[AgreementId],
        ap.[PlaceNr],
        ppt.[PlaceTypeId],
        pt.[Description],
        ap.[FromDate],
        ap.[ToDate],
        ap.[CreatedAt],
        ap.[UpdatedAt]
    FROM dbo.[AgreementPlaceHistory] AS ap
    INNER JOIN dbo.[Agreement] AS a
        ON a.[AgreementId] = ap.[AgreementId]
       AND a.[GPSLSCustomerId] = ap.[CustomerId]
       AND a.[PASystem] = ap.[PASystem]
    FULL OUTER JOIN dbo.[Place_PlaceType] AS ppt
        ON ppt.[CustomerId] = ap.[CustomerId]
       AND ppt.[PASystem] = ap.[PASystem]
       AND ppt.[PlaceNr] = ap.[PlaceNr]
    FULL OUTER JOIN dbo.[PlaceType] AS pt
        ON pt.[PlaceTypeId] = ppt.[PlaceTypeId]
       AND pt.[CustomerId] = ppt.[CustomerId]
    WHERE ap.[CustomerId] = @CustomerId
    ORDER BY ap.[AgreementId];
END;
GO