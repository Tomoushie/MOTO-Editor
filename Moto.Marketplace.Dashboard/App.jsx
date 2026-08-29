// Moto.Marketplace.Dashboard/src/App.jsx
import React, { useState, useEffect } from 'react';
import axios from 'axios';

const API_BASE = 'http://localhost:5000/api/v1';

export default function App() {
  const [plugins, setPlugins] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    loadPlugins();
  }, []);

  const loadPlugins = async () => {
    try {
      setLoading(true);
      const response = await axios.get(`${API_BASE}/plugins`);
      setPlugins(response.data);
      setError(null);
    } catch (err) {
      setError('Impossible de charger les plugins');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const deletePlugin = async (id) => {
    if (!confirm(`Supprimer le plugin ${id} ?`)) return;

    try {
      await axios.delete(`${API_BASE}/plugins/${id}`);
      setPlugins(plugins.filter(p => p.id !== id));
    } catch (err) {
      alert('Erreur lors de la suppression');
      console.error(err);
    }
  };

  const uploadPlugin = async (event) => {
    const file = event.target.files[0];
    if (!file) return;

    const formData = new FormData();
    formData.append('file', file);
    formData.append('name', file.name.replace('.dll', ''));
    formData.append('version', '1.0.0');

    try {
      await axios.post(`${API_BASE}/plugins/upload`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      await loadPlugins();
      alert('Plugin uploadé avec succès !');
    } catch (err) {
      alert('Erreur lors de l\'upload');
      console.error(err);
    }
  };

  if (loading) return <div className="loading">Chargement…</div>;
  if (error) return <div className="error">{error}</div>;

  return (
    <div className="dashboard">
      <header>
        <h1>🛒 Marketplace Dashboard</h1>
        <div className="actions">
          <input
            type="file"
            accept=".dll,.zip"
            onChange={uploadPlugin}
            style={{ display: 'none' }}
            id="upload-input"
          />
          <button onClick={() => document.getElementById('upload-input').click()}>
            📤 Upload Plugin
          </button>
          <button onClick={loadPlugins}>🔄 Refresh</button>
        </div>
      </header>

      <main>
        <h2>{plugins.length} plugin(s) disponible(s)</h2>
        <div className="plugin-grid">
          {plugins.map(plugin => (
            <div key={plugin.id} className="plugin-card">
              <div className="plugin-header">
                <h3>{plugin.name}</h3>
                <span className="version">v{plugin.version}</span>
              </div>
              <p className="author">par {plugin.author}</p>
              <p className="description">{plugin.description}</p>
              <div className="stats">
                <span>⬇ {plugin.downloadCount}</span>
                <span>★ {plugin.rating.toFixed(1)}</span>
              </div>
              <div className="actions">
                <button className="btn-edit">✏️ Éditer</button>
                <button
                  className="btn-delete"
                  onClick={() => deletePlugin(plugin.id)}
                >
                  🗑️ Supprimer
                </button>
              </div>
            </div>
          ))}
        </div>
      </main>

      <style>{`
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background: #1E1F24; color: #E5E7EB; }
        .dashboard { max-width: 1200px; margin: 0 auto; padding: 20px; }
        header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 30px; padding-bottom: 20px; border-bottom: 1px solid #2A2C31; }
        h1 { font-size: 24px; color: #D97757; }
        .actions { display: flex; gap: 10px; }
        button { padding: 8px 16px; background: #D97757; color: white; border: none; border-radius: 6px; cursor: pointer; font-size: 14px; }
        button:hover { background: #C56547; }
        .btn-delete { background: #DC2626; }
        .btn-delete:hover { background: #B91C1C; }
        .plugin-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 20px; }
        .plugin-card { background: #232428; border: 1px solid #2A2C31; border-radius: 12px; padding: 20px; }
        .plugin-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
        .plugin-header h3 { font-size: 18px; color: #D97757; }
        .version { background: #2A2C31; padding: 4px 8px; border-radius: 4px; font-size: 12px; }
        .author { font-size: 14px; color: #9CA3AF; margin-bottom: 12px; }
        .description { font-size: 14px; color: #E5E7EB; margin-bottom: 16px; line-height: 1.5; }
        .stats { display: flex; gap: 16px; margin-bottom: 16px; font-size: 14px; color: #9CA3AF; }
        .plugin-card .actions { display: flex; gap: 8px; margin-top: 16px; }
        .btn-edit { background: #2563EB; }
        .btn-edit:hover { background: #1D4ED8; }
        .loading, .error { text-align: center; padding: 40px; font-size: 18px; }
        .error { color: #DC2626; }
      `}</style>
    </div>
  );
}
