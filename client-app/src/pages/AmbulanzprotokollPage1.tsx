import { useEffect } from 'react';
import { useWatch } from 'react-hook-form';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import Appbar from '../components/Appbar';
import {
  AnamneseBodyMap,
  BrandHeader,
  IncidentHeader,
  MedicationGrid,
  PatientBlock,
  PrimaryAssessment,
} from '../features/ambulanzprotokoll-page1/page1UpperZones';
import { FindingsZone, FooterZone, MeasuresZone } from '../features/ambulanzprotokoll-page1/page1LowerZones';
import type { Page1State } from '../features/ambulanzprotokoll-page1/page1State';
import { useAmbulanzprotokollPage1Workflow } from '../features/ambulanzprotokoll-page1/page1Workflow';
import { useAuthStore } from '../stores/authStore';
import '../styles/ambulanzprotokoll-page1.css';

function downloadJson(filename: string, content: string) {
  const blob = new Blob([content], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}

export default function AmbulanzprotokollPage1() {
  const { patientId } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const { token } = useAuthStore();
  const operationSceneId = typeof location.state?.operationSceneId === 'number' ? location.state.operationSceneId : null;
  const workflow = useAmbulanzprotokollPage1Workflow({
    patientId: Number(patientId ?? 0),
    operationSceneId,
    token,
  });
  const draft = (useWatch({ control: workflow.form.control }) ?? workflow.form.getValues()) as Page1State;

  useEffect(() => {
    document.title = `Ambulanzprotokoll Seite 1 - ${patientId ?? 'unbekannt'}`;
  }, [patientId]);

  return <>
    <Appbar />
    <div className="ambd-shell">
      <div className="ambd-shell__content">
        <header className="ambd-toolbar">
          <div className="ambd-toolbar__intro">
            <p className="ambd-toolbar__eyebrow">Johanniter Ambulanzprotokoll</p>
            <h1 className="ambd-toolbar__title">Seite 1</h1>
            <p className="ambd-toolbar__copy">Papierabbild mit identischer Abschnittsreihenfolge und unverändertem Backend-Workflow.</p>
          </div>
          <div className="ambd-toolbar__meta">
            <span className="ambd-toolbar__meta-item">patientId: {patientId ?? 'unbekannt'}</span>
            <span className="ambd-toolbar__meta-item">operationSceneId: {operationSceneId ?? 'n/a'}</span>
            <span className="ambd-toolbar__meta-item">Status: {workflow.status}</span>
            <span className="ambd-toolbar__meta-item">Sync: {workflow.syncState}</span>
            {workflow.lastSavedAt ? <span className="ambd-toolbar__meta-item">Gespeichert: {workflow.lastSavedAt}</span> : null}
          </div>
          <div className="ambd-toolbar__actions">
            <button className="ambd-toolbar__button" onClick={() => navigate('/SituationRoomTable', { state: operationSceneId === null ? undefined : { operationSceneId } })} type="button">Zurück</button>
            <button className="ambd-toolbar__button" onClick={() => window.print()} type="button">Drucken</button>
            <button className="ambd-toolbar__button" onClick={() => downloadJson(`ambulanzprotokoll-page1-${patientId ?? 'draft'}.json`, workflow.exportJson)} type="button">Export JSON</button>
            <button className="ambd-toolbar__button ambd-toolbar__button--primary" onClick={() => void workflow.finalize()} type="button">Finalisieren</button>
          </div>
        </header>

        {workflow.errorMessage ? <p className="ambd-error" role="alert">{workflow.errorMessage}</p> : null}
        {workflow.validationWarnings.length > 0 ? <div className="ambd-warning" role="status">
          <strong>Hinweise zur Finalisierung</strong>
          <ul>{workflow.validationWarnings.map((warning) => <li key={warning}>{warning}</li>)}</ul>
        </div> : null}

        <div className="ambd-paper-wrap">
          <div className="ambd-page" aria-label="Johanniter Ambulanzprotokoll Seite 1">
            <BrandHeader />
            <IncidentHeader form={workflow.form} />
            <PatientBlock form={workflow.form} />
            <PrimaryAssessment form={workflow.form} />
            <AnamneseBodyMap draft={draft} form={workflow.form} onBodyMapChange={workflow.setBodyMap} />
            <MedicationGrid draft={draft} form={workflow.form} />
            <FindingsZone draft={draft} form={workflow.form} />
            <MeasuresZone form={workflow.form} />
            <FooterZone draft={draft} form={workflow.form} onSignatureChange={workflow.setSignature} />
          </div>
        </div>
      </div>
    </div>
  </>;
}
