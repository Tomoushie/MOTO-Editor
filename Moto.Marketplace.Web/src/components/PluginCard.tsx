// Moto.Marketplace.Web/src/components/PluginCard.tsx
import { useState } from 'react';
import SignatureBadge from './SignatureBadge';
import { apiFetch } from '../api/client';
import { useAuthStore } from '../stores/auth';

interface PricingInfo {
  model: 'Free' | 'OneTimePurchase';
  price: number;
  currency: string;
}

interface Props {
  item: any;
  type: 'plugin' | 'theme' | 'snippet';
  onLike?: () => void;
  onReport?: () => void;
  onPurchase?: () => void;
}

export default function PluginCard({ item, type, onLike, onReport, onPurchase }: Props) {
  const [liked, setLiked] = useState(false);
  const [likeCount, setLikeCount] = useState(item.likeCount || 0);
  const [showReportDialog, setShowReportDialog] = useState(false);
  const [isPurchasing, setIsPurchasing] = useState(false);

  const currentUser = useAuthStore(s => s.user);

  const handleLike = async () => {
    try {
      await apiFetch(`/plugins/${item.id}/like`, { method: 'POST' });
      setLiked(!liked);
      setLikeCount(liked ? likeCount - 1 : likeCount + 1);
      onLike?.();
    } catch (err) {
      console.error('Erreur like:', err);
    }
  };

  const handleReport = async (reason: string, details?: string) => {
    try {
      await apiFetch(`/plugins/${item.id}/report`, {
        method: 'POST',
        body: JSON.stringify({ reason, details }),
      });
      setShowReportDialog(false);
      onReport?.();
    } catch (err) {
      console.error('Erreur report:', err);
    }
  };

  const handlePurchase = async () => {
    if (!item.pricing || item.pricing.model !== 'OneTimePurchase') return;
    if (!currentUser?.id) {
      alert('Veuillez vous connecter pour acheter ce plugin.');
      return;
    }

    setIsPurchasing(true);
    try {
      const response = await apiFetch<{ checkoutUrl: string; sessionId: string }>('/purchase/checkout', {
        method: 'POST',
        body: JSON.stringify({
          pluginId: item.id,
          userId: currentUser.id,
        }),
      });

      // Rediriger vers Stripe Checkout
      window.location.href = response.checkoutUrl;
    } catch (err) {
      console.error('Erreur achat:', err);
      alert('Erreur lors de la création du paiement. Veuillez réessayer.');
    } finally {
      setIsPurchasing(false);
    }
  };

  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    return date.toLocaleDateString('fr-FR', { day: 'numeric', month: 'short', year: 'numeric' });
  };

  const isPaidPlugin = item.pricing?.model === 'OneTimePurchase';
  const isFreePlugin = !item.pricing || item.pricing.model === 'Free';
  const hasLicense = item.userHasLicense === true;

  return (
    <div className="bg-[#2A2C31] rounded-lg p-4 hover:bg-[#35373C] transition border border-[#35373C]">
      <div className="flex items-start justify-between mb-2">
        <h3 className="font-bold text-[#E5E7EB]">{item.name}</h3>
        {item.signature && <SignatureBadge verified />}
      </div>

      <p className="text-sm text-[#9CA3AF] mb-3 line-clamp-2">{item.description}</p>

      {/* Métadonnées enrichies */}
      <div className="space-y-2 mb-3">
        <div className="flex items-center gap-3 text-xs text-[#9CA3AF]">
          <span>👤 {item.author}</span>
          <span>📅 {formatDate(item.publishedUtc)}</span>
        </div>
        <div className="flex items-center gap-3 text-xs text-[#9CA3AF]">
          <span>⬇️ {item.downloadCount?.toLocaleString() || 0} downloads</span>
          <span>⭐ {item.rating?.toFixed(1) || '—'}</span>
        </div>
        {item.lastUpdatedUtc && (
          <div className="text-xs text-[#9CA3AF]">
            🔄 Mis à jour : {formatDate(item.lastUpdatedUtc)}
          </div>
        )}
      </div>

      {/* Dépendances */}
      {item.dependencies?.length > 0 && (
        <div className="text-xs text-[#D97757] mb-3">
          🔗 {item.dependencies.length} dépendance(s)
        </div>
      )}

      {/* ★ Section Pricing (achat unique) */}
      {isPaidPlugin && !hasLicense && (
        <div className="mb-3 p-3 bg-[#1E1F24] rounded border border-[#D97757]">
          <div className="flex items-center justify-between">
            <div>
              <div className="text-sm text-[#9CA3AF]">Prix</div>
              <div className="text-2xl font-bold text-[#E5E7EB]">
                {item.pricing.price} {item.pricing.currency}
              </div>
              <div className="text-xs text-[#10B981]">Achat unique • Licence à vie</div>
            </div>
            <button
              onClick={handlePurchase}
              disabled={isPurchasing}
              className="px-6 py-2 bg-[#10B981] text-white rounded font-medium hover:bg-[#059669] disabled:opacity-50 disabled:cursor-not-allowed transition"
            >
              {isPurchasing ? '⏳ Préparation…' : '💳 Acheter'}
            </button>
          </div>
        </div>
      )}

      {/* Badge "Déjà acheté" */}
      {isPaidPlugin && hasLicense && (
        <div className="mb-3 p-2 bg-[#10B981]/20 rounded border border-[#10B981] text-center">
          <span className="text-sm text-[#10B981] font-medium">✅ Licence acquise</span>
        </div>
      )}

      {/* Actions */}
      <div className="flex gap-2 mb-2">
        <button
          className="flex-1 bg-[#D97757] text-white py-1.5 rounded text-sm font-medium hover:bg-[#c5664a] disabled:opacity-50 disabled:cursor-not-allowed"
          disabled={isPaidPlugin && !hasLicense}
        >
          {isPaidPlugin && !hasLicense
            ? '🔒 Achat requis'
            : type === 'theme'
              ? '🎨 Appliquer'
              : type === 'snippet'
                ? '✂️ Installer'
                : '📥 Installer'}
        </button>
        <button className="px-3 bg-[#35373C] text-[#E5E7EB] rounded text-sm hover:bg-[#44475a]">
          Détails
        </button>
      </div>

      {/* Like + Report */}
      <div className="flex gap-2 pt-2 border-t border-[#35373C]">
        <button
          onClick={handleLike}
          className={`flex items-center gap-1 px-3 py-1 rounded text-xs ${
            liked ? 'bg-[#D97757] text-white' : 'bg-[#35373C] text-[#9CA3AF] hover:bg-[#44475a]'
          }`}
        >
          {liked ? '❤️' : '🤍'} {likeCount}
        </button>
        <button
          onClick={() => setShowReportDialog(true)}
          className="flex items-center gap-1 px-3 py-1 rounded text-xs bg-[#35373C] text-[#9CA3AF] hover:bg-[#44475a]"
        >
          🚩 Signaler
        </button>
      </div>

      {/* Dialog de signalement */}
      {showReportDialog && (
        <ReportDialog
          onClose={() => setShowReportDialog(false)}
          onSubmit={handleReport}
        />
      )}
    </div>
  );
}

