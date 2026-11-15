#!/usr/bin/env bash

# Garante que a pessoa base existe
curl -s -X POST http://localhost:5076/api/Pessoas \
  -H "Content-Type: application/json" \
  -d '{"nome":"Joao Silva","idade":30,"cpf":"12345678901"}' > /dev/null

echo "Iniciando gerador de carga (Pressione Ctrl+C para parar)..."

while true; do
  VALOR=$(( (RANDOM % 500) + 50 ))
  if [ $(( RANDOM % 2 )) -eq 0 ]; then
    TIPO="Receita"
  else
    TIPO="Despesa"
  fi

  # 1. Registra transacao valida (sobe business_volume_transacionado_reais_total)
  curl -s -o /dev/null -w "[SUCESSO] Transacao registrada: $TIPO de R$ $VALOR (HTTP %{http_code})\n" \
    -X POST http://localhost:5076/api/Transacoes \
    -H "Content-Type: application/json" \
    -d "{\"descricao\":\"Movimentacao $TIPO\",\"valor\":$VALOR,\"tipo\":\"$TIPO\",\"pessoaCpf\":\"12345678901\"}"

  # 2. Gera uma requisicao com erro a cada iteracao (sobe red_requests_errors_total)
  curl -s -o /dev/null -w "[ERRO]    Tentativa com CPF invalido (HTTP %{http_code})\n" \
    -X POST http://localhost:5076/api/Pessoas \
    -H "Content-Type: application/json" \
    -d '{"nome":"Teste Erro","idade":25,"cpf":"123"}'

  sleep 1
done
