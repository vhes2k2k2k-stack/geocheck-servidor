namespace ChecadorWhatsApp.Models;

public class Sucursal
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    public string Nombre { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public double RadioMetros { get; set; } = 200; // Geofence radius in meters (e.g. 200m)
    public bool Activa { get; set; } = true;
    public string ColorHex { get; set; } = "#2563EB"; // Color for Leaflet map circle
}
