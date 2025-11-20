import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { serversApi } from '../services/api';
import { useAuth } from '../context/AuthContext';
import AddServerModal from '../components/AddServerModal';

interface Server {
  id: number;
  name: string;
  type: string;
  host: string;
  port: number;
  isManagedByDocker: boolean;
  status?: string;
  lastConnectionSuccess?: boolean;
}

const Dashboard: React.FC = () => {
  const [servers, setServers] = useState<Server[]>([]);
  const [loading, setLoading] = useState(true);
  const [showAddModal, setShowAddModal] = useState(false);
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    loadServers();
  }, []);

  const loadServers = async () => {
    try {
      const response = await serversApi.getAll();
      setServers(response.data);
    } catch (error) {
      console.error('Failed to load servers', error);
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteServer = async (id: number) => {
    if (!confirm('Are you sure you want to delete this server?')) return;
    
    try {
      await serversApi.delete(id);
      loadServers();
    } catch (error) {
      console.error('Failed to delete server', error);
    }
  };

  const getTypeColor = (type: string) => {
    const colors: Record<string, string> = {
      MySQL: 'bg-blue-500',
      PostgreSQL: 'bg-indigo-500',
      SQLServer: 'bg-purple-500',
      MongoDB: 'bg-green-500',
      Redis: 'bg-red-500',
    };
    return colors[type] || 'bg-gray-500';
  };

  return (
    <div className="min-h-screen bg-gray-900">
      {/* Header */}
      <header className="bg-gray-800 border-b border-gray-700">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4 flex justify-between items-center">
          <div>
            <h1 className="text-2xl font-bold text-white">BeaverDB</h1>
            <p className="text-sm text-gray-400">Database Management Panel</p>
          </div>
          <div className="flex items-center gap-4">
            <span className="text-gray-300">Welcome, {user?.username}</span>
            <button
              onClick={logout}
              className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg transition"
            >
              Logout
            </button>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="flex justify-between items-center mb-6">
          <h2 className="text-xl font-semibold text-white">Database Servers</h2>
          <button
            onClick={() => setShowAddModal(true)}
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition flex items-center gap-2"
          >
            <span>+</span> Add Server
          </button>
        </div>

        {loading ? (
          <div className="text-center text-gray-400 py-12">Loading...</div>
        ) : servers.length === 0 ? (
          <div className="text-center py-12 bg-gray-800 rounded-lg">
            <p className="text-gray-400 mb-4">No servers configured yet</p>
            <button
              onClick={() => setShowAddModal(true)}
              className="px-6 py-3 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition"
            >
              Add Your First Server
            </button>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {servers.map((server) => (
              <div
                key={server.id}
                className="bg-gray-800 rounded-lg p-6 border border-gray-700 hover:border-gray-600 transition cursor-pointer"
                onClick={() => navigate(`/servers/${server.id}`)}
              >
                <div className="flex items-start justify-between mb-4">
                  <div className="flex items-center gap-3">
                    <div className={`w-3 h-3 rounded-full ${getTypeColor(server.type)}`} />
                    <div>
                      <h3 className="text-lg font-semibold text-white">{server.name}</h3>
                      <p className="text-sm text-gray-400">{server.type}</p>
                    </div>
                  </div>
                  {server.isManagedByDocker && (
                    <span className="px-2 py-1 bg-blue-500/20 text-blue-300 text-xs rounded">
                      Docker
                    </span>
                  )}
                </div>

                <div className="space-y-2 text-sm">
                  <div className="flex justify-between text-gray-400">
                    <span>Host:</span>
                    <span className="text-gray-300">{server.host}:{server.port}</span>
                  </div>
                  {server.status && (
                    <div className="flex justify-between text-gray-400">
                      <span>Status:</span>
                      <span className={`${server.status === 'running' ? 'text-green-400' : 'text-red-400'}`}>
                        {server.status}
                      </span>
                    </div>
                  )}
                  {server.lastConnectionSuccess !== undefined && (
                    <div className="flex justify-between text-gray-400">
                      <span>Connection:</span>
                      <span className={server.lastConnectionSuccess ? 'text-green-400' : 'text-red-400'}>
                        {server.lastConnectionSuccess ? 'Success' : 'Failed'}
                      </span>
                    </div>
                  )}
                </div>

                <div className="mt-4 pt-4 border-t border-gray-700 flex gap-2">
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      navigate(`/servers/${server.id}`);
                    }}
                    className="flex-1 px-3 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm rounded transition"
                  >
                    Manage
                  </button>
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      handleDeleteServer(server.id);
                    }}
                    className="px-3 py-2 bg-red-600 hover:bg-red-700 text-white text-sm rounded transition"
                  >
                    Delete
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </main>

      {/* Add Server Modal */}
      <AddServerModal
        isOpen={showAddModal}
        onClose={() => setShowAddModal(false)}
        onSuccess={loadServers}
      />
    </div>
  );
};

export default Dashboard;
