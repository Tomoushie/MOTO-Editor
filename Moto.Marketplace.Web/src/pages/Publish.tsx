import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { submitPlugin } from '../api/client';
import Navbar from '../components/Navbar';

export default function Publish() {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [kind, setKind] = useState('System');
  const [file, setFile] = useState<File | null>(null);
  const [signature, setSignature] = useState('');

  const mutation = useMutation({
    mutationFn: () => submitPlugin({
      name, description, kind,
      fileName: file?.name,
      signature,
    }),
    onSuccess: () => alert('✅ Plugin soumis pour vérification !'),
  });

  return (
    <div className="min-h-screen bg-[#1E1F24] text-[#E5E7EB]">
      <Navbar />
      <div className="max-w-2xl mx-auto px-6 py-8">
        <h1 className="text-3xl font-bold mb-6">📤 Publier un plugin</h1>

        <div className="space-y-4">
          <div>
            <label className="block text-sm mb-1">Nom</label>
            <input
              className="w-full bg-[#2A2C31] px-3 py-2 rounded border border-[#35373C]"
              value={name}
              onChange={e => setName(e.target.value)}
            />
          </div>

          <div>
            <label className="block text-sm mb-1">Description</label>
            <textarea
              className="w-full bg-[#2A2C31] px-3 py-2 rounded border border-[#35373C]"
              rows={3}
              value={description}
              onChange={e => setDescription(e.target.value)}
            />
          </div>

          <div>
            <label className="block text-sm mb-1">Type</label>
            <select
              className="w-full bg-[#2A2C31] px-3 py-2 rounded border border-[#35373C]"
              value={kind}
              onChange={e => setKind(e.target.value)}
            >
              <option>System</option>
              <option>Ai</option>
              <option>Ui</option>
            </select>
          </div>

          <div>
            <label className="block text-sm mb-1">Archive (.zip)</label>
            <input
              type="file"
              accept=".zip"
              onChange={e => setFile(e.target.files?.[0] ?? null)}
            />
          </div>

          <div>
            <label className="block text-sm mb-1">Signature Ed25519</label>
            <input
              className="w-full bg-[#2A2C31] px-3 py-2 rounded border border-[#35373C] font-mono text-xs"
              value={signature}
              onChange={e => setSignature(e.target.value)}
              placeholder="Signature hexadécimale de l'archive"
            />
          </div>

          <button
            disabled={mutation.isPending || !name || !file}
            onClick={() => mutation.mutate()}
            className="w-full bg-[#D97757] text-white py-2 rounded font-medium hover:bg-[#c5664a] disabled:opacity-50"
          >
            {mutation.isPending ? 'Envoi…' : 'Publier'}
          </button>
        </div>
      </div>
    </div>
  );
}
