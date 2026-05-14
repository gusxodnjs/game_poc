using System;

public static class CellMapping
{
    public const double GridResolution = 0.001;

    public static (long latCell, long lonCell) ToCell(double latitude, double longitude)
    {
        long latCell = (long)Math.Floor(latitude / GridResolution);
        long lonCell = (long)Math.Floor(longitude / GridResolution);
        return (latCell, lonCell);
    }

    public static string ToCellId(double latitude, double longitude)
    {
        var (lat, lon) = ToCell(latitude, longitude);
        return $"{lat}/{lon}";
    }

    public static double DistanceToBoundaryMeters(double latitude, double longitude)
    {
        double latFrac = latitude / GridResolution - Math.Floor(latitude / GridResolution);
        double lonFrac = longitude / GridResolution - Math.Floor(longitude / GridResolution);
        double minFracLat = Math.Min(latFrac, 1 - latFrac);
        double minFracLon = Math.Min(lonFrac, 1 - lonFrac);
        double latMeters = minFracLat * GridResolution * 111000;
        double lonMeters = minFracLon * GridResolution * 111000 * Math.Cos(latitude * Math.PI / 180);
        return Math.Min(latMeters, lonMeters);
    }
}
