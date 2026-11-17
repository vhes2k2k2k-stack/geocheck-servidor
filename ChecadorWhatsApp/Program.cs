using System.Text.Json;
using ChecadorWhatsApp.Models;
using ChecadorWhatsApp.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Forzar binding a todas las interfaces en el puerto configurado o 5097 por defecto
var serverPort = Environment.GetEnvironmentVariable("PORT") ?? "5097";
builder.WebHost.UseUrls($"http://0.0.0.0:{serverPort}");

// Register Core Services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<IGeoService, GeoService>();
builder.Services.AddSingleton<IReportService, ReportService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();

// ==========================================
// 1. API PARA LA APLICACIÓN MÓVIL (APK ANDROID)
// ==========================================

// Login / Autenticación de colaborador en la APK
app.MapPost("/api/movil/login", async (
    [FromBody] LoginMovilRequest req,
    IDatabaseService db) =>
{
    if (string.IsNullOrWhiteSpace(req.Identificador))
    {
        return Results.BadRequest(new LoginMovilResponse { Autenticado = false, Mensaje = "Identificador requerido." });
    }

    var emp = await db.ValidarLoginEmpleadoAsync(req.Identificador, req.Pin);
    if (emp == null)
    {
        return Results.Ok(new LoginMovilResponse
        {
            Autenticado = false,
            Mensaje = "Colaborador o PIN incorrecto. Verifica con Recursos Humanos."
        });
    }

    return Results.Ok(new LoginMovilResponse
    {
        Autenticado = true,
        Mensaje = $"¡Bienvenido(a), {emp.Nombre}!",
        Empleado = emp
    });
});

// Verificación de NIP asignado del colaborador desde la APK
app.MapPost("/api/movil/verificar-nip", async ([FromBody] VerificarNipRequest req, IDatabaseService db) =>
{
    if (string.IsNullOrWhiteSpace(req.EmpleadoId))
        return Results.BadRequest(new { exito = false, mensaje = "ID de colaborador requerido." });

    var emp = await db.GetEmpleadoByIdAsync(req.EmpleadoId);
    if (emp == null || !emp.Activo)
        return Results.Ok(new { exito = false, mensaje = "Colaborador no encontrado o inactivo." });

    bool esValido = string.IsNullOrWhiteSpace(emp.Pin) || emp.Pin.Equals(req.Pin?.Trim(), StringComparison.OrdinalIgnoreCase);
    if (esValido)
    {
        return Results.Ok(new { exito = true, mensaje = $"NIP verificado. Bienvenido(a), {emp.Nombre}." });
    }
    return Results.Ok(new { exito = false, mensaje = "NIP incorrecto. Por favor verifica tu código de acceso asignado." });
});

