import { useEffect, useState } from 'react';
import type { ReactElement } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';

interface Props {
  element: ReactElement;
}

const ProtectedAdminRoute = ({ element }: Props) => {
  const { adminToken, validateToken } = useAuthStore();
  const [checkedToken, setCheckedToken] = useState<string | null>(null);
  const [isTokenValid, setIsTokenValid] = useState(false);
  const isChecking = Boolean(adminToken) && checkedToken !== adminToken;

  useEffect(() => {
    let isActive = true;

    Promise.resolve(adminToken ? validateToken(adminToken) : false)
      .catch(() => false)
      .then((isValid) => {
        if (!isActive) {
          return;
        }

        setCheckedToken(adminToken);
        setIsTokenValid(isValid);
      });

    return () => {
      isActive = false;
    };
  }, [adminToken, validateToken]);

  if (isChecking) return null;
  return isTokenValid ? <>{element}</> : <Navigate to="/AdminLandingPage" replace />;
};

export default ProtectedAdminRoute;
