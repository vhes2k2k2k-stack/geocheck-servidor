namespace ChecadorWhatsApp.Models;

public class Empleado
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    public string Codigo { get; set; } = string.Empty; // Código de empleado (ej: "EMP01")
    public string Pin { get; set; } = "1234"; // PIN de acceso en app móvil
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Departamento { get; set; } = "Operaciones";
    public string Puesto { get; set; } = "Colaborador";
    public string? SucursalId { get; set; }
    public string HoraEntrada { get; set; } = "08:00"; // "HH:mm"
    public string HoraSalida { get; set; } = "17:00";  // "HH:mm"
    public int ToleranciaMinutos { get; set; } = 15;
    public bool PermitirRemoto { get; set; } = false;
    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;

    public TimeSpan GetHoraEntradaSpan()
    {
        return TimeSpan.TryParse(HoraEntrada, out var ts) ? ts : new TimeSpan(8, 0, 0);
    }

    public TimeSpan GetHoraSalidaSpan()
    {
        return TimeSpan.TryParse(HoraSalida, out var ts) ? ts : new TimeSpan(17, 0, 0);
    }
}
