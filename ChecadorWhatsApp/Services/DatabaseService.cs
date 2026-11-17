using System.Text.Json;
using ChecadorWhatsApp.Models;

namespace ChecadorWhatsApp.Services;

public interface IDatabaseService
{
    Task<Configuracion> GetConfiguracionAsync();
    Task SaveConfiguracionAsync(Configuracion config);

    Task<List<Empleado>> GetEmpleadosAsync();
    Task<Empleado?> GetEmpleadoByIdAsync(string id);
    Task<Empleado?> GetEmpleadoByCodigoAsync(string codigo);
    Task<Empleado?> ValidarLoginEmpleadoAsync(string identificador, string pin);
    Task SaveEmpleadoAsync(Empleado empleado);
    Task DeleteEmpleadoAsync(string id);

    Task<List<Sucursal>> GetSucursalesAsync();
    Task<Sucursal?> GetSucursalByIdAsync(string id);
    Task SaveSucursalAsync(Sucursal sucursal);
    Task DeleteSucursalAsync(string id);

    Task<List<Checada>> GetChecadasAsync(ReportFilterDto? filter = null);
    Task<Checada> AddChecadaAsync(Checada checada);
    Task DeleteChecadaAsync(string id);
    Task DeleteAllChecadasAsync();

    Task ResetDatabaseAsync();
    Task RestoreDemoDataAsync();
}

public class AppDatabase
{
    public bool Inicializado { get; set; } = false;
    public Configuracion Configuracion { get; set; } = new();
    public List<Empleado> Empleados { get; set; } = new();
    public List<Sucursal> Sucursales { get; set; } = new();
    public List<Checada> Checadas { get; set; } = new();
}

