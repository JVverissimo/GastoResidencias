using System.Text.RegularExpressions;

namespace project_API.Helpers
{
    /// <summary>
    /// Utilitário para mascaramento de dados pessoais e sensíveis (CPF, senhas, tokens, segredos)
    /// em conformidade com a LGPD (Lei Geral de Proteção de Dados).
    /// </summary>
    public static partial class SensitiveDataMasker
    {
        public const string RedactedSecret = "********";
        public const string DefaultMaskedCpf = "***.***.***-**";

        // Regex para identificar CPFs formatados (000.000.000-00) ou apenas 11 dígitos consecutivos
        [GeneratedRegex(@"(?<!\d)(\d{3})\.?(\d{3})\.?(\d{3})-?(\d{2})(?!\d)", RegexOptions.Compiled)]
        private static partial Regex CpfPatternRegex();

        // Regex para identificar rota /por-cpf/{valor} mesmo com CPF inválido/incompleto
        [GeneratedRegex(@"(/por-cpf/)([^/?#]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex RotaPorCpfRegex();

        // Regex para identificar parâmetros sensíveis em query strings (?senha=123&cpf=456)
        [GeneratedRegex(@"([?&](?:cpf|pessoacpf|senha|password|pwd|pass|secret|token|apikey|api_key|authorization|cvv|cartao)=)([^&#\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex QueryStringSensitiveRegex();

        // Regex para identificar chaves sensíveis em JSON ou pares chave=valor dentro de mensagens
        [GeneratedRegex(@"([""']?(?:senha|password|pwd|pass|secret|token|apikey|api_key|authorization|cvv)[""']?\s*[:=]\s*[""']?)([^""',}\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex KeyValueSecretRegex();

        /// <summary>
        /// Mascara um CPF preservando apenas os 2 dígitos verificadores finais (ex: ***.***.***-09)
        /// ou ocultando integralmente caso o formato seja inválido.
        /// </summary>
        public static string MaskCpf(string? cpf)
        {
            if (string.IsNullOrWhiteSpace(cpf))
            {
                return DefaultMaskedCpf;
            }

            var digits = new string(cpf.Where(char.IsDigit).ToArray());
            if (digits.Length != 11)
            {
                return DefaultMaskedCpf;
            }

            return $"***.***.***-{digits.Substring(9, 2)}";
        }

        /// <summary>
        /// Oculta integralmente senhas, tokens ou credenciais sem revelar seu tamanho real.
        /// </summary>
        public static string MaskSecret(string? secret)
        {
            return RedactedSecret;
        }

        /// <summary>
        /// Mascara CPFs e parâmetros sensíveis (como senha, token, cpf) presentes em URLs, rotas ou query strings.
        /// </summary>
        public static string MaskUrl(string? urlOrPath)
        {
            if (string.IsNullOrWhiteSpace(urlOrPath))
            {
                return string.Empty;
            }

            var sanitized = RotaPorCpfRegex().Replace(urlOrPath, match =>
            {
                var prefix = match.Groups[1].Value;
                var rawValue = Uri.UnescapeDataString(match.Groups[2].Value);
                return $"{prefix}{MaskCpf(rawValue)}";
            });

            sanitized = QueryStringSensitiveRegex().Replace(sanitized, match =>
            {
                var prefix = match.Groups[1].Value;
                var rawValue = Uri.UnescapeDataString(match.Groups[2].Value);
                if (prefix.Contains("cpf", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{prefix}{MaskCpf(rawValue)}";
                }
                return $"{prefix}{RedactedSecret}";
            });

            sanitized = CpfPatternRegex().Replace(sanitized, match =>
            {
                var d4 = match.Groups[4].Value;
                return $"***.***.***-{d4}";
            });

            return sanitized;
        }

        /// <summary>
        /// Sanitiza textos livres ou mensagens de erro ocultando CPFs e credenciais/senhas.
        /// </summary>
        public static string SanitizeText(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var sanitized = CpfPatternRegex().Replace(input, match =>
            {
                var d4 = match.Groups[4].Value;
                return $"***.***.***-{d4}";
            });

            sanitized = KeyValueSecretRegex().Replace(sanitized, match =>
            {
                var prefix = match.Groups[1].Value;
                return $"{prefix}{RedactedSecret}";
            });

            return sanitized;
        }
    }
}
