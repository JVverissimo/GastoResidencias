import React, { useState } from 'react';
import DashboardTotais from './DashboardTotais';
import CadastrarPessoa from './CadastraPessoa';
import ListarPessoas from './ListarPessoa';
import GerenciarTransacoes from './GerenciamentoTransacao';
import './assets/estilos.css';

function App() {
  const [telaAtiva, setTelaAtiva] = useState('dashboard');

  return (
    <div className="app-container">
      {/* HEADER ESCURO DO PROTÓTIPO */}
      <header className="header-escuro">
        <div className="header-titulo" onClick={() => setTelaAtiva('dashboard')}>
          Controle Financeiro Integrador
        </div>
        <nav className="header-nav">
          <button className={`nav-link ${telaAtiva === 'dashboard' ? 'active' : ''}`} onClick={() => setTelaAtiva('dashboard')}>Dashboard</button>
          <button className={`nav-link ${telaAtiva === 'cadastrar-pessoa' ? 'active' : ''}`} onClick={() => setTelaAtiva('cadastrar-pessoa')}>+ Pessoa</button>
          <button className={`nav-link ${telaAtiva === 'listar-pessoas' ? 'active' : ''}`} onClick={() => setTelaAtiva('listar-pessoas')}>Gerenciar Pessoas</button>
          <button className={`nav-link ${telaAtiva === 'transacoes' ? 'active' : ''}`} onClick={() => setTelaAtiva('transacoes')}>Transações</button>
        </nav>
      </header>
      {/* RENDERIZAÇÃO DINÂMICA DAS VIEWS */}
      <main className="conteudo-principal">
        {telaAtiva === 'dashboard' && <DashboardTotais />}
        {telaAtiva === 'cadastrar-pessoa' && <CadastrarPessoa />}
        {telaAtiva === 'listar-pessoas' && <ListarPessoas />}
        {telaAtiva === 'transacoes' && <GerenciarTransacoes />}
      </main>
    </div>
  );
}

export default App;