function ReportDialog({ onClose, onSubmit }: { onClose: () => void; onSubmit: (reason: string, details?: string) => void }) {
  const [reason, setReason] = useState('');
  const [details, setDetails] = useState('');

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-[#2A2C31] rounded-lg p-6 w-96 border border-[#35373C]">
        <h3 className="text-lg font-bold text-[#E5E7EB] mb-4">🚩 Signaler ce contenu</h3>

        <div className="space-y-3">
          <div>
            <label className="block text-sm text-[#9CA3AF] mb-1">Raison</label>
            <select
              value={reason}
              onChange={e => setReason(e.target.value)}
              className="w-full bg-[#1E1F24] px-3 py-2 rounded border border-[#35373C] text-[#E5E7EB]"
            >
              <option value="">Sélectionner…</option>
              <option value="spam">Spam</option>
              <option value="malware">Malware / Code malveillant</option>
              <option value="inappropriate">Contenu inapproprié</option>
              <option value="copyright">Violation de copyright</option>
              <option value="other">Autre</option>
            </select>
          </div>

          <div>
            <label className="block text-sm text-[#9CA3AF] mb-1">Détails (optionnel)</label>
            <textarea
              value={details}
              onChange={e => setDetails(e.target.value)}
              rows={3}
              className="w-full bg-[#1E1F24] px-3 py-2 rounded border border-[#35373C] text-[#E5E7EB]"
            />
          </div>
        </div>

        <div className="flex gap-2 mt-4">
          <button
            onClick={onClose}
            className="flex-1 bg-[#35373C] text-[#E5E7EB] py-2 rounded hover:bg-[#44475a]"
          >
            Annuler
          </button>
          <button
            onClick={() => onSubmit(reason, details)}
            disabled={!reason}
            className="flex-1 bg-[#EF4444] text-white py-2 rounded disabled:opacity-50 hover:bg-[#DC2626]"
          >
            Signaler
          </button>
        </div>
      </div>
    </div>
  );
}
