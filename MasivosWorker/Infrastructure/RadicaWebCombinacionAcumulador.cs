using Models.Dto;

namespace Infrastructure;

/// <summary>
/// Acumula combinaciones únicas (FechaFactura, bodega) por lote en memoria.
/// </summary>
public sealed class RadicaWebCombinacionAcumulador
{
    private static readonly IEqualityComparer<(DateOnly Fecha, string Bodega)> Comparer =
        EqualityComparer<(DateOnly Fecha, string Bodega)>.Create(
            (a, b) =>
                a.Fecha == b.Fecha &&
                string.Equals(a.Bodega, b.Bodega, StringComparison.OrdinalIgnoreCase),
            x => HashCode.Combine(x.Fecha, StringComparer.OrdinalIgnoreCase.GetHashCode(x.Bodega)));

    private readonly HashSet<(DateOnly Fecha, string Bodega)> _combinaciones = new(Comparer);

    public void AgregarSiExitoso(DocumentoProcesamientoResult resultado)
    {
        if (!resultado.EsExitoso)
            return;

        if (!resultado.FechaFactura.HasValue || string.IsNullOrWhiteSpace(resultado.IdBodega))
            return;

        _combinaciones.Add((
            DateOnly.FromDateTime(resultado.FechaFactura.Value.Date),
            resultado.IdBodega.Trim()));
    }

    public IReadOnlyList<(DateOnly Fecha, string Bodega)> ObtenerCombinaciones() =>
        _combinaciones.ToList();

    public int Cantidad => _combinaciones.Count;
}