// Registro de Marcaje con GPS desde la APK
app.MapPost("/api/movil/checar", async (
    [FromBody] ChecadaMovilRequest req,
    IDatabaseService db,
    IGeoService geo,
    IWebHostEnvironment env) =>
{
    if (string.IsNullOrWhiteSpace(req.EmpleadoId))
    {
        return Results.BadRequest(new ChecadaMovilResponse { Exito = false, Mensaje = "ID de colaborador requerido." });
    }

    var emp = await db.GetEmpleadoByIdAsync(req.EmpleadoId);
    if (emp == null || !emp.Activo)
    {
        return Results.Ok(new ChecadaMovilResponse { Exito = false, Mensaje = "Colaborador no encontrado o inactivo." });
    }

    // Validar NIP de seguridad asignado
    if (!string.IsNullOrWhiteSpace(emp.Pin))
    {
        if (string.IsNullOrWhiteSpace(req.Pin) || !emp.Pin.Equals(req.Pin.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Results.Ok(new ChecadaMovilResponse
            {
                Exito = false,
                Mensaje = "NIP de seguridad incorrecto. Registro de asistencia rechazado."
            });
        }
    }

    // Parsear tipo de checada
    if (!Enum.TryParse<TipoChecada>(req.Tipo, true, out var tipo))
    {
        tipo = TipoChecada.Entrada;
    }

    var config = await db.GetConfiguracionAsync();
    var sucursales = await db.GetSucursalesAsync();

    // 1. Evaluar Geocerca GPS respecto a la sede en donde debe laborar
    var evaluacion = geo.EvaluarGeocerca(req.Latitud, req.Longitud, sucursales, emp.SucursalId);
    var sucursalAsignada = evaluacion.sucursalAsignada;
    var distancia = evaluacion.distanciaMetros;
    var dentroDeZona = evaluacion.dentroDeZona;

    if (emp.PermitirRemoto)
    {
        dentroDeZona = true;
    }

    // 2. Evaluar Puntualidad (para ENTRADA)
    var ahora = DateTime.Now;
    var estatus = EstatusChecada.Normal;
    int minutosRetardo = 0;

    if (tipo == TipoChecada.Entrada)
    {
        var horaEsperada = emp.GetHoraEntradaSpan();
        var horaLlegada = ahora.TimeOfDay;
        var tolerancia = TimeSpan.FromMinutes(emp.ToleranciaMinutos > 0 ? emp.ToleranciaMinutos : config.ToleranciaMinutosDefault);
        var horaLimite = horaEsperada.Add(tolerancia);

        if (horaLlegada > horaLimite)
        {
            estatus = EstatusChecada.Retardo;
            minutosRetardo = (int)Math.Round((horaLlegada - horaEsperada).TotalMinutes);
        }
        else
        {
            estatus = EstatusChecada.ATiempo;
        }
    }

    if (!dentroDeZona)
    {
        estatus = EstatusChecada.FueraDeZona;
    }

    // 3. Crear y Guardar la Checada vinculada a su sede de trabajo
    var nuevaChecada = new Checada
    {
        EmpleadoId = emp.Id,
        EmpleadoNombre = emp.Nombre,
        Departamento = emp.Departamento,
        FechaHora = ahora,
        Tipo = tipo,
        Latitud = req.Latitud,
        Longitud = req.Longitud,
        NombreUbicacion = dentroDeZona ? sucursalAsignada?.Nombre : "Ubicación Móvil GPS",
        DireccionUbicacion = sucursalAsignada?.Direccion,
        SucursalAsignadaId = sucursalAsignada?.Id,
        SucursalAsignadaNombre = sucursalAsignada?.Nombre ?? "Sede no asignada",
        DistanciaMetros = distancia,
        EnGeocerca = dentroDeZona,
        Estatus = estatus,
        MinutosRetardo = minutosRetardo,
        Origen = "App Móvil (Android APK)",
        Notas = string.IsNullOrWhiteSpace(req.Dispositivo) ? null : $"Dispositivo: {req.Dispositivo}"
    };

    // 3. Procesar foto selfie si fue enviada (Verificación Facial de Entrada)
    if (!string.IsNullOrWhiteSpace(req.FotoBase64))
    {
        try
        {
            var rawBase64 = req.FotoBase64;
            var commaIdx = rawBase64.IndexOf(',');
            if (commaIdx >= 0)
            {
                rawBase64 = rawBase64.Substring(commaIdx + 1);
            }
            var imageBytes = Convert.FromBase64String(rawBase64);
            var selfiesDir = Path.Combine(env.WebRootPath, "uploads", "selfies");
            Directory.CreateDirectory(selfiesDir);

            var fileName = $"{Guid.NewGuid():N}.jpg";
            var filePath = Path.Combine(selfiesDir, fileName);
            await File.WriteAllBytesAsync(filePath, imageBytes);

            nuevaChecada.FotoSelfieUrl = $"/uploads/selfies/{fileName}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al procesar selfie: {ex.Message}");
        }
    }

    await db.AddChecadaAsync(nuevaChecada);

    var distStr = geo.FormatearDistancia(distancia);
    string mensajeConfirmacion;
    if (dentroDeZona)
    {
        mensajeConfirmacion = $"¡{nuevaChecada.TipoTexto} registrada con éxito! (a {distStr} en zona permitida).";
    }
    else
    {
        mensajeConfirmacion = $"¡{nuevaChecada.TipoTexto} registrada! AVISO: Estás fuera de rango a {distStr} de distancia.";
    }

    return Results.Ok(new ChecadaMovilResponse
    {
        Exito = true,
        Mensaje = mensajeConfirmacion,
        Checada = nuevaChecada,
        EnGeocerca = dentroDeZona,
        DistanciaMetros = distancia,
        SucursalAsignada = sucursalAsignada?.Nombre,
        FotoSelfieUrl = nuevaChecada.FotoSelfieUrl,
        EstatusTexto = nuevaChecada.EstatusTexto,
        MinutosRetardo = minutosRetardo
    });
});

