namespace ChecadorWhatsApp.Models;

public enum TipoChecada
{
    Entrada,
    SalidaComer,
    RegresoComer,
    Salida
}

public enum EstatusChecada
{
    ATiempo,
    Retardo,
    FueraDeZona,
    Normal
}

public class Checada
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EmpleadoId { get; set; } = string.Empty;
    public string EmpleadoNombre { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; } = DateTime.Now;
    public TipoChecada Tipo { get; set; } = TipoChecada.Entrada;
    
    // GPS Geolocation fields
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public string? NombreUbicacion { get; set; }
    public string? DireccionUbicacion { get; set; }
    
    // Sede en donde debe estar laborando el empleado
    public string? SucursalAsignadaId { get; set; }
    public string? SucursalAsignadaNombre { get; set; }

    // Compatibilidad hacia atrás
    public string? SucursalCercanaId { get => SucursalAsignadaId; set => SucursalAsignadaId = value; }
    public string? SucursalCercanaNombre { get => SucursalAsignadaNombre; set => SucursalAsignadaNombre = value; }

    public double? DistanciaMetros { get; set; }
    public bool EnGeocerca { get; set; } = true;
    
    // Punctuality & Status
    public EstatusChecada Estatus { get; set; } = EstatusChecada.Normal;
    public int MinutosRetardo { get; set; } = 0;
    
    // Metadata
    public string Origen { get; set; } = "App Móvil (Android)";
    public string? FotoSelfieUrl { get; set; }
    public string? MensajeOriginal { get; set; }
    public string? Notas { get; set; }

    public string TipoTexto => Tipo switch
    {
        TipoChecada.Entrada => "Entrada",
        TipoChecada.SalidaComer => "Salida a Comer",
        TipoChecada.RegresoComer => "Regreso de Comer",
        TipoChecada.Salida => "Salida de Jornada",
        _ => Tipo.ToString()
    };

    public string EstatusTexto => Estatus switch
    {
        EstatusChecada.ATiempo => "A Tiempo",
        EstatusChecada.Retardo => $"Retardo ({MinutosRetardo} min)",
        EstatusChecada.FueraDeZona => "Fuera de Sede Asignada",
        _ => "Normal"
    };

    public string? EnlaceGoogleMaps => (Latitud.HasValue && Longitud.HasValue) 
        ? $"https://www.google.com/maps?q={Latitud.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Longitud.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        : null;
}
