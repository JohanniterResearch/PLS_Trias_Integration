import { Navigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';

export default function LandingPage() {
  const { token } = useAuthStore();
  if (!token) {
    return <Navigate to="/qrAuthenticator" />;
  } else {
    return <Navigate to="/RoleSelection" />;
  }
}
