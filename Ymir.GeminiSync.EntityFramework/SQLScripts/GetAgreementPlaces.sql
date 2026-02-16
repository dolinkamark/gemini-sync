SELECT ap.[CustomerId]
      ,a.[ExternalAgreementId]
      ,ap.[AgreementId]
      ,ap.[PlaceNr]
      ,ap.[FromDate]
      ,ap.[ToDate]
      ,ap.[CreatedAt]
      ,ap.[UpdatedAt]
FROM [dbo].[AgreementPlaceHistory] AS ap
INNER JOIN [dbo].[Agreement] a ON a.AgreementId = ap.AgreementId AND a.GPSLSCustomerId = ap.CustomerId AND a.PASystem = ap.PASystem
WHERE ap.CustomerId = 270
  AND EXISTS (
        SELECT 1
        FROM dbo.Place_PlaceType AS ppt
        INNER JOIN dbo.PlaceType AS pt
            ON pt.PlaceTypeId = ppt.PlaceTypeId
           AND pt.CustomerId = ppt.CustomerId
        WHERE ppt.PlaceNr = ap.PlaceNr
          AND ppt.PASystem = ap.PASystem
          AND pt.Description = 'Nedgravd privat'
    )