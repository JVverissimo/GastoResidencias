using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace project_API.Services
{
    /// <summary>
    /// Coletor thread-safe para instrumentação do Método RED (Rate, Errors, Duration)
    /// e Métricas de Negócio (volume financeiro transacionado, ticket médio, operações e rejeições).
    /// </summary>
    public class MetricsCollector
    {
        private static readonly double[] LatencyBucketsMs = { 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000 };
        private const int MaxLatencySamples = 2048;

        private readonly DateTime _startTimeUtc = DateTime.UtcNow;
        private readonly object _redLock = new();
        private readonly object _businessLock = new();

        // --- Métricas RED (Rate, Errors, Duration) ---
        private long _requestsTotal;
        private long _requestsInProgress;
        private long _requestsErrorsTotal;
        private long _requestsErrors4xxTotal;
        private long _requestsErrors5xxTotal;

        private long _requestDurationCount;
        private double _requestDurationSumMs;
        private double _requestDurationMinMs = double.MaxValue;
        private double _requestDurationMaxMs;
        private readonly long[] _latencyBucketCounts = new long[LatencyBucketsMs.Length];
        private readonly Queue<double> _recentLatenciesMs = new();
        private readonly Queue<DateTime> _recentRequestTimestampsUtc = new();

        private sealed class EndpointRedStats
        {
            public long Count;
            public long Errors;
            public double DurationSumMs;
        }

        private readonly ConcurrentDictionary<string, long> _requestsByRouteAndStatus = new();
        private readonly ConcurrentDictionary<string, EndpointRedStats> _endpointStats = new();

        // --- Métricas de Negócio (Volume Transacionado e Operações) ---
        private long _pessoasCadastradasTotal;
        private long _pessoasRemovidasTotal;
        private readonly ConcurrentDictionary<string, long> _pessoasRejeitadasPorMotivo = new();

        private long _transacoesReceitaCount;
        private long _transacoesDespesaCount;
        private decimal _volumeReceitaReais;
        private decimal _volumeDespesaReais;
        private readonly ConcurrentDictionary<string, long> _transacoesRejeitadasPorMotivo = new();

        private long _consultasTotaisCount;

        public void IncrementRequestsInProgress()
        {
            Interlocked.Increment(ref _requestsInProgress);
        }

        public void DecrementRequestsInProgress()
        {
            Interlocked.Decrement(ref _requestsInProgress);
        }

        public (double RatePerSecondWindow60s, double ErrorPercentage) RecordHttpRequest(
            string method,
            string path,
            int statusCode,
            double elapsedMs)
        {
            var nowUtc = DateTime.UtcNow;
            var totalReqs = Interlocked.Increment(ref _requestsTotal);

            bool isError = statusCode >= 400;
            long totalErrors = Interlocked.Read(ref _requestsErrorsTotal);

            if (isError)
            {
                totalErrors = Interlocked.Increment(ref _requestsErrorsTotal);
                if (statusCode >= 500)
                {
                    Interlocked.Increment(ref _requestsErrors5xxTotal);
                }
                else
                {
                    Interlocked.Increment(ref _requestsErrors4xxTotal);
                }
            }

            double rateWindow60s;
            lock (_redLock)
            {
                _requestDurationCount++;
                _requestDurationSumMs += elapsedMs;

                if (elapsedMs < _requestDurationMinMs)
                {
                    _requestDurationMinMs = elapsedMs;
                }
                if (elapsedMs > _requestDurationMaxMs)
                {
                    _requestDurationMaxMs = elapsedMs;
                }

                for (int i = 0; i < LatencyBucketsMs.Length; i++)
                {
                    if (elapsedMs <= LatencyBucketsMs[i])
                    {
                        _latencyBucketCounts[i]++;
                    }
                }

                if (_recentLatenciesMs.Count >= MaxLatencySamples)
                {
                    _recentLatenciesMs.Dequeue();
                }
                _recentLatenciesMs.Enqueue(elapsedMs);

                _recentRequestTimestampsUtc.Enqueue(nowUtc);
                var cutoff = nowUtc.AddSeconds(-60);
                while (_recentRequestTimestampsUtc.Count > 0 && _recentRequestTimestampsUtc.Peek() < cutoff)
                {
                    _recentRequestTimestampsUtc.Dequeue();
                }

                var windowSeconds = Math.Min(60.0, Math.Max(1.0, (nowUtc - _startTimeUtc).TotalSeconds));
                rateWindow60s = _recentRequestTimestampsUtc.Count / windowSeconds;
            }

            var routeStatusKey = $"{method}|{path}|{statusCode}";
            _requestsByRouteAndStatus.AddOrUpdate(routeStatusKey, 1, (_, current) => current + 1);

            var endpointKey = $"{method}|{path}";
            var stats = _endpointStats.GetOrAdd(endpointKey, _ => new EndpointRedStats());
            lock (stats)
            {
                stats.Count++;
                if (isError)
                {
                    stats.Errors++;
                }
                stats.DurationSumMs += elapsedMs;
            }

            var errorPercentage = totalReqs > 0 ? (totalErrors * 100.0) / totalReqs : 0.0;
            return (rateWindow60s, errorPercentage);
        }

        public (double RatePerSecondWindow60s, double ErrorPercentage) RecordHttpUnhandledException(
            string method,
            string path,
            double elapsedMs)
        {
            return RecordHttpRequest(method, path, 500, elapsedMs);
        }

        public void IncrementPessoaCadastrada()
        {
            Interlocked.Increment(ref _pessoasCadastradasTotal);
        }

        public void IncrementPessoaRemovida()
        {
            Interlocked.Increment(ref _pessoasRemovidasTotal);
        }

        public void RecordPessoaRejeitada(string motivo)
        {
            _pessoasRejeitadasPorMotivo.AddOrUpdate(motivo, 1, (_, current) => current + 1);
        }

        public decimal RecordTransacaoRegistrada(bool isReceita, decimal valor)
        {
            var valorAbsoluto = Math.Abs(valor);
            lock (_businessLock)
            {
                if (isReceita)
                {
                    _transacoesReceitaCount++;
                    _volumeReceitaReais += valorAbsoluto;
                }
                else
                {
                    _transacoesDespesaCount++;
                    _volumeDespesaReais += valorAbsoluto;
                }

                return _volumeReceitaReais + _volumeDespesaReais;
            }
        }

        public void RecordTransacaoRejeitada(string motivo)
        {
            _transacoesRejeitadasPorMotivo.AddOrUpdate(motivo, 1, (_, current) => current + 1);
        }

        public void IncrementConsultaTotais()
        {
            Interlocked.Increment(ref _consultasTotaisCount);
        }

        public string FormatPrometheusMetrics(
            bool dbUp,
            int? dbPessoasCount = null,
            int? dbTransacoesCount = null,
            decimal? dbTotalReceitas = null,
            decimal? dbTotalDespesas = null)
        {
            var sb = new StringBuilder();
            var nowUtc = DateTime.UtcNow;
            var uptimeSeconds = Math.Max(0.001, (nowUtc - _startTimeUtc).TotalSeconds);
            var memoryBytes = GC.GetTotalMemory(forceFullCollection: false);
            var workingSetBytes = Process.GetCurrentProcess().WorkingSet64;

            var totalReqs = Interlocked.Read(ref _requestsTotal);
            var totalErrors = Interlocked.Read(ref _requestsErrorsTotal);
            var errors4xx = Interlocked.Read(ref _requestsErrors4xxTotal);
            var errors5xx = Interlocked.Read(ref _requestsErrors5xxTotal);
            var inProgress = Interlocked.Read(ref _requestsInProgress);

            var overallRatePerSec = totalReqs / uptimeSeconds;
            var errorRatio = totalReqs > 0 ? (double)totalErrors / totalReqs : 0.0;
            var errorPercentage = errorRatio * 100.0;

            long durationCount;
            double durationSumMs;
            double minMs;
            double maxMs;
            long[] bucketSnapshot;
            double[] samplesSnapshot;
            double rateWindow60s;

            lock (_redLock)
            {
                durationCount = _requestDurationCount;
                durationSumMs = _requestDurationSumMs;
                minMs = _requestDurationCount > 0 ? _requestDurationMinMs : 0.0;
                maxMs = _requestDurationMaxMs;
                bucketSnapshot = (long[])_latencyBucketCounts.Clone();
                samplesSnapshot = _recentLatenciesMs.ToArray();

                var cutoff = nowUtc.AddSeconds(-60);
                while (_recentRequestTimestampsUtc.Count > 0 && _recentRequestTimestampsUtc.Peek() < cutoff)
                {
                    _recentRequestTimestampsUtc.Dequeue();
                }
                var windowSeconds = Math.Min(60.0, Math.Max(1.0, uptimeSeconds));
                rateWindow60s = _recentRequestTimestampsUtc.Count / windowSeconds;
            }

            var avgDurationMs = durationCount > 0 ? durationSumMs / durationCount : 0.0;
            Array.Sort(samplesSnapshot);
            var p50Ms = CalculatePercentile(samplesSnapshot, 0.50);
            var p90Ms = CalculatePercentile(samplesSnapshot, 0.90);
            var p95Ms = CalculatePercentile(samplesSnapshot, 0.95);
            var p99Ms = CalculatePercentile(samplesSnapshot, 0.99);

            // =========================================================================
            // 1. MÉTRICAS RED (RATE, ERRORS, DURATION)
            // =========================================================================

            // R - RATE (Taxa de Requisições)
            sb.AppendLine("# HELP red_requests_total Total acumulado de requisicoes HTTP recebidas (Metodo RED - Rate).");
            sb.AppendLine("# TYPE red_requests_total counter");
            sb.AppendLine($"red_requests_total {totalReqs}");
            sb.AppendLine();

            sb.AppendLine("# HELP red_requests_rate_per_second Taxa de requisicoes HTTP por segundo (media desde o inicio e janela movel de 60s).");
            sb.AppendLine("# TYPE red_requests_rate_per_second gauge");
            sb.AppendLine($"red_requests_rate_per_second{{window=\"overall\"}} {overallRatePerSec.ToString("F4", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"red_requests_rate_per_second{{window=\"60s\"}} {rateWindow60s.ToString("F4", CultureInfo.InvariantCulture)}");
            sb.AppendLine();

            sb.AppendLine("# HELP http_requests_in_progress Numero de requisicoes HTTP em processamento no momento.");
            sb.AppendLine("# TYPE http_requests_in_progress gauge");
            sb.AppendLine($"http_requests_in_progress {inProgress}");
            sb.AppendLine();

            sb.AppendLine("# HELP http_requests_total Total acumulado de requisicoes HTTP processadas.");
            sb.AppendLine("# TYPE http_requests_total counter");
            sb.AppendLine($"http_requests_total {totalReqs}");
            sb.AppendLine();

            // E - ERRORS (Falhas e Percentual de Falhas)
            sb.AppendLine("# HELP red_requests_errors_total Total de requisicoes HTTP com falha (status >= 400) (Metodo RED - Errors).");
            sb.AppendLine("# TYPE red_requests_errors_total counter");
            sb.AppendLine($"red_requests_errors_total {totalErrors}");
            sb.AppendLine();

            sb.AppendLine("# HELP red_requests_errors_by_class_total Total de falhas HTTP separadas por classe de status (4xx vs 5xx).");
            sb.AppendLine("# TYPE red_requests_errors_by_class_total counter");
            sb.AppendLine($"red_requests_errors_by_class_total{{status_class=\"4xx\"}} {errors4xx}");
            sb.AppendLine($"red_requests_errors_by_class_total{{status_class=\"5xx\"}} {errors5xx}");
            sb.AppendLine();

            sb.AppendLine("# HELP red_requests_error_percentage Percentual de falhas sobre o total de requisicoes HTTP (0 a 100%) (Metodo RED - Errors).");
            sb.AppendLine("# TYPE red_requests_error_percentage gauge");
            sb.AppendLine($"red_requests_error_percentage {errorPercentage.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine();

            sb.AppendLine("# HELP red_requests_error_ratio Proporcao de falhas sobre o total de requisicoes HTTP (0.0 a 1.0).");
            sb.AppendLine("# TYPE red_requests_error_ratio gauge");
            sb.AppendLine($"red_requests_error_ratio {errorRatio.ToString("F4", CultureInfo.InvariantCulture)}");
            sb.AppendLine();

            sb.AppendLine("# HELP http_requests_errors_total Alias de compatibilidade para o total de requisicoes com erro.");
            sb.AppendLine("# TYPE http_requests_errors_total counter");
            sb.AppendLine($"http_requests_errors_total {totalErrors}");
            sb.AppendLine();

            // D - DURATION (Duração / Latência das Respostas)
            sb.AppendLine("# HELP red_request_duration_milliseconds Histograma de duracao/latencia das respostas HTTP em milissegundos (Metodo RED - Duration).");
            sb.AppendLine("# TYPE red_request_duration_milliseconds histogram");
            for (int i = 0; i < LatencyBucketsMs.Length; i++)
            {
                var le = LatencyBucketsMs[i].ToString(CultureInfo.InvariantCulture);
                sb.AppendLine($"red_request_duration_milliseconds_bucket{{le=\"{le}\"}} {bucketSnapshot[i]}");
            }
            sb.AppendLine($"red_request_duration_milliseconds_bucket{{le=\"+Inf\"}} {durationCount}");
            sb.AppendLine($"red_request_duration_milliseconds_sum {durationSumMs.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"red_request_duration_milliseconds_count {durationCount}");
            sb.AppendLine();

            sb.AppendLine("# HELP red_request_duration_stats_milliseconds Estatisticas de latencia das respostas HTTP (media, minima, maxima e percentis P50/P90/P95/P99).");
            sb.AppendLine("# TYPE red_request_duration_stats_milliseconds gauge");
            sb.AppendLine($"red_request_duration_stats_milliseconds{{stat=\"avg\"}} {avgDurationMs.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"red_request_duration_stats_milliseconds{{stat=\"min\"}} {minMs.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"red_request_duration_stats_milliseconds{{stat=\"max\"}} {maxMs.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"red_request_duration_stats_milliseconds{{stat=\"p50\"}} {p50Ms.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"red_request_duration_stats_milliseconds{{stat=\"p90\"}} {p90Ms.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"red_request_duration_stats_milliseconds{{stat=\"p95\"}} {p95Ms.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"red_request_duration_stats_milliseconds{{stat=\"p99\"}} {p99Ms.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine();

            sb.AppendLine("# HELP http_request_duration_milliseconds Tempo acumulado e contagem de respostas HTTP em milissegundos.");
            sb.AppendLine("# TYPE http_request_duration_milliseconds summary");
            sb.AppendLine($"http_request_duration_milliseconds_sum {durationSumMs.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"http_request_duration_milliseconds_count {durationCount}");
            sb.AppendLine();

            // Detalhamento RED por Rota/Endpoint
            sb.AppendLine("# HELP http_requests_by_route_total Total de requisicoes HTTP detalhadas por metodo, rota e status.");
            sb.AppendLine("# TYPE http_requests_by_route_total counter");
            foreach (var entry in _requestsByRouteAndStatus.OrderBy(e => e.Key))
            {
                var parts = entry.Key.Split('|');
                if (parts.Length == 3)
                {
                    var method = EscapeLabelValue(parts[0]);
                    var path = EscapeLabelValue(parts[1]);
                    var status = EscapeLabelValue(parts[2]);
                    sb.AppendLine($"http_requests_by_route_total{{method=\"{method}\",path=\"{path}\",status_code=\"{status}\"}} {entry.Value}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("# HELP red_endpoint_error_percentage Percentual de falhas (0-100%) por endpoint HTTP.");
            sb.AppendLine("# TYPE red_endpoint_error_percentage gauge");
            foreach (var entry in _endpointStats.OrderBy(e => e.Key))
            {
                var parts = entry.Key.Split('|');
                if (parts.Length == 2)
                {
                    long count;
                    long errors;
                    lock (entry.Value)
                    {
                        count = entry.Value.Count;
                        errors = entry.Value.Errors;
                    }
                    var pct = count > 0 ? (errors * 100.0) / count : 0.0;
                    var method = EscapeLabelValue(parts[0]);
                    var path = EscapeLabelValue(parts[1]);
                    sb.AppendLine($"red_endpoint_error_percentage{{method=\"{method}\",path=\"{path}\"}} {pct.ToString("F2", CultureInfo.InvariantCulture)}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("# HELP red_endpoint_duration_average_milliseconds Latencia media em milissegundos por endpoint HTTP.");
            sb.AppendLine("# TYPE red_endpoint_duration_average_milliseconds gauge");
            foreach (var entry in _endpointStats.OrderBy(e => e.Key))
            {
                var parts = entry.Key.Split('|');
                if (parts.Length == 2)
                {
                    long count;
                    double sumMs;
                    lock (entry.Value)
                    {
                        count = entry.Value.Count;
                        sumMs = entry.Value.DurationSumMs;
                    }
                    var avg = count > 0 ? sumMs / count : 0.0;
                    var method = EscapeLabelValue(parts[0]);
                    var path = EscapeLabelValue(parts[1]);
                    sb.AppendLine($"red_endpoint_duration_average_milliseconds{{method=\"{method}\",path=\"{path}\"}} {avg.ToString("F2", CultureInfo.InvariantCulture)}");
                }
            }
            sb.AppendLine();

            // =========================================================================
            // 2. MÉTRICAS DE NEGÓCIO (VOLUME TRANSACIONADO E OPERAÇÕES)
            // =========================================================================

            long receitaCount;
            long despesaCount;
            decimal volumeReceita;
            decimal volumeDespesa;

            lock (_businessLock)
            {
                receitaCount = _transacoesReceitaCount;
                despesaCount = _transacoesDespesaCount;
                volumeReceita = _volumeReceitaReais;
                volumeDespesa = _volumeDespesaReais;
            }

            var totalTransacoesSessao = receitaCount + despesaCount;
            var volumeBrutoTotalSessao = volumeReceita + volumeDespesa;
            var saldoLiquidoSessao = volumeReceita - volumeDespesa;

            var ticketMedioGeral = totalTransacoesSessao > 0 ? volumeBrutoTotalSessao / totalTransacoesSessao : 0m;
            var ticketMedioReceita = receitaCount > 0 ? volumeReceita / receitaCount : 0m;
            var ticketMedioDespesa = despesaCount > 0 ? volumeDespesa / despesaCount : 0m;

            sb.AppendLine("# HELP business_volume_transacionado_reais_total Volume financeiro bruto total movimentado na sessao em reais (Receitas + Despesas).");
            sb.AppendLine("# TYPE business_volume_transacionado_reais_total counter");
            sb.AppendLine($"business_volume_transacionado_reais_total {volumeBrutoTotalSessao.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine();

            sb.AppendLine("# HELP business_volume_transacionado_por_tipo_reais_total Volume financeiro transacionado na sessao separado por tipo (Receita vs Despesa).");
            sb.AppendLine("# TYPE business_volume_transacionado_por_tipo_reais_total counter");
            sb.AppendLine($"business_volume_transacionado_por_tipo_reais_total{{tipo=\"Receita\"}} {volumeReceita.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"business_volume_transacionado_por_tipo_reais_total{{tipo=\"Despesa\"}} {volumeDespesa.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine();

            sb.AppendLine("# HELP business_saldo_liquido_movimentado_reais Saldo liquido movimentado na sessao em reais (Receitas - Despesas).");
            sb.AppendLine("# TYPE business_saldo_liquido_movimentado_reais gauge");
            sb.AppendLine($"business_saldo_liquido_movimentado_reais {saldoLiquidoSessao.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine();

            sb.AppendLine("# HELP business_ticket_medio_transacao_reais Valor medio (ticket medio) das transacoes registradas na sessao em reais.");
            sb.AppendLine("# TYPE business_ticket_medio_transacao_reais gauge");
            sb.AppendLine($"business_ticket_medio_transacao_reais{{tipo=\"Geral\"}} {ticketMedioGeral.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"business_ticket_medio_transacao_reais{{tipo=\"Receita\"}} {ticketMedioReceita.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"business_ticket_medio_transacao_reais{{tipo=\"Despesa\"}} {ticketMedioDespesa.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine();

            sb.AppendLine("# HELP app_transacoes_operacoes_total Total de transacoes registradas com sucesso na sessao por tipo.");
            sb.AppendLine("# TYPE app_transacoes_operacoes_total counter");
            sb.AppendLine($"app_transacoes_operacoes_total{{tipo=\"Receita\"}} {receitaCount}");
            sb.AppendLine($"app_transacoes_operacoes_total{{tipo=\"Despesa\"}} {despesaCount}");
            sb.AppendLine();

            sb.AppendLine("# HELP business_transacoes_rejeitadas_total Total de transacoes rejeitadas por violacao de regras de negocio.");
            sb.AppendLine("# TYPE business_transacoes_rejeitadas_total counter");
            var rejPessoaInexistente = _transacoesRejeitadasPorMotivo.GetValueOrDefault("pessoa_inexistente", 0);
            var rejMenorIdadeReceita = _transacoesRejeitadasPorMotivo.GetValueOrDefault("menor_idade_receita", 0);
            sb.AppendLine($"business_transacoes_rejeitadas_total{{motivo=\"pessoa_inexistente\"}} {rejPessoaInexistente}");
            sb.AppendLine($"business_transacoes_rejeitadas_total{{motivo=\"menor_idade_receita\"}} {rejMenorIdadeReceita}");
            sb.AppendLine();

            sb.AppendLine("# HELP app_pessoas_operacoes_total Total de operacoes de cadastro e remocao de pessoas na sessao.");
            sb.AppendLine("# TYPE app_pessoas_operacoes_total counter");
            sb.AppendLine($"app_pessoas_operacoes_total{{operacao=\"cadastro\"}} {Interlocked.Read(ref _pessoasCadastradasTotal)}");
            sb.AppendLine($"app_pessoas_operacoes_total{{operacao=\"remocao\"}} {Interlocked.Read(ref _pessoasRemovidasTotal)}");
            sb.AppendLine();

            sb.AppendLine("# HELP business_pessoas_rejeitadas_total Total de operacoes de pessoas rejeitadas por validacao ou regra de negocio.");
            sb.AppendLine("# TYPE business_pessoas_rejeitadas_total counter");
            var rejCpfInvalido = _pessoasRejeitadasPorMotivo.GetValueOrDefault("cpf_invalido", 0);
            var rejCpfDuplicado = _pessoasRejeitadasPorMotivo.GetValueOrDefault("cpf_duplicado", 0);
            var rejNaoEncontrado = _pessoasRejeitadasPorMotivo.GetValueOrDefault("cpf_nao_encontrado", 0);
            sb.AppendLine($"business_pessoas_rejeitadas_total{{motivo=\"cpf_invalido\"}} {rejCpfInvalido}");
            sb.AppendLine($"business_pessoas_rejeitadas_total{{motivo=\"cpf_duplicado\"}} {rejCpfDuplicado}");
            sb.AppendLine($"business_pessoas_rejeitadas_total{{motivo=\"cpf_nao_encontrado\"}} {rejNaoEncontrado}");
            sb.AppendLine();

            sb.AppendLine("# HELP business_totais_consultas_total Total de consultas realizadas ao relatorio consolidado de totais.");
            sb.AppendLine("# TYPE business_totais_consultas_total counter");
            sb.AppendLine($"business_totais_consultas_total {Interlocked.Read(ref _consultasTotaisCount)}");
            sb.AppendLine();

            // =========================================================================
            // 3. MÉTRICAS DE PROCESSO E ESTADO CONSOLIDADO NO BANCO DE DADOS
            // =========================================================================

            sb.AppendLine("# HELP process_uptime_seconds Tempo de atividade do processo em segundos.");
            sb.AppendLine("# TYPE process_uptime_seconds gauge");
            sb.AppendLine($"process_uptime_seconds {uptimeSeconds.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine();

            sb.AppendLine("# HELP process_working_set_bytes Memoria fisica (Working Set) utilizada pelo processo em bytes.");
            sb.AppendLine("# TYPE process_working_set_bytes gauge");
            sb.AppendLine($"process_working_set_bytes {workingSetBytes}");
            sb.AppendLine();

            sb.AppendLine("# HELP dotnet_gc_memory_bytes Memoria gerenciada alocada no GC do .NET em bytes.");
            sb.AppendLine("# TYPE dotnet_gc_memory_bytes gauge");
            sb.AppendLine($"dotnet_gc_memory_bytes {memoryBytes}");
            sb.AppendLine();

            sb.AppendLine("# HELP app_database_up Indica se a consulta de metricas no banco de dados esta disponivel (1 = online, 0 = indisponivel).");
            sb.AppendLine("# TYPE app_database_up gauge");
            sb.AppendLine($"app_database_up {(dbUp ? 1 : 0)}");

            if (dbUp)
            {
                sb.AppendLine();
                sb.AppendLine("# HELP app_pessoas_cadastradas_atual Numero atual de pessoas cadastradas no banco de dados.");
                sb.AppendLine("# TYPE app_pessoas_cadastradas_atual gauge");
                sb.AppendLine($"app_pessoas_cadastradas_atual {dbPessoasCount ?? 0}");
                sb.AppendLine();

                sb.AppendLine("# HELP app_transacoes_cadastradas_atual Numero atual de transacoes registradas no banco de dados.");
                sb.AppendLine("# TYPE app_transacoes_cadastradas_atual gauge");
                sb.AppendLine($"app_transacoes_cadastradas_atual {dbTransacoesCount ?? 0}");
                sb.AppendLine();

                var receitas = dbTotalReceitas ?? 0m;
                var despesas = dbTotalDespesas ?? 0m;
                var saldo = receitas - despesas;
                var volumeTotalBanco = Math.Abs(receitas) + Math.Abs(despesas);

                sb.AppendLine("# HELP business_db_volume_transacionado_reais Volume financeiro total persistido no banco de dados em reais (Receitas + Despesas).");
                sb.AppendLine("# TYPE business_db_volume_transacionado_reais gauge");
                sb.AppendLine($"business_db_volume_transacionado_reais {volumeTotalBanco.ToString("F2", CultureInfo.InvariantCulture)}");
                sb.AppendLine();

                sb.AppendLine("# HELP app_financeiro_valor_reais Valores financeiros atuais consolidados no banco de dados em reais (R$).");
                sb.AppendLine("# TYPE app_financeiro_valor_reais gauge");
                sb.AppendLine($"app_financeiro_valor_reais{{categoria=\"receitas\"}} {receitas.ToString("F2", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"app_financeiro_valor_reais{{categoria=\"despesas\"}} {despesas.ToString("F2", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"app_financeiro_valor_reais{{categoria=\"saldo_liquido\"}} {saldo.ToString("F2", CultureInfo.InvariantCulture)}");
            }

            return sb.ToString();
        }

        private static double CalculatePercentile(double[] sortedSamples, double quantile)
        {
            if (sortedSamples.Length == 0)
            {
                return 0.0;
            }

            var index = (int)Math.Ceiling(quantile * sortedSamples.Length) - 1;
            index = Math.Clamp(index, 0, sortedSamples.Length - 1);
            return sortedSamples[index];
        }

        private static string EscapeLabelValue(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n");
        }
    }
}
