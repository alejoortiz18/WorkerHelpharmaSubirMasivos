-- Script de inicialización de Configuraciones
-- Ejecutar en BD: Scaneados
-- Propósito: Insertar configuraciones iniciales (ej: Prompt OpenAI)

USE Scaneados;

-- Insertar o actualizar el prompt de OpenAI para detección de códigos de barras
IF NOT EXISTS (SELECT 1 FROM dbo.Configuraciones WHERE Clave = 'OpenAi:PromptBarcode')
BEGIN
    INSERT INTO dbo.Configuraciones (Clave, Valor, Descripcion)
    VALUES (
        'OpenAi:PromptBarcode',
        'Lee el documento PDF adjunto completo.

Tu tarea: localizar el código de barras principal e identificar el texto legible impreso justo DEBAJO de ese código de barras.

Reglas estrictas:
- NO leas las barras del código de barras; solo el texto humano visible debajo.
- Elimina caracteres especiales visibles (por ejemplo *).
- Responde UNA sola línea con ese texto, sin explicaciones.
- Si hay letras y números (ejemplo: KI-434411 o FBO79606), devuelve el texto tal como se ve debajo del barcode, sin asteriscos.
- Si no hay código de barras visible o no puedes leer el texto debajo con certeza, responde exactamente: NO_BARCODE',
        'Prompt para detección de códigos de barras en OpenAI - Versión estándar'
    );
    PRINT 'Configuración OpenAi:PromptBarcode insertada.';
END
ELSE
BEGIN
    PRINT 'Configuración OpenAi:PromptBarcode ya existe.';
END

-- Verificación
SELECT ConfiguracionId, Clave, LEFT(Valor, 100) AS ValorPreview, Descripcion, FechaCreacion
FROM dbo.Configuraciones
ORDER BY FechaCreacion DESC;
