using System.Text.Json.Serialization;

namespace ChecadorWhatsApp.Models;

// ====== DTOs DE LA APLICACIÓN MÓVIL (APK ANDROID) ======

public class ChecadaMovilRequest
{
    [JsonPropertyName("empleadoId")]
    public string EmpleadoId { get; set; } = string.Empty;

    [JsonPropertyName("pin")]
    public string? Pin { get; set; }

    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = "Entrada"; // "Entrada", "SalidaComer", "RegresoComer", "Salida"

    [JsonPropertyName("latitud")]
    public double Latitud { get; set; }

    [JsonPropertyName("longitud")]
    public double Longitud { get; set; }

    [JsonPropertyName("precisionMetros")]
    public double? PrecisionMetros { get; set; }

    [JsonPropertyName("dispositivo")]
    public string? Dispositivo { get; set; } // Marca y modelo del celular

    [JsonPropertyName("fotoBase64")]
    public string? FotoBase64 { get; set; } // Selfie en base64 capturada al registrar Entrada
}

public class ChecadaMovilResponse
{
    [JsonPropertyName("exito")]
    public bool Exito { get; set; }

    [JsonPropertyName("mensaje")]
    public string Mensaje { get; set; } = string.Empty;

    [JsonPropertyName("checada")]
    public Checada? Checada { get; set; }

    [JsonPropertyName("fotoSelfieUrl")]
    public string? FotoSelfieUrl { get; set; }

    [JsonPropertyName("enGeocerca")]
    public bool EnGeocerca { get; set; }

    [JsonPropertyName("distanciaMetros")]
    public double? DistanciaMetros { get; set; }

    [JsonPropertyName("sucursalAsignada")]
    public string? SucursalAsignada { get; set; }

    [JsonPropertyName("sucursalCercana")]
    public string? SucursalCercana { get => SucursalAsignada; set => SucursalAsignada = value; }

    [JsonPropertyName("estatusTexto")]
    public string EstatusTexto { get; set; } = string.Empty;

    [JsonPropertyName("minutosRetardo")]
    public int MinutosRetardo { get; set; }
}

public class EstadoMovilResponse
{
    [JsonPropertyName("empleadoId")]
    public string EmpleadoId { get; set; } = string.Empty;

    [JsonPropertyName("empleadoNombre")]
    public string EmpleadoNombre { get; set; } = string.Empty;

    [JsonPropertyName("departamento")]
    public string Departamento { get; set; } = string.Empty;

    [JsonPropertyName("puesto")]
    public string Puesto { get; set; } = string.Empty;

    [JsonPropertyName("horario")]
    public string Horario { get; set; } = string.Empty;

    [JsonPropertyName("estadoJornada")]
    public string EstadoJornada { get; set; } = "Sin Entrada";

    [JsonPropertyName("horaEntrada")]
    public string? HoraEntrada { get; set; }

    [JsonPropertyName("horaSalidaComer")]
    public string? HoraSalidaComer { get; set; }

    [JsonPropertyName("horaRegresoComer")]
    public string? HoraRegresoComer { get; set; }

    [JsonPropertyName("horaSalida")]
    public string? HoraSalida { get; set; }

    [JsonPropertyName("tiempoTrabajado")]
    public string? TiempoTrabajado { get; set; }

    [JsonPropertyName("checadasHoy")]
    public List<Checada> ChecadasHoy { get; set; } = new();
}

public class VerificarNipRequest
{
    [JsonPropertyName("empleadoId")]
    public string EmpleadoId { get; set; } = string.Empty;

    [JsonPropertyName("pin")]
    public string? Pin { get; set; }
}

public class LoginMovilRequest
{
    [JsonPropertyName("identificador")]
    public string Identificador { get; set; } = string.Empty; // ID o Código o Teléfono

    [JsonPropertyName("pin")]
    public string Pin { get; set; } = string.Empty;
}

public class LoginMovilResponse
{
    [JsonPropertyName("autenticado")]
    public bool Autenticado { get; set; }

    [JsonPropertyName("mensaje")]
    public string Mensaje { get; set; } = string.Empty;

    [JsonPropertyName("empleado")]
    public Empleado? Empleado { get; set; }
}

// ====== DASHBOARD & REPORTES ======

public class DashboardStatsDto
{
    public int TotalEmpleadosActivos { get; set; }
    public int TotalChecadasHoy { get; set; }
    public int EntradasATiempoHoy { get; set; }
    public int RetardosHoy { get; set; }
    public int FueraDeZonaHoy { get; set; }
    public int EnComidaAhora { get; set; }
    public int JornadaCompletadaHoy { get; set; }
    public List<Checada> UltimasChecadas { get; set; } = new();
}

public class ReportFilterDto
{
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string? EmpleadoId { get; set; }
    public string? Departamento { get; set; }
    public string? SucursalId { get; set; }
    public string? Tipo { get; set; }
    public string? Estatus { get; set; }
}
