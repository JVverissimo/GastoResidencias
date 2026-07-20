import React, { useState } from 'react';
import api from './services/api';

function CadastrarPessoa() {
  const [nome, setNome] = useState('');
  const [idade, setIdade] = useState('');
  const [cpf, setCpf] = useState('');
  const [mensagem, setMensagem] = useState({ tipo: '', texto: '' });

  const handleSalvar = async (e) => {
    e.preventDefault();
    setMensagem({ tipo: '', texto: '' });

    if (!nome || !idade || !cpf) {
      setMensagem({ tipo: 'erro', texto: 'Por favor, preencha todos os campos obrigatórios.' });
      return;
    }

    try {
      await api.post('/Pessoas', { nome, idade: parseInt(idade), cpf });
      setMensagem({ tipo: 'sucesso', texto: 'Pessoa cadastrada com sucesso!' });
      setNome('');
      setIdade('');
      setCpf('');
    } catch (error) {
      const apiErro = error.response?.data || "Erro de conexão com o servidor.";
      setMensagem({ tipo: 'erro', texto: apiErro });
    }
  };

  return (
    <div className="form-container">
      <h2 className="form-titulo">Nova Pessoa</h2>
      
      {mensagem.texto && <div className={`alerta ${mensagem.tipo}`}>{mensagem.texto}</div>}

      <form onSubmit={handleSalvar}>
        <div className="form-grupo">
          <label>Nome Completo*</label>
          <input type="text" value={nome} onChange={(e) => setNome(e.target.value)} placeholder="Ex: João Victor" />
        </div>

        <div className="form-grupo">
          <label>Idade*</label>
          <input type="number" value={idade} onChange={(e) => setIdade(e.target.value)} placeholder="Ex: 19" />
        </div>

        <div className="form-grupo">
          <label>CPF*</label>
          <input type="text" value={cpf} onChange={(e) => setCpf(e.target.value)} placeholder="000.000.000-00" />
        </div>

        <button type="submit" className="btn-salvar">Salvar Cadastro</button>
      </form>
    </div>
  );
}

export default CadastrarPessoa;