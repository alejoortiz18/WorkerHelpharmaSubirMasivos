namespace GestionArchivosEscaneados.Constants;

public static class SecretoUi
{
    public static string Enmascarar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        if (valor.Length <= 4)
            return new string('•', valor.Length);

        var visible = valor[^4..];
        var ocultos = Math.Min(valor.Length - 4, 16);
        return new string('•', ocultos) + visible;
    }

    public static bool DebeConservarValorExistente(string? valorEnviado, bool tieneValorConfigurado) =>
        tieneValorConfigurado && string.IsNullOrWhiteSpace(valorEnviado);
}
