/**
 * Envía PDF completo a OpenAI con prompt explícito.
 * Uso: node probe-openai-pdf.mjs [rutaPdf] [rutaPrompt]
 */
import fs from 'fs';
import path from 'path';

const pdfPath = process.argv[2]
  ?? '\\\\192.168.0.69\\ArchivosScaneados\\dgutierrez\\2026-06-24\\noprocesados\\CRC_900277244_FE256832.pdf';
const promptPath = process.argv[3]
  ?? 'C:\\Users\\serviciosrelease\\Documents\\Desarrollos\\workerHelpharmaSubirArchivos\\WorkerHelpharmaSubirMasivos\\MasivosWorker\\MasivosWorker\\Prompts\\barcode-openai.txt';
const appsettingsPath = path.resolve(
  path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1')),
  '..', '..', 'GestionArchivosEscaneados.Web', 'appsettings.json'
);

function loadOpenAiConfig() {
  const json = JSON.parse(fs.readFileSync(appsettingsPath, 'utf8'));
  const localPath = path.resolve(path.dirname(appsettingsPath), '..', 'scripts', 'appsettings.Production.local.json');
  if (fs.existsSync(localPath)) {
    const local = JSON.parse(fs.readFileSync(localPath, 'utf8'));
    if (local.OpenAi) Object.assign(json.OpenAi ??= {}, local.OpenAi);
  }
  return json.OpenAi;
}

async function main() {
  if (!fs.existsSync(pdfPath)) throw new Error(`PDF no encontrado: ${pdfPath}`);
  if (!fs.existsSync(promptPath)) throw new Error(`Prompt no encontrado: ${promptPath}`);

  const { ApiKey, Model } = loadOpenAiConfig();
  if (!ApiKey) throw new Error('OpenAi:ApiKey no configurada');

  const prompt = fs.readFileSync(promptPath, 'utf8').trimEnd();
  const pdfBytes = fs.readFileSync(pdfPath);
  const pdfBase64 = pdfBytes.toString('base64');

  console.log('=== Probe OpenAI PDF completo ===');
  console.log('PDF:', pdfPath);
  console.log('Tamaño PDF (bytes):', pdfBytes.length);
  console.log('Prompt:', promptPath);
  console.log('Prompt (caracteres):', prompt.length);
  console.log('Modelo:', Model ?? 'gpt-4.1-mini');
  console.log('---');

  const body = {
    model: Model ?? 'gpt-4.1-mini',
    temperature: 0,
    max_tokens: 32,
    messages: [{
      role: 'user',
      content: [
        { type: 'text', text: prompt },
        {
          type: 'file',
          file: {
            filename: 'documento.pdf',
            file_data: `data:application/pdf;base64,${pdfBase64}`
          }
        }
      ]
    }]
  };

  const resp = await fetch('https://api.openai.com/v1/chat/completions', {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${ApiKey}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(body)
  });

  const json = await resp.json();
  if (!resp.ok) {
    console.error('HTTP', resp.status, JSON.stringify(json, null, 2));
    process.exit(1);
  }

  const texto = json.choices?.[0]?.message?.content?.trim() ?? '';
  console.log('Respuesta cruda OpenAI:', JSON.stringify(texto));
  console.log('Longitud respuesta:', texto.length);
}

main().catch(err => {
  console.error(err.message);
  process.exit(1);
});
