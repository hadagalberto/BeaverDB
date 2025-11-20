import React, { useState } from 'react';
import { serversApi } from '../services/api';

interface AddServerModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

const AddServerModal: React.FC<AddServerModalProps> = ({ isOpen, onClose, onSuccess }) => {
  const [formData, setFormData] = useState({
    name: '',
    type: 'MySQL',
    host: 'localhost',
    port: 3306,
    username: '',
    password: '',
    isManagedByDocker: false,
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const serverTypes = [
    { value: 'MySQL', defaultPort: 3306, defaultUser: 'root' },
    { value: 'PostgreSQL', defaultPort: 5432, defaultUser: 'postgres' },
    { value: 'SQLServer', defaultPort: 1433, defaultUser: 'sa' },
    { value: 'MongoDB', defaultPort: 27017, defaultUser: 'admin' },
    { value: 'Redis', defaultPort: 6379, defaultUser: '' },
  ];

  const handleTypeChange = (type: string) => {
    const serverType = serverTypes.find((t) => t.value === type);
    if (serverType) {
      setFormData({
        ...formData,
        type,
        port: serverType.defaultPort,
        username: serverType.defaultUser,
      });
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      await serversApi.create(formData);
      onSuccess();
      onClose();
      // Reset form
      setFormData({
        name: '',
        type: 'MySQL',
        host: 'localhost',
        port: 3306,
        username: '',
        password: '',
        isManagedByDocker: false,
      });
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to create server');
    } finally {
      setLoading(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-gray-800 rounded-lg p-6 max-w-2xl w-full max-h-[90vh] overflow-y-auto">
        <div className="flex justify-between items-center mb-6">
          <h3 className="text-2xl font-semibold text-white">Add Database Server</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-white text-2xl"
          >
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Server Name */}
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-2">
              Server Name *
            </label>
            <input
              type="text"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              className="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              placeholder="My MySQL Server"
              required
            />
          </div>

          {/* Server Type */}
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-2">
              Database Type *
            </label>
            <select
              value={formData.type}
              onChange={(e) => handleTypeChange(e.target.value)}
              className="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              {serverTypes.map((type) => (
                <option key={type.value} value={type.value}>
                  {type.value}
                </option>
              ))}
            </select>
          </div>

          {/* Docker Managed */}
          <div className="flex items-center space-x-3 p-4 bg-gray-700/50 rounded-lg">
            <input
              type="checkbox"
              id="dockerManaged"
              checked={formData.isManagedByDocker}
              onChange={(e) =>
                setFormData({
                  ...formData,
                  isManagedByDocker: e.target.checked,
                  host: e.target.checked ? formData.type.toLowerCase() : 'localhost',
                })
              }
              className="w-4 h-4 text-blue-600 bg-gray-600 border-gray-500 rounded focus:ring-blue-500"
            />
            <label htmlFor="dockerManaged" className="text-sm text-gray-300">
              <span className="font-medium text-white">Managed by Docker</span>
              <p className="text-xs text-gray-400 mt-1">
                BeaverDB will create and manage a Docker container for this server
              </p>
            </label>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* Host */}
            <div>
              <label className="block text-sm font-medium text-gray-300 mb-2">
                Host *
              </label>
              <input
                type="text"
                value={formData.host}
                onChange={(e) => setFormData({ ...formData, host: e.target.value })}
                className="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="localhost"
                required
                disabled={formData.isManagedByDocker}
              />
              {formData.isManagedByDocker && (
                <p className="text-xs text-gray-400 mt-1">
                  Auto-set to container name
                </p>
              )}
            </div>

            {/* Port */}
            <div>
              <label className="block text-sm font-medium text-gray-300 mb-2">
                Port *
              </label>
              <input
                type="number"
                value={formData.port}
                onChange={(e) =>
                  setFormData({ ...formData, port: parseInt(e.target.value) })
                }
                className="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
                required
              />
            </div>
          </div>

          {/* Username */}
          {formData.type !== 'Redis' && (
            <div>
              <label className="block text-sm font-medium text-gray-300 mb-2">
                Username {formData.isManagedByDocker ? '' : '*'}
              </label>
              <input
                type="text"
                value={formData.username}
                onChange={(e) => setFormData({ ...formData, username: e.target.value })}
                className="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder={formData.type === 'MySQL' ? 'root' : 'postgres'}
                required={!formData.isManagedByDocker}
              />
            </div>
          )}

          {/* Password */}
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-2">
              Password *
            </label>
            <input
              type="password"
              value={formData.password}
              onChange={(e) => setFormData({ ...formData, password: e.target.value })}
              className="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              placeholder="••••••••"
              required
            />
            {formData.isManagedByDocker && (
              <p className="text-xs text-gray-400 mt-1">
                This will be set as the root/admin password for the new container
              </p>
            )}
          </div>

          {/* Error Message */}
          {error && (
            <div className="bg-red-500/20 border border-red-500 text-red-200 px-4 py-3 rounded-lg">
              {error}
            </div>
          )}

          {/* Info Box */}
          {formData.isManagedByDocker && (
            <div className="bg-blue-500/20 border border-blue-500 text-blue-200 px-4 py-3 rounded-lg text-sm">
              <p className="font-medium mb-1">🐳 Docker Container will be created</p>
              <ul className="list-disc list-inside space-y-1 text-xs">
                <li>Image: {formData.type.toLowerCase()}:latest</li>
                <li>Port mapping: {formData.port}:{formData.port}</li>
                <li>Auto-start enabled</li>
              </ul>
            </div>
          )}

          {/* Buttons */}
          <div className="flex gap-3 pt-4">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 px-4 py-3 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition"
              disabled={loading}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="flex-1 px-4 py-3 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition disabled:opacity-50"
              disabled={loading}
            >
              {loading ? 'Creating...' : 'Create Server'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default AddServerModal;
