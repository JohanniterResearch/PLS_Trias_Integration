import { useState, useEffect } from 'react';
import BodyPart from '../components/BodyPartsFront';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';
import { saveBodyPart, getBodyParts } from '../api/endpoints';

const ShowBodyFront = () => {
  const [bodyParts, setBodyParts] = useState<Record<string, number | boolean>>({});
  const navigate = useNavigate();
  const { token } = useAuthStore();
  const location = useLocation();
  const staticPatientId = location.state?.patientId;
  const operationSceneId = location.state?.operationSceneId;
  const returnTo = location.state?.returnTo || '/TriagePage1';
  const triageResult = location.state?.triageResult;
  const [isLoading, setIsLoading] = useState(true);

  const handleBodyPartClick = async (id: string, isClicked: number | boolean) => {
    setBodyParts({ ...bodyParts, [id]: isClicked });
    try {
      await saveBodyPart(id, isClicked as number, staticPatientId, token!);
    } catch (error) {
      console.error(`Error while saving body part ${id}`, error);
    }
  };

  useEffect(() => {
    const fetchInitialBodyParts = async () => {
      try {
        const { data } = await getBodyParts(staticPatientId, token!);
        setBodyParts(data || {});
      } catch (error) {
        console.error('Error fetching body parts', error);
      } finally {
        setIsLoading(false);
      }
    };
    fetchInitialBodyParts();
  }, [staticPatientId, token]);

  const goToShowBodyBack = () => {
    navigate('/ShowBodyBack', { state: { patientId: staticPatientId, operationSceneId, returnTo, triageResult } });
  };

  const goBackToTriage = () => {
    navigate(returnTo, { state: { patientId: staticPatientId, operationSceneId, triageResult } });
  };

  return (
    <div className="bodyparts-container">
      {isLoading ? (
        <p>Loading...</p>
      ) : (
        <>
          <h1>Front</h1>
          <svg
            className="body-svg"
            viewBox="0 0 105 201"
            width="100%"
            height="100%"
            preserveAspectRatio="xMidYMid meet"
            version="1.1"
            xmlns="http://www.w3.org/2000/svg"
          >
            <g className="first-layer">
              {Object.entries(bodyParts).map(([part, clicked]) => (
                <BodyPart
                  key={part}
                  id={part}
                  onClick={handleBodyPartClick}
                  initialClickedState={clicked || false}
                />
              ))}
            </g>
          </svg>
          <div className="bodyparts-actions">
            <button className="bodyparts-button" onClick={goBackToTriage}>Zurück zur Triage</button>
            <button className="bodyparts-button" onClick={goToShowBodyBack}>Rückseite anzeigen</button>
          </div>
        </>
      )}
    </div>
  );
};

export default ShowBodyFront;