// Consultar Estado del Día del Empleado desde la APK
app.MapGet("/api/movil/estado/{empleadoId}", async (string empleadoId, IDatabaseService db) =>
{
    var emp = await db.GetEmpleadoByIdAsync(empleadoId);
    if (emp == null) return Results.NotFound();

    var hoy = DateTime.Today;
    var checadasHoy = (await db.GetChecadasAsync(new ReportFilterDto
    {
        EmpleadoId = emp.Id,
        FechaInicio = hoy,
        FechaFin = hoy
    })).OrderBy(c => c.FechaHora).ToList();

    var entrada = checadasHoy.FirstOrDefault(c => c.Tipo == TipoChecada.Entrada);
    var comer = checadasHoy.FirstOrDefault(c => c.Tipo == TipoChecada.SalidaComer);
    var regreso = checadasHoy.FirstOrDefault(c => c.Tipo == TipoChecada.RegresoComer);
    var salida = checadasHoy.LastOrDefault(c => c.Tipo == TipoChecada.Salida);

    string tiempoTrabajado = "--";
    if (entrada != null)
    {
        var fin = salida?.FechaHora ?? DateTime.Now;
        var diff = fin - entrada.FechaHora;
        tiempoTrabajado = $"{(int)diff.TotalHours}h {diff.Minutes}m";
    }

    string estadoJornada = (entrada != null, salida != null) switch
    {
        (true, true) => "Jornada Completada",
        (true, false) when comer != null && regreso == null => "En Tiempo de Comida",
        (true, false) => "Activo / En Turno",
        _ => "Sin Entrada Registrada"
    };

    return Results.Ok(new EstadoMovilResponse
    {
        EmpleadoId = emp.Id,
        EmpleadoNombre = emp.Nombre,
        Departamento = emp.Departamento,
        Puesto = emp.Puesto,
        Horario = $"{emp.HoraEntrada} - {emp.HoraSalida}",
        EstadoJornada = estadoJornada,
        HoraEntrada = entrada?.FechaHora.ToString("hh:mm tt"),
        HoraSalidaComer = comer?.FechaHora.ToString("hh:mm tt"),
        HoraRegresoComer = regreso?.FechaHora.ToString("hh:mm tt"),
        HoraSalida = salida?.FechaHora.ToString("hh:mm tt"),
        TiempoTrabajado = tiempoTrabajado,
        ChecadasHoy = checadasHoy
    });
});

// Lista de Empleados para el Selector de la APK
app.MapGet("/api/movil/empleados", async (IDatabaseService db) =>
{
    var empleados = await db.GetEmpleadosAsync();
    return Results.Ok(empleados.Where(e => e.Activo).Select(e => new
    {
        id = e.Id,
        codigo = e.Codigo,
        nombre = e.Nombre,
        departamento = e.Departamento,
        puesto = e.Puesto,
        horaEntrada = e.HoraEntrada,
        horaSalida = e.HoraSalida
    }));
});

// Lista de Sucursales y Geocercas para la APK
app.MapGet("/api/movil/sucursales", async (IDatabaseService db) =>
{
    var sucursales = await db.GetSucursalesAsync();
    return Results.Ok(sucursales.Where(s => s.Activa));
});

// Obtener Información de IP de Red del Servidor para los Celulares
app.MapGet("/api/servidor/info", async (IDatabaseService db) =>
{
    var config = await db.GetConfiguracionAsync();
    var hostName = System.Net.Dns.GetHostName();
    var ipAddresses = System.Net.Dns.GetHostAddresses(hostName)
        .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip))
        .Select(ip => ip.ToString())
        .ToList();

    var primaryIp = ipAddresses.FirstOrDefault() ?? "192.168.1.14";
    var urlWifi = $"http://{primaryIp}:5097";
    var urlDatosMoviles = config.ServidorDatosMovilesUrl;

    return Results.Ok(new
    {
        ip = primaryIp,
        puerto = 5097,
        urlSugerida = urlWifi,
        urlWifi = urlWifi,
        urlDatosMoviles = urlDatosMoviles,
        todasLasIps = ipAddresses
    });
});

// ==========================================
// 2. DESCARGA DEL APK PARA DISPOSITIVOS MÓVILES
// ==========================================

app.MapGet("/api/descargas/apk", (IWebHostEnvironment env) =>
{
    var rutaApk = Path.Combine(env.WebRootPath, "downloads", "ChecadorApp.apk");
    if (!File.Exists(rutaApk))
    {
        // Intentar en la raíz de la app
        rutaApk = Path.Combine(env.ContentRootPath, "..", "ChecadorApp.apk");
    }

    if (File.Exists(rutaApk))
    {
        return Results.File(rutaApk, "application/vnd.android.package-archive", "ChecadorApp.apk");
    }

    return Results.NotFound(new { error = "El archivo APK aún no ha sido compilado." });
});

