import axios from 'axios';

// Aponta para o backend ASP.NET Core (perfil "http" do launchSettings.json).
// Se preferir HTTPS, troque para: https://localhost:7005/api
const api = axios.create({
  baseURL: 'http://localhost:5076/api',
});

export default api;
