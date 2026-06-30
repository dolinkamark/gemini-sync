CREATE OR ALTER PROCEDURE [dbo].[GetGarbageBinCollectionsByPlaceType]
    @CustomerId INT,
    @PlaceTypeDescription NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        al.CustomerId,
        al.AgreementLineId,
        al.AgreementId,
        a.ExternalAgreementId,
        a.Bid,
        a.BuildingType,
        a.NrOfOccupancyUnits,
        al.UnitId,
        al.PlaceNr,
        al.Termin,
        al.Frequence,
        f.Name AS FractionName,
        u.ShortName,
        u.HasLock,
        al.RegDate,
        al.FromDate,
        al.ToDate,
        al.LastChanged
    FROM dbo.AgreementLine AS al
    INNER JOIN dbo.Agreement AS a
        ON a.GPSLSCustomerId = al.CustomerId
       AND a.AgreementId = al.AgreementId
    INNER JOIN dbo.Unit AS u
        ON u.UnitId = al.UnitId
       AND u.CustomerId = al.CustomerId
    INNER JOIN dbo.Place AS p
        ON p.PlaceNr = al.PlaceNr
       AND p.GPSLSCustomerId = al.CustomerId
       AND p.AgreementId = al.AgreementId
    INNER JOIN dbo.Unit_Fraction AS uf
        ON uf.CustomerId = u.CustomerId
       AND uf.UnitId = u.UnitId
    INNER JOIN dbo.Fraction AS f
        ON f.CustomerId = uf.CustomerId
       AND f.FractionId = uf.FractionId
       AND f.Name COLLATE Latin1_General_100_CI_AS LIKE N'%restavfall%'
    WHERE
        al.CustomerId = @CustomerId
        AND al.Status <> 99
        AND p.Status <> 99
        AND (
            GETDATE() < al.ToDate
            OR al.ToDate = '1900-01-01T00:00:00.000'
        )
        AND EXISTS (
            SELECT 1
            FROM dbo.Place_PlaceType AS ppt
            INNER JOIN dbo.PlaceType AS pt
                ON pt.PlaceTypeId = ppt.PlaceTypeId
               AND pt.CustomerId = ppt.CustomerId
            WHERE ppt.PlaceNr = al.PlaceNr
              AND ppt.CustomerId = al.CustomerId
              AND pt.Description = @PlaceTypeDescription
        );
END;
GO
