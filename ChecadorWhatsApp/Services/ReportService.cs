using System.Text;
using ChecadorWhatsApp.Models;

namespace ChecadorWhatsApp.Services;

public interface IReportService
{
    List<ResumenDiario> GenerarResumenDiario(List<Checada> checadas, List<Empleado> empleados, DateTime? fechaInicio = null, DateTime? fechaFin = null);
    DashboardStatsDto ObtenerDashboardStats(List<Checada> checadas, List<Empleado> empleados);
    byte[] ExportarCsvResumen(List<ResumenDiario> resumenes);
    byte[] ExportarCsvBitacora(List<Checada> checadas);
}

public class ReportService : IReportService
{
    public List<ResumenDiario> GenerarResumenDiario(List<Checada> checadas, List<Empleado> empleados, DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        var dictEmpleados = empleados.ToDictionary(e => e.Id, e => e);

        // Agrupar por empleado y por fecha (solo día)
        var grupos = checadas
            .Where(c => (!fechaInicio.HasValue || c.FechaHora.Date >= fechaInicio.Value.Date) &&
                        (!fechaFin.HasValue || c.FechaHora.Date <= fechaFin.Value.Date))
            .GroupBy(c => new { c.EmpleadoId, Fecha = c.FechaHora.Date })
            .OrderByDescending(g => g.Key.Fecha)
            .ThenBy(g => g.Key.EmpleadoId);

        var resultado = new List<ResumenDiario>();

        foreach (var grupo in grupos)
        {
            var empId = grupo.Key.EmpleadoId;
            dictEmpleados.TryGetValue(empId, out var emp);

            var punches = grupo.OrderBy(c => c.FechaHora).ToList();

            var entrada = punches.FirstOrDefault(c => c.Tipo == TipoChecada.Entrada);
            var salidaComer = punches.FirstOrDefault(c => c.Tipo == TipoChecada.SalidaComer);
            var regresoComer = punches.FirstOrDefault(c => c.Tipo == TipoChecada.RegresoComer);
            var salida = punches.LastOrDefault(c => c.Tipo == TipoChecada.Salida);

            // Validar cumplimiento de geocerca (si alguna checada estuvo fuera de zona y el empleado no es remoto)
            bool cumplioGps = true;
            if (emp == null || !emp.PermitirRemoto)
            {
                cumplioGps = punches.All(c => c.EnGeocerca);
            }

            var item = new ResumenDiario
            {
                EmpleadoId = empId,
                EmpleadoNombre = emp?.Nombre ?? punches.First().EmpleadoNombre,
                Departamento = emp?.Departamento ?? punches.First().Departamento,
                Fecha = grupo.Key.Fecha,
                HoraEntrada = entrada?.FechaHora,
                EstatusEntrada = entrada?.Estatus ?? EstatusChecada.Normal,
                MinutosRetardo = entrada?.MinutosRetardo ?? 0,
                HoraSalidaComer = salidaComer?.FechaHora,
                HoraRegresoComer = regresoComer?.FechaHora,
                HoraSalida = salida?.FechaHora,
                CumplioGeocerca = cumplioGps,
                TotalMarcajes = punches.Count
            };

            resultado.Add(item);
        }

        return resultado;
    }

    public DashboardStatsDto ObtenerDashboardStats(List<Checada> checadas, List<Empleado> empleados)
    {
        var hoy = DateTime.Today;
        var checadasHoy = checadas.Where(c => c.FechaHora.Date == hoy).ToList();
        var resumenesHoy = GenerarResumenDiario(checadasHoy, empleados, hoy, hoy);

        return new DashboardStatsDto
        {
            TotalEmpleadosActivos = empleados.Count(e => e.Activo),
            TotalChecadasHoy = checadasHoy.Count,
            EntradasATiempoHoy = checadasHoy.Count(c => c.Tipo == TipoChecada.Entrada && c.Estatus == EstatusChecada.ATiempo),
            RetardosHoy = checadasHoy.Count(c => c.Tipo == TipoChecada.Entrada && c.Estatus == EstatusChecada.Retardo),
            FueraDeZonaHoy = checadasHoy.Count(c => !c.EnGeocerca),
            EnComidaAhora = resumenesHoy.Count(r => r.EstadoJornada == "En Comida"),
            JornadaCompletadaHoy = resumenesHoy.Count(r => r.EstadoJornada == "Jornada Completada"),
            UltimasChecadas = checadas.OrderByDescending(c => c.FechaHora).Take(15).ToList()
        };
    }

    public byte[] ExportarCsvResumen(List<ResumenDiario> resumenes)
    {
        var sb = new StringBuilder();
        // UTF-8 BOM para que Excel en español no altere tildes ni caracteres especiales
        sb.Append('\uFEFF');

        sb.AppendLine("ID Empleado,Nombre,Departamento,Fecha,Entrada,Estatus Entrada,Retardo (min),Salida Comer,Regreso Comer,Tiempo Comida (min),Salida Jornada,Horas Totales,Horas Efectivas,Cumplimiento GPS,Estado Jornada");

        foreach (var r in resumenes)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                EscapeCsv(r.EmpleadoId),
                EscapeCsv(r.EmpleadoNombre),
                EscapeCsv(r.Departamento),
                r.Fecha.ToString("yyyy-MM-dd"),
                r.HoraEntrada?.ToString("HH:mm:ss") ?? "--",
                EscapeCsv(r.EstatusEntrada.ToString()),
                r.MinutosRetardo.ToString(),
                r.HoraSalidaComer?.ToString("HH:mm:ss") ?? "--",
                r.HoraRegresoComer?.ToString("HH:mm:ss") ?? "--",
                r.MinutosComida.ToString("F1"),
                r.HoraSalida?.ToString("HH:mm:ss") ?? "--",
                r.HorasTotales.ToString("F2"),
                r.HorasEfectivas.ToString("F2"),
                r.CumplioGeocerca ? "EN ZONA" : "FUERA DE ZONA",
                EscapeCsv(r.EstadoJornada)
            }));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] ExportarCsvBitacora(List<Checada> checadas)
    {
        var sb = new StringBuilder();
        sb.Append('\uFEFF');

        sb.AppendLine("ID Marcaje,ID Empleado,Nombre,Departamento,Fecha,Hora,Tipo Marcaje,Latitud,Longitud,Ubicacion,Sede Asignada,Distancia (m),Dentro de Geocerca,Estatus,Minutos Retardo,Origen,Enlace Mapa");

        foreach (var c in checadas)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                EscapeCsv(c.Id),
                EscapeCsv(c.EmpleadoId),
                EscapeCsv(c.EmpleadoNombre),
                EscapeCsv(c.Departamento),
                c.FechaHora.ToString("yyyy-MM-dd"),
                c.FechaHora.ToString("HH:mm:ss"),
                EscapeCsv(c.TipoTexto),
                c.Latitud?.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                c.Longitud?.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                EscapeCsv(c.NombreUbicacion ?? c.DireccionUbicacion ?? ""),
                EscapeCsv(c.SucursalAsignadaNombre ?? "N/A"),
                c.DistanciaMetros?.ToString("F1") ?? "0",
                c.EnGeocerca ? "SI" : "NO",
                EscapeCsv(c.EstatusTexto),
                c.MinutosRetardo.ToString(),
                EscapeCsv(c.Origen),
                EscapeCsv(c.EnlaceGoogleMaps ?? "")
            }));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return "\"\"";
        return $"\"{valor.Replace("\"", "\"\"")}\"";
    }
}