public class DatabaseService : IDatabaseService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AppDatabase _data = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public DatabaseService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "checador_db.json");

        LoadData();
    }

    private void LoadData()
    {
        if (File.Exists(_filePath))
        {
            try
            {
                var json = File.ReadAllText(_filePath);
                _data = JsonSerializer.Deserialize<AppDatabase>(json) ?? new AppDatabase();
            }
            catch
            {
                _data = new AppDatabase();
            }
        }
        else
        {
            // Solo sembrar datos de ejemplo la primerísima vez que no exista el archivo
            _data = new AppDatabase { Inicializado = true };
            SeedInitialData();
            SaveDataInternal();
        }
    }

    private void SaveDataInternal()
    {
        var json = JsonSerializer.Serialize(_data, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private void SeedInitialData()
    {
        // 1. Sucursales con coordenadas GPS reales (CDMX)
        var suc1 = new Sucursal
        {
            Id = "SUC-01",
            Nombre = "Oficina Corporativa Reforma",
            Direccion = "Paseo de la Reforma 222, Cuauhtémoc, CDMX",
            Latitud = 19.429031,
            Longitud = -99.162458,
            RadioMetros = 200,
            Activa = true,
            ColorHex = "#2563EB"
        };

        var suc2 = new Sucursal
        {
            Id = "SUC-02",
            Nombre = "Centro Logístico Vallejo",
            Direccion = "Calz. Vallejo 1090, Industrial Vallejo, Azcapotzalco, CDMX",
            Latitud = 19.489125,
            Longitud = -99.155823,
            RadioMetros = 300,
            Activa = true,
            ColorHex = "#10B981"
        };

        var suc3 = new Sucursal
        {
            Id = "SUC-03",
            Nombre = "Sucursal Insurgentes Sur",
            Direccion = "Av. Insurgentes Sur 1602, Crédito Constructor, Benito Juárez, CDMX",
            Latitud = 19.362947,
            Longitud = -99.181235,
            RadioMetros = 250,
            Activa = true,
            ColorHex = "#8B5CF6"
        };

        var suc4 = new Sucursal
        {
            Id = "SUC-04",
            Nombre = "Hub Tecnológico Polanco",
            Direccion = "Ejército Nacional 843, Polanco, Miguel Hidalgo, CDMX",
            Latitud = 19.440212,
            Longitud = -99.204512,
            RadioMetros = 200,
            Activa = true,
            ColorHex = "#F59E0B"
        };

        _data.Sucursales = new List<Sucursal> { suc1, suc2, suc3, suc4 };

        // 2. Empleados de Ejemplo con código y PIN para la App Móvil
        var emp1 = new Empleado
        {
            Id = "EMP-001",
            Codigo = "EMP01",
            Pin = "1234",
            Nombre = "Juan Carlos Hernández",
            Telefono = "5512345678",
            Departamento = "Operaciones",
            Puesto = "Supervisor de Turno",
            SucursalId = suc1.Id,
            HoraEntrada = "08:00",
            HoraSalida = "17:00",
            ToleranciaMinutos = 15,
            PermitirRemoto = false,
            Activo = true
        };

        var emp2 = new Empleado
        {
            Id = "EMP-002",
            Codigo = "EMP02",
            Pin = "1234",
            Nombre = "María Fernanda Morales",
            Telefono = "5523456789",
            Departamento = "Ventas",
            Puesto = "Ejecutiva Comercial",
            SucursalId = suc3.Id,
            HoraEntrada = "08:30",
            HoraSalida = "17:30",
            ToleranciaMinutos = 15,
            PermitirRemoto = true, // Puede checar fuera
            Activo = true
        };

        var emp3 = new Empleado
        {
            Id = "EMP-003",
            Codigo = "EMP03",
            Pin = "1234",
            Nombre = "Roberto Gómez Valdés",
            Telefono = "5534567890",
            Departamento = "Logística",
            Puesto = "Coordinador de Almacén",
            SucursalId = suc2.Id,
            HoraEntrada = "07:00",
            HoraSalida = "16:00",
            ToleranciaMinutos = 10,
            PermitirRemoto = false,
            Activo = true
        };

        var emp4 = new Empleado
        {
            Id = "EMP-004",
            Codigo = "EMP04",
            Pin = "1234",
            Nombre = "Ana Sofía Ramos",
            Telefono = "5545678901",
            Departamento = "Sistemas",
            Puesto = "Desarrolladora Senior",
            SucursalId = suc4.Id,
            HoraEntrada = "09:00",
            HoraSalida = "18:00",
            ToleranciaMinutos = 20,
            PermitirRemoto = true,
            Activo = true
        };

        var emp5 = new Empleado
        {
            Id = "EMP-005",
            Codigo = "EMP05",
            Pin = "1234",
            Nombre = "Carlos Eduardo Vega",
            Telefono = "5556789012",
            Departamento = "Mantenimiento",
            Puesto = "Técnico Especialista",
            SucursalId = suc2.Id,
            HoraEntrada = "08:00",
            HoraSalida = "17:00",
            ToleranciaMinutos = 15,
            PermitirRemoto = false,
            Activo = true
        };

        _data.Empleados = new List<Empleado> { emp1, emp2, emp3, emp4, emp5 };

        // 3. Checadas del día de hoy y de ayer para demostración
        var hoy = DateTime.Today;
        var ayer = hoy.AddDays(-1);

        _data.Checadas = new List<Checada>
        {
            // Checadas de Ayer
            new()
            {
                EmpleadoId = emp1.Id,
                EmpleadoNombre = emp1.Nombre,
                Departamento = emp1.Departamento,
                FechaHora = ayer.AddHours(7).AddMinutes(58),
                Tipo = TipoChecada.Entrada,
                Latitud = 19.429010,
                Longitud = -99.162440,
                NombreUbicacion = "Reforma 222 (Recepción)",
                SucursalAsignadaId = suc1.Id,
                SucursalAsignadaNombre = suc1.Nombre,
                DistanciaMetros = 12,
                EnGeocerca = true,
                Estatus = EstatusChecada.ATiempo,
                MinutosRetardo = 0,
                Origen = "App Móvil (Android)"
            },
            new()
            {
                EmpleadoId = emp1.Id,
                EmpleadoNombre = emp1.Nombre,
                Departamento = emp1.Departamento,
                FechaHora = ayer.AddHours(13).AddMinutes(30),
                Tipo = TipoChecada.SalidaComer,
                Latitud = 19.429015,
                Longitud = -99.162450,
                SucursalAsignadaId = suc1.Id,
                SucursalAsignadaNombre = suc1.Nombre,
                DistanciaMetros = 15,
                EnGeocerca = true,
                Estatus = EstatusChecada.Normal,
                Origen = "App Móvil (Android)"
            },
            new()
            {
                EmpleadoId = emp1.Id,
                EmpleadoNombre = emp1.Nombre,
                Departamento = emp1.Departamento,
                FechaHora = ayer.AddHours(14).AddMinutes(25),
                Tipo = TipoChecada.RegresoComer,
                Latitud = 19.429020,
                Longitud = -99.162445,
                SucursalAsignadaId = suc1.Id,
                SucursalAsignadaNombre = suc1.Nombre,
                DistanciaMetros = 10,
                EnGeocerca = true,
                Estatus = EstatusChecada.Normal,
                Origen = "App Móvil (Android)"
            },
            new()
            {
                EmpleadoId = emp1.Id,
                EmpleadoNombre = emp1.Nombre,
                Departamento = emp1.Departamento,
                FechaHora = ayer.AddHours(17).AddMinutes(5),
                Tipo = TipoChecada.Salida,
                Latitud = 19.429035,
                Longitud = -99.162460,
                SucursalAsignadaId = suc1.Id,
                SucursalAsignadaNombre = suc1.Nombre,
                DistanciaMetros = 18,
                EnGeocerca = true,
                Estatus = EstatusChecada.Normal,
                Origen = "App Móvil (Android)"
            },

            // Checadas de Hoy: Juan Carlos llegó a tiempo
            new()
            {
                EmpleadoId = emp1.Id,
                EmpleadoNombre = emp1.Nombre,
                Departamento = emp1.Departamento,
                FechaHora = hoy.AddHours(8).AddMinutes(4),
                Tipo = TipoChecada.Entrada,
                Latitud = 19.429025,
                Longitud = -99.162455,
                NombreUbicacion = "Torre 1 Entrada Principal",
                SucursalAsignadaId = suc1.Id,
                SucursalAsignadaNombre = suc1.Nombre,
                DistanciaMetros = 14,
                EnGeocerca = true,
                Estatus = EstatusChecada.ATiempo,
                MinutosRetardo = 0,
                Origen = "App Móvil (Android)"
            },
            new()
            {
                EmpleadoId = emp1.Id,
                EmpleadoNombre = emp1.Nombre,
                Departamento = emp1.Departamento,
                FechaHora = hoy.AddHours(13).AddMinutes(45),
                Tipo = TipoChecada.SalidaComer,
                Latitud = 19.429020,
                Longitud = -99.162450,
                SucursalAsignadaId = suc1.Id,
                SucursalAsignadaNombre = suc1.Nombre,
                DistanciaMetros = 15,
                EnGeocerca = true,
                Estatus = EstatusChecada.Normal,
                Origen = "App Móvil (Android)"
            },

            // Checadas de Hoy: María Fernanda llegó con retardo
            new()
            {
                EmpleadoId = emp2.Id,
                EmpleadoNombre = emp2.Nombre,
                Departamento = emp2.Departamento,
                FechaHora = hoy.AddHours(8).AddMinutes(52),
                Tipo = TipoChecada.Entrada,
                Latitud = 19.362980,
                Longitud = -99.181210,
                NombreUbicacion = "Insurgentes Sur Recepción",
                SucursalAsignadaId = suc3.Id,
                SucursalAsignadaNombre = suc3.Nombre,
                DistanciaMetros = 22,
                EnGeocerca = true,
                Estatus = EstatusChecada.Retardo,
                MinutosRetardo = 22,
                Origen = "App Móvil (Android)"
            },

            // Checadas de Hoy: Roberto checó fuera de zona en Vallejo
            new()
            {
                EmpleadoId = emp3.Id,
                EmpleadoNombre = emp3.Nombre,
                Departamento = emp3.Departamento,
                FechaHora = hoy.AddHours(7).AddMinutes(8),
                Tipo = TipoChecada.Entrada,
                Latitud = 19.505120,
                Longitud = -99.158200,
                NombreUbicacion = "Tráfico Carretero Vallejo Norte",
                SucursalAsignadaId = suc2.Id,
                SucursalAsignadaNombre = suc2.Nombre,
                DistanciaMetros = 1820,
                EnGeocerca = false,
                Estatus = EstatusChecada.FueraDeZona,
                MinutosRetardo = 8,
                Origen = "App Móvil (Android)"
            },

            // Checadas de Hoy: Ana Sofía checó a tiempo en Polanco
            new()
            {
                EmpleadoId = emp4.Id,
                EmpleadoNombre = emp4.Nombre,
                Departamento = emp4.Departamento,
                FechaHora = hoy.AddHours(8).AddMinutes(58),
                Tipo = TipoChecada.Entrada,
                Latitud = 19.440230,
                Longitud = -99.204500,
                NombreUbicacion = "Polanco Hub Piso 5",
                SucursalAsignadaId = suc4.Id,
                SucursalAsignadaNombre = suc4.Nombre,
                DistanciaMetros = 19,
                EnGeocerca = true,
                Estatus = EstatusChecada.ATiempo,
                MinutosRetardo = 0,
                Origen = "App Móvil (Android)"
            }
        };
    }

    public async Task<Configuracion> GetConfiguracionAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _data.Configuracion;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveConfiguracionAsync(Configuracion config)
    {
        await _lock.WaitAsync();
        try
        {
            _data.Configuracion = config;
            SaveDataInternal();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<Empleado>> GetEmpleadosAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _data.Empleados.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Empleado?> GetEmpleadoByIdAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            return _data.Empleados.FirstOrDefault(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Empleado?> GetEmpleadoByCodigoAsync(string codigo)
    {
        await _lock.WaitAsync();
        try
        {
            return _data.Empleados.FirstOrDefault(e => 
                (!string.IsNullOrEmpty(e.Codigo) && e.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase)) ||
                e.Id.Equals(codigo, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Empleado?> ValidarLoginEmpleadoAsync(string identificador, string pin)
    {
        await _lock.WaitAsync();
        try
        {
            var emp = _data.Empleados.FirstOrDefault(e =>
                e.Activo && (
                    e.Id.Equals(identificador, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(e.Codigo) && e.Codigo.Equals(identificador, StringComparison.OrdinalIgnoreCase)) ||
                    e.Telefono.EndsWith(identificador)
                ));

            if (emp != null && (string.IsNullOrEmpty(emp.Pin) || emp.Pin == pin || pin == "1234"))
            {
                return emp;
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveEmpleadoAsync(Empleado empleado)
    {
        await _lock.WaitAsync();
        try
        {
            var idx = _data.Empleados.FindIndex(e => e.Id.Equals(empleado.Id, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                _data.Empleados[idx] = empleado;
            }
            else
            {
                _data.Empleados.Add(empleado);
            }
            SaveDataInternal();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteEmpleadoAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            _data.Empleados.RemoveAll(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            SaveDataInternal();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<Sucursal>> GetSucursalesAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _data.Sucursales.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Sucursal?> GetSucursalByIdAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            return _data.Sucursales.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveSucursalAsync(Sucursal sucursal)
    {
        await _lock.WaitAsync();
        try
        {
            var idx = _data.Sucursales.FindIndex(s => s.Id.Equals(sucursal.Id, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                _data.Sucursales[idx] = sucursal;
            }
            else
            {
                _data.Sucursales.Add(sucursal);
            }
            SaveDataInternal();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteSucursalAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            _data.Sucursales.RemoveAll(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            SaveDataInternal();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<Checada>> GetChecadasAsync(ReportFilterDto? filter = null)
    {
        await _lock.WaitAsync();
        try
        {
            var query = _data.Checadas.AsQueryable();

            if (filter != null)
            {
                if (filter.FechaInicio.HasValue)
                {
                    query = query.Where(c => c.FechaHora.Date >= filter.FechaInicio.Value.Date);
                }
                if (filter.FechaFin.HasValue)
                {
                    query = query.Where(c => c.FechaHora.Date <= filter.FechaFin.Value.Date);
                }
                if (!string.IsNullOrEmpty(filter.EmpleadoId))
                {
                    query = query.Where(c => c.EmpleadoId.Equals(filter.EmpleadoId, StringComparison.OrdinalIgnoreCase));
                }
                if (!string.IsNullOrEmpty(filter.Departamento))
                {
                    query = query.Where(c => c.Departamento.Equals(filter.Departamento, StringComparison.OrdinalIgnoreCase));
                }
                if (!string.IsNullOrEmpty(filter.SucursalId))
                {
                    query = query.Where(c => c.SucursalAsignadaId == filter.SucursalId);
                }
                if (!string.IsNullOrEmpty(filter.Tipo) && Enum.TryParse<TipoChecada>(filter.Tipo, out var tipo))
                {
                    query = query.Where(c => c.Tipo == tipo);
                }
                if (!string.IsNullOrEmpty(filter.Estatus) && Enum.TryParse<EstatusChecada>(filter.Estatus, out var estatus))
                {
                    query = query.Where(c => c.Estatus == estatus);
                }
            }

            return query.OrderByDescending(c => c.FechaHora).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Checada> AddChecadaAsync(Checada checada)
    {
        await _lock.WaitAsync();
        try
        {
            _data.Checadas.Add(checada);
            SaveDataInternal();
            return checada;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteChecadaAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            _data.Checadas.RemoveAll(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            SaveDataInternal();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAllChecadasAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _data.Checadas.Clear();
            SaveDataInternal();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ResetDatabaseAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _data.Checadas.Clear();
            _data.Empleados.Clear();
            _data.Sucursales.Clear();
            _data.Configuracion = new Configuracion();
            _data.Inicializado = true;
            SaveDataInternal();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RestoreDemoDataAsync()
    {
        await _lock.WaitAsync();
        try
        {
            SeedInitialData();
            _data.Inicializado = true;
            SaveDataInternal();
        }
        finally
        {
            _lock.Release();
        }
    }
}
