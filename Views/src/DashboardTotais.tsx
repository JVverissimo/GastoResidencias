import React, { useState, useEffect } from 'react';
import api, { logEstruturado } from './services/api';

function DashboardTotais() {
  const [dados, setDados] = useState({ pessoas: [], consolidadoGeral: {} });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    carregarDashboard();
  }, []);

  const carregarDashboard = async () => {
    try {
      const response = await api.get('/Totais');
      setDados(response.data);
    } catch (error) {
      logEstruturado('ERROR', 'Erro ao carregar dados do dashboard', {
        correlationId: error.response?.headers?.['x-correlation-id'] || error.config?.metadata?.correlationId,
        errorMessage: error.message,
      });
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <div className="loading">Carregando dados da nuvem...</div>;

  return (
    <div>
      <h2 className="titulo-sessao">Resumo Financeiro Consolidado</h2>
      
      {/* DASHBOARD DE CARDS SUPERIORES */}
      <div className="cards-container">
        <div className="card card-receita">
          <h3>Total Geral Receitas</h3>
          <p>R$ {dados.consolidadoGeral?.totalGeralReceitas?.toFixed(2) || "0.00"}</p>
        </div>
        <div className="card card-despesa">
          <h3>Total Geral Despesas</h3>
          <p>R$ {dados.consolidadoGeral?.totalGeralDespesas?.toFixed(2) || "0.00"}</p>
        </div>
        <div className={`card card-saldo ${dados.consolidadoGeral?.saldoLiquidoGeral >= 0 ? 'positivo' : 'negativo'}`}>
          <h3>Saldo Líquido Geral</h3>
          <p>R$ {dados.consolidadoGeral?.saldoLiquidoGeral?.toFixed(2) || "0.00"}</p>
        </div>
      </div>

      {/* TABELA DE DADOS DE ACORDO COM O PROTÓTIPO */}
      <h2 className="titulo-sessao" style={{ marginTop: '40px' }}>Detalhamento por Usuário</h2>
      <table className="tabela-layout">
        <thead>
          <tr>
            <th>#</th>
            <th>Nome</th>
            <th>Idade</th>
            <th>CPF</th>
            <th>Total Receitas</th>
            <th>Total Despesas</th>
            <th>Saldo</th>
          </tr>
        </thead>
        <tbody>
          {dados.pessoas.map((p, index) => (
            <tr key={p.pessoaId}>
              <td><strong>{index + 1}</strong></td>
              <td>{p.nome}</td>
              <td>{p.idade} anos</td>
              <td>{p.cpf}</td>
              <td className="txt-receita">R$ {p.totalReceitas.toFixed(2)}</td>
              <td className="txt-despesa">R$ {p.totalDespesas.toFixed(2)}</td>
              <td className={`txt-saldo ${p.saldo >= 0 ? 'positivo' : 'negativo'}`}>
                R$ {p.saldo.toFixed(2)}
              </td>
            </tr>
          ))}
          {dados.pessoas.length === 0 && (
            <tr>
              <td colSpan="7" style={{ textAlign: 'center', padding: '20px' }}>Nenhum dado financeiro registrado.</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

export default DashboardTotais;