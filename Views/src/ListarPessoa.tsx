import React, { useState, useEffect } from 'react';
import api, { logEstruturado } from './services/api';

function ListarPessoas() {
  const [pessoas, setPessoas] = useState([]);

  useEffect(() => {
    buscarPessoas();
  }, []);

  const buscarPessoas = async () => {
    try {
      const response = await api.get('/Pessoas');
      setPessoas(response.data);
    } catch (error) {
      logEstruturado('ERROR', 'Erro ao buscar pessoas', {
        correlationId: error.response?.headers?.['x-correlation-id'] || error.config?.metadata?.correlationId,
        errorMessage: error.message,
      });
    }
  };

  const handleDeletar = async (cpf) => {
    if (window.confirm(`Deseja realmente remover a pessoa com CPF ${cpf}? Todas as transações serão excluídas.`)) {
      try {
        await api.delete(`/Pessoas/por-cpf/${cpf}`);
        alert("Usuário removido com sucesso!");
        buscarPessoas(); // Atualiza a tabela
      } catch (error) {
        alert(error.response?.data || "Erro ao deletar usuário.");
      }
    }
  };

  return (
    <div>
      <h2 className="titulo-sessao">Gerenciamento de Pessoas Cadastradas</h2>
      <table className="tabela-layout">
        <thead>
          <tr>
            <th>ID</th>
            <th>Nome</th>
            <th>Idade</th>
            <th>CPF</th>
            <th style={{ textAlign: 'center' }}>Ações</th>
          </tr>
        </thead>
        <tbody>
          {pessoas.map((p) => (
            <tr key={p.id}>
              <td>{p.id}</td>
              <td>{p.nome}</td>
              <td>{p.idade} anos</td>
              <td>{p.cpf}</td>
              <td style={{ textAlign: 'center' }}>
                <button className="btn-excluir" onClick={() => handleDeletar(p.cpf)}>Excluir</button>
              </td>
            </tr>
          ))}
          {pessoas.length === 0 && (
            <tr>
              <td colSpan="5" style={{ textAlign: 'center', padding: '20px' }}>Nenhuma pessoa cadastrada no sistema.</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

export default ListarPessoas;