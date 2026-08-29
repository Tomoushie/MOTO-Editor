// Moto.Marketplace.Web/src/components/AnalyticsChart.tsx
import { useQuery } from '@tanstack/react-query';
import { LineChart, Line, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, PieChart, Pie, Cell } from 'recharts';
import { apiFetch } from '../api/client';

interface AnalyticsData {
  downloads: { date: string; count: number }[];
  ratings: { rating: number; count: number }[];
  topPlugins: { name: string; downloads: number }[];
  totalDownloads: number;
  averageRating: number;
}

export default function AnalyticsChart() {
  const { data, isLoading } = useQuery({
    queryKey: ['analytics'],
    queryFn: () => apiFetch<AnalyticsData>('/analytics/dashboard'),
  });

  if (isLoading) return <div className="text-center py-12">Chargement des statistiques…</div>;
  if (!data) return null;

  const COLORS = ['#D97757', '#10B981', '#3B82F6', '#F59E0B', '#8B5CF6'];

  return (
    <div className="space-y-6">
      {/* KPIs */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-[#2A2C31] rounded-lg p-6 border border-[#35373C]">
          <div className="text-[#9CA3AF] text-sm mb-1">Total Downloads</div>
          <div className="text-3xl font-bold text-[#E5E7EB]">
            {data.totalDownloads.toLocaleString()}
          </div>
        </div>
        <div className="bg-[#2A2C31] rounded-lg p-6 border border-[#35373C]">
          <div className="text-[#9CA3AF] text-sm mb-1">Note Moyenne</div>
          <div className="text-3xl font-bold text-[#E5E7EB]">
            ⭐ {data.averageRating.toFixed(2)}
          </div>
        </div>
        <div className="bg-[#2A2C31] rounded-lg p-6 border border-[#35373C]">
          <div className="text-[#9CA3AF] text-sm mb-1">Plugins Actifs</div>
          <div className="text-3xl font-bold text-[#E5E7EB]">
            {data.topPlugins.length}
          </div>
        </div>
      </div>

      {/* Graphique Downloads (Line Chart) */}
      <div className="bg-[#2A2C31] rounded-lg p-6 border border-[#35373C]">
        <h3 className="text-lg font-bold text-[#E5E7EB] mb-4">📈 Downloads (30 derniers jours)</h3>
        <ResponsiveContainer width="100%" height={300}>
          <LineChart data={data.downloads}>
            <CartesianGrid strokeDasharray="3 3" stroke="#35373C" />
            <XAxis dataKey="date" stroke="#9CA3AF" style={{ fontSize: '12px' }} />
            <YAxis stroke="#9CA3AF" style={{ fontSize: '12px' }} />
            <Tooltip
              contentStyle={{ backgroundColor: '#1E1F24', border: '1px solid #35373C', borderRadius: '8px' }}
              labelStyle={{ color: '#E5E7EB' }}
            />
            <Line type="monotone" dataKey="count" stroke="#D97757" strokeWidth={2} dot={{ fill: '#D97757' }} />
          </LineChart>
        </ResponsiveContainer>
      </div>

      {/* Graphique Ratings (Bar Chart) */}
      <div className="bg-[#2A2C31] rounded-lg p-6 border border-[#35373C]">
        <h3 className="text-lg font-bold text-[#E5E7EB] mb-4">⭐ Répartition des Notes</h3>
        <ResponsiveContainer width="100%" height={300}>
          <BarChart data={data.ratings}>
            <CartesianGrid strokeDasharray="3 3" stroke="#35373C" />
            <XAxis dataKey="rating" stroke="#9CA3AF" style={{ fontSize: '12px' }} />
            <YAxis stroke="#9CA3AF" style={{ fontSize: '12px' }} />
            <Tooltip
              contentStyle={{ backgroundColor: '#1E1F24', border: '1px solid #35373C', borderRadius: '8px' }}
              labelStyle={{ color: '#E5E7EB' }}
            />
            <Bar dataKey="count" fill="#10B981" radius={[8, 8, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>

      {/* Top Plugins (Pie Chart) */}
      <div className="bg-[#2A2C31] rounded-lg p-6 border border-[#35373C]">
        <h3 className="text-lg font-bold text-[#E5E7EB] mb-4">🏆 Top 5 Plugins</h3>
        <ResponsiveContainer width="100%" height={300}>
          <PieChart>
            <Pie
              data={data.topPlugins.slice(0, 5)}
              dataKey="downloads"
              nameKey="name"
              cx="50%"
              cy="50%"
              outerRadius={100}
              label={(entry) => entry.name}
            >
              {data.topPlugins.slice(0, 5).map((_, index) => (
                <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
              ))}
            </Pie>
            <Tooltip
              contentStyle={{ backgroundColor: '#1E1F24', border: '1px solid #35373C', borderRadius: '8px' }}
            />
          </PieChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
