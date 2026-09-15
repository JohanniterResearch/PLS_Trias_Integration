import { useEffect, useState } from 'react';
import type { ReactElement } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';

interface Props {
  element: ReactElement;
}

const ProtectedRoute = ({ element }: Props) => {
  const { token, validateToken } = useAuthStore();
  const [checkedToken, setCheckedToken] = useState<string | null>(null);
  const [isTokenValid, setIsTokenValid] = useState(false);
  const isChecking = Boolean(token) && checkedToken !== token;

  useEffect(() => {
    let isActive = true;

    Promise.resolve(token ? validateToken(token) : false)
      .catch(() => false)
      .then((isValid) => {
        if (!isActive) {
          return;
        }

        setCheckedToken(token);
        setIsTokenValid(isValid);
      });

    return () => {
      isActive = false;
    };
  }, [token, validateToken]);

  if (isChecking) return null;
  return isTokenValid ? <>{element}</> : <Navigate to="/qrAuthenticator" replace />;
};

export default ProtectedRoute;
