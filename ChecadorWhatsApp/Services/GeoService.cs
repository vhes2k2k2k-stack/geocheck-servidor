using ChecadorWhatsApp.Models;

namespace ChecadorWhatsApp.Services;

public interface IGeoService
{
    double CalcularDistanciaMetros(double lat1, double lon1, double lat2, double lon2);
    (Sucursal? sucursalAsignada, double distanciaMetros, bool dentroDeZona) EvaluarGeocerca(double lat, double lon, IEnumerable<Sucursal> sucursales, string? sucursalAsignadaId = null);
    string FormatearDistancia(double distanciaMetros);
    double NormalizarCoordenada(double valor, bool esLatitud);
}

public class GeoService : IGeoService
{
    private const double RadioTierraMetros = 6371000.0;

    public double NormalizarCoordenada(double valor, bool esLatitud)
    {
        if (double.IsNaN(valor) || double.IsInfinity(valor)) return 0;
        double maxPermitido = esLatitud ? 90.0 : 180.0;
        if (Math.Abs(valor) <= maxPermitido) return valor;

        // Auto-corregir coordenadas ingresadas sin punto decimal (ej: -101921107 -> -101.921107)
        double v = valor;
        while (Math.Abs(v) > maxPermitido && Math.Abs(v) > 0.000001)
        {
            v /= 10.0;
        }
        return Math.Round(v, 6);
    }

    public double CalcularDistanciaMetros(double lat1, double lon1, double lat2, double lon2)
    {
        lat1 = NormalizarCoordenada(lat1, true);
        lon1 = NormalizarCoordenada(lon1, false);
        lat2 = NormalizarCoordenada(lat2, true);
        lon2 = NormalizarCoordenada(lon2, false);

        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);

        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return Math.Round(RadioTierraMetros * c, 1);
    }

    public (Sucursal? sucursalAsignada, double distanciaMetros, bool dentroDeZona) EvaluarGeocerca(
        double lat, double lon, IEnumerable<Sucursal> sucursales, string? sucursalAsignadaId = null)
    {
        lat = NormalizarCoordenada(lat, true);
        lon = NormalizarCoordenada(lon, false);

        var lista = sucursales.Where(s => s.Activa).ToList();
        if (!lista.Any())
        {
            return (null, 0, true);
        }

        // Si el empleado tiene una sede asignada en donde debe laborar
        if (!string.IsNullOrEmpty(sucursalAsignadaId))
        {
            var sucursalAsignada = lista.FirstOrDefault(s => s.Id.Equals(sucursalAsignadaId, StringComparison.OrdinalIgnoreCase));
            if (sucursalAsignada != null)
            {
                // La distancia obligatoria se calcula respecto a su sede asignada
                double distAsignada = CalcularDistanciaMetros(lat, lon, sucursalAsignada.Latitud, sucursalAsignada.Longitud);
                bool enZona = distAsignada <= sucursalAsignada.RadioMetros;
                return (sucursalAsignada, distAsignada, enZona);
            }
        }

        // Si es un colaborador sin sede fija asignada (multi-sucursal o en ruta), se calcula respecto a la más cercana
        Sucursal? masCercana = null;
        double menorDistancia = double.MaxValue;

        foreach (var suc in lista)
        {
            double dist = CalcularDistanciaMetros(lat, lon, suc.Latitud, suc.Longitud);
            if (dist < menorDistancia)
            {
                menorDistancia = dist;
                masCercana = suc;
            }
        }

        bool dentro = masCercana != null && menorDistancia <= masCercana.RadioMetros;
        return (masCercana, menorDistancia, dentro);
    }

    public string FormatearDistancia(double distanciaMetros)
    {
        if (distanciaMetros < 1000)
        {
            return $"{Math.Round(distanciaMetros)} m";
        }
        return $"{Math.Round(distanciaMetros / 1000.0, 2)} km";
    }

    private static double ToRadians(double angleInDegrees)
    {
        return (Math.PI / 180.0) * angleInDegrees;
    }
}
