import axios from 'axios';

const api = axios.create({
  baseURL: 'https://localhost:7244/api',
  withCredentials: true, // Sends the refresh token cookie automatically
});

// Request interceptor to attach access token
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken');
  // console.log('Access Token:', token);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Response interceptor to auto-refresh on 401
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    // ✅ Check access token existed and retry not already done
    if (
      error.response?.status === 401 &&
      !originalRequest._retry &&
      localStorage.getItem('accessToken')
    ) {
      console.log('Refreshing access token...');
      originalRequest._retry = true;
      try {
        const response = await axios.post(
          'https://localhost:7244/api/auth/refresh-token',
          null,
          { withCredentials: true }
        );

        const { AccessToken } = response.data;
        // console.log('New Access Token:', AccessToken);
        // Save the new token
        localStorage.setItem('accessToken', AccessToken);

        // Update original request with new token
        originalRequest.headers['Authorization'] = `Bearer ${AccessToken}`;

        // Retry the original request with updated token
        return api(originalRequest);
      } catch (refreshError) {
        localStorage.removeItem('accessToken');
        window.location.href = '/login';
        return Promise.reject(refreshError);
      }
    }

    return Promise.reject(error);
  }
);

export default api;
