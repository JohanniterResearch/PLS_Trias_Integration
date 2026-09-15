import { useNavigate, useLocation } from 'react-router-dom';
import { useEffect, useState } from 'react';
import { usePatientTriage } from '../hooks/usePatientTriage';
import { TRIAGE_COLOR } from '../types/triageColor';
import { usePatientLocationStore } from '../stores/patientLocationStore';
import PatientLocationStatus from '../components/PatientLocationStatus';
import { FaUserInjured } from 'react-icons/fa';

export default function TriagePage2() {
  const navigate = useNavigate();
  const location = useLocation();
  const [red, setRed] = useState(false);
  const patientId = location.state?.patientId;
  const operationSceneId: number = location.state?.operationSceneId;
  const triageResult = location.state?.triageResult;
  const ensurePatientLocation = usePatientLocationStore((state) => state.ensurePatientLocation);
  const { setTriageColor, setRespiration } = usePatientTriage(patientId);

  useEffect(() => {
    void ensurePatientLocation(patientId);
  }, [ensurePatientLocation, patientId]);

  useEffect(() => {
    setRed(triageResult === TRIAGE_COLOR.ROT);
  }, [triageResult]);

  const handleRed = () => {
    setRed(true);
    setTriageColor(TRIAGE_COLOR.ROT, { respiration: false });
  };

  const handleNextPage = async () => {
    await setRespiration(true);
    navigate('/TriagePage3', { state: { patientId, operationSceneId } });
  };

  const handleNewPatient = () => {
    navigate('/ScanPatient', { state: { operationSceneId } });
  };

  const handleBodyClick = () => {
    navigate('/ShowBodyFront', {
      state: { patientId, operationSceneId, returnTo: '/TriagePage2', triageResult: red ? TRIAGE_COLOR.ROT : undefined },
    });
  };

  function renderContent() {
    if (!red) {
      return (
        <>
          <header className="triage-header">
            Patient:In ID: {patientId}
            <br /> nicht gehfähig
          </header>
          <main className="triage-main">
            <div className="triage-actions triage-actions--two">
              <button className="triage-button" onClick={handleNextPage}>Atmung suffizient</button>
              <button className="triage-button" onClick={handleRed}>Atmung nicht suffizient</button>
            </div>
          </main>
        </>
      );
    } else {
      return (
        <>
          <header className="triage-header">
            Patient:In ID: {patientId}
            <br /> nicht gehfähig
          </header>
          <main className="triage-main triage-main--result">
            <p className="triage-result triage-result--red">Patient wurde mit der Kategorie ROT versehen</p>
            <button className="triage-button triage-button--wide" onClick={handleNewPatient}>Nächsten Patienten laden</button>
          </main>
        </>
      );
    }
  }

  return (
    <div className="triage-page">
      {renderContent()}
      <footer className="triage-footer">
        <section className="triage-location">
          <h4>GPS Status:</h4>
          <PatientLocationStatus patientId={patientId} />
        </section>
        <button className="triage-body-button" onClick={handleBodyClick} title="Körper markieren">
          <FaUserInjured aria-hidden="true" />
          <span>Körper markieren</span>
        </button>
      </footer>
    </div>
  );
}
