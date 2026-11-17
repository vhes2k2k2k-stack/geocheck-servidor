namespace ChecadorWhatsApp.Models;

public class Configuracion
{
    public string NombreEmpresa { get; set; } = "Logística & Servicios S.A. de C.V.";
    public string PinAdmin { get; set; } = "1234";
    public int ToleranciaMinutosDefault { get; set; } = 15;
    public double RadioGeocercaDefaultMetros { get; set; } = 250;
    public bool ExigirUbicacionGPS { get; set; } = true;
    public bool PermitirMarcajeWeb { get; set; } = true;
    public string ServidorUrl { get; set; } = "http://localhost:5097";
    public string ServidorDatosMovilesUrl { get; set; } = string.Empty;
}