// ==========================================
// 3. DASHBOARD Y REPORTES EN TIEMPO REAL
// ==========================================

app.MapGet("/api/dashboard/stats", async (
    IDatabaseService db,
    IReportService reportService) =>
{
    var checadas = await db.GetChecadasAsync();
    var empleados = await db.GetEmpleadosAsync();
    var stats = reportService.ObtenerDashboardStats(checadas, empleados);
    return Results.Ok(stats);
});

app.MapGet("/api/checadas", async (
    [FromQuery] DateTime? fechaInicio,
    [FromQuery] DateTime? fechaFin,
    [FromQuery] string? empleadoId,
    [FromQuery] string? departamento,
    [FromQuery] string? sucursalId,
    [FromQuery] string? tipo,
    [FromQuery] string? estatus,
    IDatabaseService db) =>
{
    var filter = new ReportFilterDto
    {
        FechaInicio = fechaInicio,
        FechaFin = fechaFin,
        EmpleadoId = empleadoId,
        Departamento = departamento,
        SucursalId = sucursalId,
        Tipo = tipo,
        Estatus = estatus
    };

    var lista = await db.GetChecadasAsync(filter);
    return Results.Ok(lista);
});

app.MapPost("/api/checadas/manual", async (
    [FromBody] Checada checada,
    IDatabaseService db,
    IGeoService geo) =>
{
    if (string.IsNullOrWhiteSpace(checada.EmpleadoId))
        return Results.BadRequest(new { error = "EmpleadoId requerido" });

    var emp = await db.GetEmpleadoByIdAsync(checada.EmpleadoId);
    if (emp != null)
    {
        checada.EmpleadoNombre = emp.Nombre;
        checada.Departamento = emp.Departamento;
    }

    var sucursales = await db.GetSucursalesAsync();
    if (checada.Latitud.HasValue && checada.Longitud.HasValue)
    {
        var ev = geo.EvaluarGeocerca(checada.Latitud.Value, checada.Longitud.Value, sucursales, emp?.SucursalId);
        checada.SucursalAsignadaId = ev.sucursalAsignada?.Id;
        checada.SucursalAsignadaNombre = ev.sucursalAsignada?.Nombre;
        checada.DistanciaMetros = ev.distanciaMetros;
        checada.EnGeocerca = ev.dentroDeZona || (emp?.PermitirRemoto ?? false);
    }

    checada.Origen = "Portal Web";
    var result = await db.AddChecadaAsync(checada);
    return Results.Ok(result);
});

app.MapDelete("/api/checadas/{id}", async (string id, IDatabaseService db) =>
{
    await db.DeleteChecadaAsync(id);
    return Results.Ok(new { success = true, mensaje = "Registro eliminado." });
});

app.MapDelete("/api/checadas", async (IDatabaseService db) =>
{
    await db.DeleteAllChecadasAsync();
    return Results.Ok(new { success = true, mensaje = "Todos los registros de asistencia fueron eliminados." });
});

// ==========================================
// 4. REPORTES E INFORMES EJECUTIVOS
// ==========================================

app.MapGet("/api/reportes/resumen", async (
    [FromQuery] DateTime? fechaInicio,
    [FromQuery] DateTime? fechaFin,
    [FromQuery] string? empleadoId,
    [FromQuery] string? departamento,
    IDatabaseService db,
    IReportService reportService) =>
{
    var filter = new ReportFilterDto
    {
        FechaInicio = fechaInicio,
        FechaFin = fechaFin,
        EmpleadoId = empleadoId,
        Departamento = departamento
    };

    var checadas = await db.GetChecadasAsync(filter);
    var empleados = await db.GetEmpleadosAsync();
    var resumen = reportService.GenerarResumenDiario(checadas, empleados, fechaInicio, fechaFin);
    return Results.Ok(resumen);
});

app.MapGet("/api/reportes/export/csv-resumen", async (
    [FromQuery] DateTime? fechaInicio,
    [FromQuery] DateTime? fechaFin,
    IDatabaseService db,
    IReportService reportService) =>
{
    var checadas = await db.GetChecadasAsync(new ReportFilterDto { FechaInicio = fechaInicio, FechaFin = fechaFin });
    var empleados = await db.GetEmpleadosAsync();
    var resumen = reportService.GenerarResumenDiario(checadas, empleados, fechaInicio, fechaFin);
    var bytes = reportService.ExportarCsvResumen(resumen);

    var fileName = $"Reporte_Resumen_Checador_{DateTime.Now:yyyyMMdd_HHmm}.csv";
    return Results.File(bytes, "text/csv; charset=utf-8", fileName);
});

