namespace GestionArchivosEscaneados.Infrastructure.Api;

/// <summary>
/// Variantes OCR frecuentes (I vs 1) para reintentar DatosSoportes sin cambiar la lectura OpenAI.
/// </summary>
public static class SoporteCodigoOcrHelper
{
    public static IEnumerable<string> VariantesConfusionI1(string codigo)
    {
        var limpio = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(limpio))
            yield break;

        yield return limpio;

        for (var i = 1; i < limpio.Length - 1; i++)
        {
            if (limpio[i] != '1')
                continue;
            if (!char.IsLetter(limpio[i - 1]))
                continue;
            if (!char.IsDigit(limpio[i + 1]))
                continue;

            yield return limpio[..i] + "I" + limpio[(i + 1)..];
        }

        for (var i = 1; i < limpio.Length - 1; i++)
        {
            if (limpio[i] != 'I')
                continue;
            if (!char.IsLetter(limpio[i - 1]))
                continue;
            if (!char.IsDigit(limpio[i + 1]))
                continue;

            yield return limpio[..i] + "1" + limpio[(i + 1)..];
        }
    }
}
