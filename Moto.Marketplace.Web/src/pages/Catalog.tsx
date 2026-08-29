import { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getCatalog, getThemes, getLanguages, getSnippets } from '../api/client';
import PluginCard from '../components/PluginCard';
import SearchBar from '../components/SearchBar';
import Navbar from '../components/Navbar';

type Tab = 'plugins' | 'themes' | 'languages' | 'snippets';

export default function Catalog() {
  const [tab, setTab] = useState<Tab>('plugins');
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState<string>('');

  const { data: plugins, isLoading } = useQuery({
    queryKey: ['plugins', query, category],
    queryFn: () => getCatalog(query, category),
    enabled: tab === 'plugins',
  });

  const { data: themes } = useQuery({
    queryKey: ['themes', query],
    queryFn: () => getThemes(query),
    enabled: tab === 'themes',
  });

  const { data: languages } = useQuery({
    queryKey: ['languages'],
    queryFn: getLanguages,
    enabled: tab === 'languages',
  });

  const { data: snippets } = useQuery({
    queryKey: ['snippets', query],
    queryFn: () => getSnippets(query),
    enabled: tab === 'snippets',
  });

  return (
    <div className="min-h-screen bg-[#1E1F24] text-[#E5E7EB]">
      <Navbar />
      <div className="max-w-7xl mx-auto px-6 py-8">
        <h1 className="text-3xl font-bold mb-2">🛒 MOTO Marketplace</h1>
        <p className="text-[#9CA3AF] mb-6">Plugins, thèmes, langues et snippets pour MOTO Editor</p>

        <SearchBar
          value={query}
          onChange={setQuery}
          placeholder="Rechercher..."
        />

        <div className="flex gap-2 mb-6 border-b border-[#35373C]">
          {(['plugins', 'themes', 'languages', 'snippets'] as Tab[]).map(t => (
            <button
              key={t}
              onClick={() => setTab(t)}
              className={`px-4 py-2 font-medium transition ${
                tab === t
                  ? 'text-[#D97757] border-b-2 border-[#D97757]'
                  : 'text-[#9CA3AF] hover:text-[#E5E7EB]'
              }`}
            >
              {t === 'plugins' && '🧩 Plugins'}
              {t === 'themes' && '🎨 Thèmes'}
              {t === 'languages' && '🌐 Langues'}
              {t === 'snippets' && '✂️ Snippets'}
            </button>
          ))}
        </div>

        {isLoading && <div className="text-center py-12">Chargement…</div>}

        {tab === 'plugins' && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {plugins?.map((p: any) => <PluginCard key={p.id} item={p} type="plugin" />)}
          </div>
        )}

        {tab === 'themes' && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {themes?.map((t: any) => <PluginCard key={t.id} item={t} type="theme" />)}
          </div>
        )}

        {tab === 'languages' && (
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {languages?.map((l: any) => (
              <div key={l.code} className="bg-[#2A2C31] rounded-lg p-4 hover:bg-[#35373C] transition">
                <div className="text-2xl mb-2">{l.flag}</div>
                <div className="font-bold">{l.nativeName}</div>
                <div className="text-sm text-[#9CA3AF]">{l.name}</div>
                <button className="mt-2 px-3 py-1 bg-[#D97757] text-white rounded text-sm">
                  Installer
                </button>
              </div>
            ))}
          </div>
        )}

        {tab === 'snippets' && (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {snippets?.map((s: any) => <PluginCard key={s.id} item={s} type="snippet" />)}
          </div>
        )}
      </div>
    </div>
  );
}
