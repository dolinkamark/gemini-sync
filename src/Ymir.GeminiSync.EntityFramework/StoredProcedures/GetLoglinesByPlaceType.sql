CREATE OR ALTER PROCEDURE [dbo].[GetLoglinesByPlaceType]
    @CustomerId INT,
    @PlaceTypeDescription NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        al.CustomerId,
        l.LogLineId,
        l.Time,
        al.AgreementLineId,
        al.AgreementId,
        a.ExternalAgreementId,
        p.PlaceNr,
        a.Bid,
        a.BuildingType,
        al.UnitId,
        f.Name AS FractionName,
        u.Name,
        u.ShortName
    FROM dbo.LogLine AS l
    INNER JOIN dbo.AgreementLine AS al
        ON al.CustomerId = l.CustomerId
        AND al.AgreementLineId = l.AgreementLineId
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
        WHERE
            al.AgreementLineId != 0
            AND al.CustomerId = @CustomerId
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
