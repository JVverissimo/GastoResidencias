import axios from 'axios';

const CHAVES_SECRETAS = /^(senha|password|pwd|pass|secret|token|apikey|api_key|authorization|cvv|cartao)$/i;
const CHAVES_CPF = /^(cpf|pessoacpf)$/i;
const REGEX_CPF = /(?<!\d)(\d{3})\.?(\d{3})\.?(\d{3})-?(\d{2})(?!\d)/g;
const REGEX_ROTA_CPF = /(\/por-cpf\/)([^/?#]+)/gi;

export const mascararCpf = (cpf) => {
  if (!cpf || typeof cpf !== 'string') return '***.***.***-**';
  const digitos = cpf.replace(/\D/g, '');
  if (digitos.length !== 11) return '***.***.***-**';
  return `***.***.***-${digitos.slice(9, 11)}`;
};

export const mascararTextoSensivel = (texto) => {
  if (typeof texto !== 'string') return texto;
  return texto
    .replace(REGEX_ROTA_CPF, (_, prefixo, valor) => `${prefixo}${mascararCpf(decodeURIComponent(valor))}`)
    .replace(REGEX_CPF, '***.***.***-$4');
};

export const sanitizarDadosSensiveis = (dados) => {
  if (dados === null || dados === undefined) return dados;
  if (typeof dados === 'string') return mascararTextoSensivel(dados);
  if (typeof dados !== 'object') return dados;
  if (Array.isArray(dados)) return dados.map(sanitizarDadosSensiveis);

  const sanitizado = {};
  for (const [chave, valor] of Object.entries(dados)) {
    if (CHAVES_SECRETAS.test(chave)) {
      sanitizado[chave] = '********';
    } else if (CHAVES_CPF.test(chave)) {
      sanitizado[chave] = mascararCpf(String(valor ?? ''));
    } else {
      sanitizado[chave] = sanitizarDadosSensiveis(valor);
    }
  }
  return sanitizado;
};

export const gerarCorrelationId = () => {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID();
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
};

export const logEstruturado = (level, message, context = {}) => {
  const payload = {
    timestamp: new Date().toISOString(),
    level,
    message: mascararTextoSensivel(message),
    ...sanitizarDadosSensiveis(context),
  };
  const jsonLog = JSON.stringify(payload);
  if (level === 'ERROR') {
    console.error(jsonLog);
  } else if (level === 'WARN') {
    console.warn(jsonLog);
  } else {
    console.info(jsonLog);
  }
};

// Aponta para o backend ASP.NET Core (perfil "http" do launchSettings.json).
// Se preferir HTTPS, troque para: https://localhost:7005/api
const api = axios.create({
  baseURL: 'http://localhost:5076/api',
});

api.interceptors.request.use((config) => {
  const correlationId = config.headers['X-Correlation-ID'] || gerarCorrelationId();
  config.headers['X-Correlation-ID'] = correlationId;
  config.metadata = { correlationId, startTime: Date.now() };

  logEstruturado('INFO', 'Requisição HTTP iniciada no cliente', {
    correlationId,
    method: config.method?.toUpperCase(),
    url: mascararTextoSensivel(config.url),
  });

  return config;
});

api.interceptors.response.use(
  (response) => {
    const correlationId =
      response.headers['x-correlation-id'] ||
      response.config.metadata?.correlationId;
    const elapsedMs = response.config.metadata?.startTime
      ? Date.now() - response.config.metadata.startTime
      : undefined;

    logEstruturado('INFO', 'Resposta HTTP recebida no cliente', {
      correlationId,
      method: response.config.method?.toUpperCase(),
      url: mascararTextoSensivel(response.config.url),
      statusCode: response.status,
      elapsedMs,
    });

    return response;
  },
  (error) => {
    const correlationId =
      error.response?.headers?.['x-correlation-id'] ||
      error.config?.metadata?.correlationId;
    const elapsedMs = error.config?.metadata?.startTime
      ? Date.now() - error.config.metadata.startTime
      : undefined;

    logEstruturado('ERROR', 'Falha na requisição HTTP no cliente', {
      correlationId,
      method: error.config?.method?.toUpperCase(),
      url: mascararTextoSensivel(error.config?.url),
      statusCode: error.response?.status,
      elapsedMs,
      errorMessage: mascararTextoSensivel(error.message),
    });

    return Promise.reject(error);
  }
);

export default api;
