CREATE OR ALTER PROCEDURE [dbo].[GetAgreementPlacesByPlaceType]
    @CustomerId INT,
    @PlaceTypeDescription NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ap.[CustomerId]
          ,a.[ExternalAgreementId]
          ,ap.[AgreementId]
          ,ap.[PlaceNr]
          ,a.[NrOfOccupancyUnits]
          ,ap.[FromDate]
          ,ap.[ToDate]
          ,ap.[CreatedAt]
          ,ap.[UpdatedAt]
    FROM [dbo].[AgreementPlaceHistory] AS ap
    INNER JOIN [dbo].[Agreement] a ON a.AgreementId = ap.AgreementId AND a.GPSLSCustomerId = ap.CustomerId AND a.PASystem = ap.PASystem
    INNER JOIN [dbo].[Place] p ON p.PlaceNr = ap.PlaceNr AND p.AgreementId = ap.AgreementId AND p.GPSLSCustomerId = ap.CustomerId AND p.PASystem = ap.PASystem
    WHERE ap.CustomerId = @CustomerId
      AND p.Exemption = 0
      AND ap.FromDate <= GETDATE()
      AND EXISTS (
            SELECT 1
            FROM dbo.Place_PlaceType AS ppt
            INNER JOIN dbo.PlaceType AS pt
                ON pt.PlaceTypeId = ppt.PlaceTypeId
               AND pt.CustomerId = ppt.CustomerId
            WHERE ppt.PlaceNr = ap.PlaceNr
              AND ppt.PASystem = ap.PASystem
              AND pt.Description = @PlaceTypeDescription
        );
END;
GO