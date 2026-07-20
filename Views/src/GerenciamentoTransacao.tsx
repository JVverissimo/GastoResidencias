import React, { useState, useEffect } from 'react';
import api from './services/api';

function GerenciarTransacoes() {
  const [transacoes, setTransacoes] = useState([]);
  const [descricao, setDescricao] = useState('');
  const [valor, setValor] = useState('');
  const [tipo, setTipo] = useState('Receita');
  const [pessoaCpf, setPessoaCpf] = useState('');
  const [alerta, setAlerta] = useState({ tipo: '', texto: '' });

  useEffect(() => {
    buscarTransacoes();
  }, []);

  const buscarTransacoes = async () => {
    try {
      const response = await api.get('/Transacoes');
      setTransacoes(response.data);
    } catch (error) {
      console.error("Erro ao buscar histórico", error);
    }
  };

  const handleSalvarTransacao = async (e) => {
    e.preventDefault();
    setAlerta({ tipo: '', texto: '' });

    if (!descricao || !valor || !pessoaCpf) {
      setAlerta({ tipo: 'erro', texto: 'Preencha todos os campos da transação.' });
      return;
    }

    try {
      await api.post('/Transacoes', {
        descricao,
        valor: parseFloat(valor),
        tipo,
        pessoaCpf
      });
      setAlerta({ tipo: 'sucesso', texto: 'Transação registrada com sucesso!' });
      setDescricao('');
      setValor('');
      setPessoaCpf('');
      buscarTransacoes(); // Atualiza a tabela abaixo
    } catch (error) {
      setAlerta({ tipo: 'erro', texto: error.response?.data || "Erro ao salvar transação." });
    }
  };

  return (
    <div>
      <div className="form-container">
        <h2 className="form-titulo">Nova Movimentação</h2>
        {alerta.texto && <div className={`alerta ${alerta.tipo}`}>{alerta.texto}</div>}
        
        <form onSubmit={handleSalvarTransacao}>
          <div className="form-grupo">
            <label>CPF do Beneficiário/Pagador*</label>
            <input type="text" value={pessoaCpf} onChange={(e) => setPessoaCpf(e.target.value)} placeholder="000.000.000-00" />
          </div>
          <div className="form-grupo">
            <label>Descrição da Transação*</label>
            <input type="text" value={descricao} onChange={(e) => setDescricao(e.target.value)} placeholder="Ex: Monitor Dell 24" />
          </div>
          <div className="form-grupo">
            <label>Valor (R$)*</label>
            <input type="number" step="0.01" value={valor} onChange={(e) => setValor(e.target.value)} placeholder="0.00" />
          </div>
          <div className="form-grupo">
            <label>Tipo de Transação*</label>
            <select value={tipo} onChange={(e) => setTipo(e.target.value)} className="form-select">
              <option value="Receita">Receita</option>
              <option value="Despesa">Despesa</option>
            </select>
          </div>
          <button type="submit" className="btn-salvar">Registrar Transação</button>
        </form>
      </div>

      <h2 className="titulo-sessao" style={{ marginTop: '40px' }}>Histórico Geral de Lançamentos</h2>
      <table className="tabela-layout">
        <thead>
          <tr>
            <th>ID</th>
            <th>Descrição</th>
            <th>Valor</th>
            <th>Tipo</th>
            <th>CPF Vinculado</th>
            <th>Data de Lançamento</th>
          </tr>
        </thead>
        <tbody>
          {transacoes.map((t) => (
            <tr key={t.id}>
              <td>{t.id}</td>
              <td>{t.descricao}</td>
              <td className={t.tipo === 'Receita' ? 'txt-receita' : 'txt-despesa'}>
                R$ {t.valor.toFixed(2)}
              </td>
              <td>
                <span className={`badge ${t.tipo === 'Receita' ? 'bg-receita' : 'bg-despesa'}`}>
                  {t.tipo}
                </span>
              </td>
              <td>{t.pessoaCpf}</td>
              <td>{new Date(t.data).toLocaleString('pt-BR')}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default GerenciarTransacoes;