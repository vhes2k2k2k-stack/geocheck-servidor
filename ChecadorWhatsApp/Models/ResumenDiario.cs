namespace ChecadorWhatsApp.Models;

public class ResumenDiario
{
    public string EmpleadoId { get; set; } = string.Empty;
    public string EmpleadoNombre { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    
    // Marcajes
    public DateTime? HoraEntrada { get; set; }
    public EstatusChecada EstatusEntrada { get; set; } = EstatusChecada.Normal;
    public int MinutosRetardo { get; set; } = 0;
    
    public DateTime? HoraSalidaComer { get; set; }
    public DateTime? HoraRegresoComer { get; set; }
    public double MinutosComida => (HoraSalidaComer.HasValue && HoraRegresoComer.HasValue && HoraRegresoComer > HoraSalidaComer)
        ? Math.Round((HoraRegresoComer.Value - HoraSalidaComer.Value).TotalMinutes, 1)
        : 0;
    
    public DateTime? HoraSalida { get; set; }

    // Totales calculados
    public double HorasTotales => (HoraEntrada.HasValue && HoraSalida.HasValue && HoraSalida > HoraEntrada)
        ? Math.Round((HoraSalida.Value - HoraEntrada.Value).TotalHours, 2)
        : (HoraEntrada.HasValue && Fecha.Date == DateTime.Now.Date)
            ? Math.Round((DateTime.Now - HoraEntrada.Value).TotalHours, 2)
            : 0;

    public double HorasEfectivas
    {
        get
        {
            var total = HorasTotales;
            var comidaHoras = MinutosComida / 60.0;
            return Math.Max(0, Math.Round(total - comidaHoras, 2));
        }
    }

    public bool CumplioGeocerca { get; set; } = true;
    public int TotalMarcajes { get; set; } = 0;
    public string EstadoJornada => (HoraEntrada.HasValue, HoraSalida.HasValue) switch
    {
        (true, true) => "Jornada Completada",
        (true, false) when HoraSalidaComer.HasValue && !HoraRegresoComer.HasValue => "En Comida",
        (true, false) => "Activo / En Turno",
        _ => "Sin Entrada Registrada"
    };
}
