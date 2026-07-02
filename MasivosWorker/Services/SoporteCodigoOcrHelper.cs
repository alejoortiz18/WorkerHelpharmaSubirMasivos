namespace Services;

/// <summary>
/// Variantes OCR frecuentes para reintentar DatosSoportes sin cambiar la lectura OpenAI.
/// </summary>
public static class SoporteCodigoOcrHelper
{
    public static IEnumerable<string> VariantesConsultaDatosSoportes(string codigo)
    {
        var candidatos = new List<string>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var baseVariant in VariantesConfusionI1(codigo))
        {
            AgregarVariante(baseVariant, candidatos, vistos);
            AgregarVariante(baseVariant.Replace("-", string.Empty, StringComparison.Ordinal), candidatos, vistos);

            if (baseVariant.StartsWith("FEMI", StringComparison.OrdinalIgnoreCase))
            {
                var resto = baseVariant[4..];
                var restoSinGuion = resto.Replace("-", string.Empty, StringComparison.Ordinal);
                AgregarVariante("FMI" + resto, candidatos, vistos);
                AgregarVariante("FMI" + restoSinGuion, candidatos, vistos);
                AgregarVariante("IM" + resto, candidatos, vistos);
                AgregarVariante("IM" + restoSinGuion, candidatos, vistos);
            }
        }

        return candidatos;
    }

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

    private static void AgregarVariante(string? valor, List<string> candidatos, HashSet<string> vistos)
    {
        var limpio = (valor ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(limpio) || !vistos.Add(limpio))
            return;

        candidatos.Add(limpio);
    }
}