app.MapGet("/api/reportes/export/csv-bitacora", async (
    [FromQuery] DateTime? fechaInicio,
    [FromQuery] DateTime? fechaFin,
    IDatabaseService db,
    IReportService reportService) =>
{
    var checadas = await db.GetChecadasAsync(new ReportFilterDto { FechaInicio = fechaInicio, FechaFin = fechaFin });
    var bytes = reportService.ExportarCsvBitacora(checadas);

    var fileName = $"Bitacora_Marcajes_GPS_{DateTime.Now:yyyyMMdd_HHmm}.csv";
    return Results.File(bytes, "text/csv; charset=utf-8", fileName);
});

// ==========================================
// 5. CATÁLOGOS (EMPLEADOS & SUCURSALES)
// ==========================================

app.MapGet("/api/empleados", async (IDatabaseService db) =>
{
    return Results.Ok(await db.GetEmpleadosAsync());
});

app.MapPost("/api/empleados", async ([FromBody] Empleado emp, IDatabaseService db) =>
{
    if (string.IsNullOrWhiteSpace(emp.Nombre))
    {
        return Results.BadRequest(new { error = "El nombre del empleado es obligatorio." });
    }
    await db.SaveEmpleadoAsync(emp);
    return Results.Ok(emp);
});

app.MapDelete("/api/empleados/{id}", async (string id, IDatabaseService db) =>
{
    await db.DeleteEmpleadoAsync(id);
    return Results.Ok(new { success = true });
});

app.MapGet("/api/sucursales", async (IDatabaseService db) =>
{
    return Results.Ok(await db.GetSucursalesAsync());
});

app.MapPost("/api/sucursales", async ([FromBody] Sucursal suc, IDatabaseService db, IGeoService geo) =>
{
    if (string.IsNullOrWhiteSpace(suc.Nombre))
    {
        return Results.BadRequest(new { error = "El nombre de la sucursal es obligatorio." });
    }
    suc.Latitud = geo.NormalizarCoordenada(suc.Latitud, true);
    suc.Longitud = geo.NormalizarCoordenada(suc.Longitud, false);
    if (suc.RadioMetros <= 0) suc.RadioMetros = 200;

    await db.SaveSucursalAsync(suc);
    return Results.Ok(suc);
});

app.MapDelete("/api/sucursales/{id}", async (string id, IDatabaseService db) =>
{
    await db.DeleteSucursalAsync(id);
    return Results.Ok(new { success = true });
});

// ==========================================
// 6. CONFIGURACIÓN DEL SISTEMA
// ==========================================

app.MapGet("/api/configuracion", async (IDatabaseService db) =>
{
    return Results.Ok(await db.GetConfiguracionAsync());
});

app.MapPost("/api/configuracion", async ([FromBody] Configuracion config, IDatabaseService db) =>
{
    await db.SaveConfiguracionAsync(config);
    return Results.Ok(config);
});

// Registrar o actualizar URL de datos móviles (túnel público)
app.MapPost("/api/configuracion/url-publica", async ([FromBody] System.Text.Json.JsonElement body, IDatabaseService db) =>
{
    string? url = null;
    if (body.TryGetProperty("url", out var elem))
    {
        url = elem.GetString();
    }
    if (!string.IsNullOrWhiteSpace(url))
    {
        var config = await db.GetConfiguracionAsync();
        config.ServidorDatosMovilesUrl = url.Trim();
        await db.SaveConfiguracionAsync(config);
        return Results.Ok(new { success = true, url = config.ServidorDatosMovilesUrl });
    }
    return Results.BadRequest(new { error = "URL inválida" });
});

// ==========================================
// 7. MANTENIMIENTO Y RESTABLECIMIENTO DEL SISTEMA
// ==========================================

// Limpiar toda la base de datos (deja la aplicación completamente vacía y limpia)
app.MapPost("/api/sistema/limpiar-todo", async (IDatabaseService db) =>
{
    await db.ResetDatabaseAsync();
    return Results.Ok(new { success = true, mensaje = "Base de datos restablecida. La aplicación ha quedado completamente limpia." });
});

// Restaurar datos de demostración
app.MapPost("/api/sistema/restaurar-demo", async (IDatabaseService db) =>
{
    await db.RestoreDemoDataAsync();
    return Results.Ok(new { success = true, mensaje = "Datos de ejemplo restaurados correctamente." });
});

app.Run();
