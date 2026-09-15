import { useEffect, useState, type KeyboardEvent } from 'react';
import L from 'leaflet';
import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet';
import { useLocation, useNavigate } from 'react-router-dom';
import 'leaflet/dist/leaflet.css';
import '../styles/MarkerStyles.css';
import { useAuthStore } from '../stores/authStore';
import type { Patient } from '../types';
import { getPersons } from '../api/endpoints';
import { isTriageColor, TRIAGE_COLORS, TRIAGE_COLOR_BACKGROUND, type TriageColor } from '../types/triageColor';

type TriageDisplay =
  | { kind: 'unassigned'; label: string; backgroundColor: string; textColor: string; border?: string }
  | { kind: 'valid'; label: string; canonical: TriageColor; backgroundColor: string; textColor: string; border?: string }
  | { kind: 'invalid'; label: string; backgroundColor: string; textColor: string; border: string };

function SituationRoomTable() {
  const location = useLocation();
  const [persons, setPersons] = useState<Patient[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [mapCenter, setMapCenter] = useState<[number, number]>([48.2715, 16.403]);
  const navigate = useNavigate();
  const { token } = useAuthStore();
  const operationSceneId: number = location.state?.operationSceneId;

  function updateMapCenter(personsList: Patient[]) {
    if (personsList.length === 0) return;
    let count = 0;
    let latSum = 0;
    let lonSum = 0;
    for (const person of personsList) {
      if (person.latitude_patient != null && person.longitude_patient != null) {
        latSum += person.latitude_patient;
        lonSum += person.longitude_patient;
        count++;
      }
    }
    if (count > 0) setMapCenter([latSum / count, lonSum / count]);
  };

  useEffect(() => {
    getPersons(operationSceneId, token!)
      .then(({ data }) => {
        setPersons(data);
        updateMapCenter(data);
        setIsLoading(false);
      })
      .catch((error) => {
        console.error('Error fetching persons:', error);
        setIsLoading(false);
      });
  }, [operationSceneId, token]);

  const getTriageDisplay = (triageColor: string | null): TriageDisplay => {
    const normalized = triageColor?.trim().toLowerCase();
    if (!normalized) {
      return {
        kind: 'unassigned',
        label: 'Keine Farbe',
        backgroundColor: 'white',
        textColor: 'black',
        border: '1px solid #ccc',
      };
    }

    if (isTriageColor(normalized)) {
      return {
        kind: 'valid',
        canonical: normalized,
        label: normalized,
        backgroundColor: TRIAGE_COLOR_BACKGROUND[normalized],
        textColor: normalized === 'gelb' ? 'black' : 'white',
      };
    }

    return {
      kind: 'invalid',
      label: `Ungültig: ${triageColor}`,
      backgroundColor: '#ff8c00',
      textColor: 'black',
      border: '2px solid #8a4b00',
    };
  };

  const createMarkerIcon = (color: string) =>
    L.divIcon({
      className: 'custom-div-icon',
      html: `<div style='background-color:${color};' class='marker-pin'></div>`,
      iconSize: [30, 42],
      iconAnchor: [15, 42],
    });

  const renderMarkers = () =>
    persons.map((person) =>
      person.latitude_patient != null && person.longitude_patient != null ? (
        <Marker
          key={person.idpatient}
          position={[person.latitude_patient, person.longitude_patient]}
          icon={createMarkerIcon(getTriageDisplay(person.triagefarbe).backgroundColor)}
        >
          <Popup>
            Nummer: {person.idpatient}
            <br />
            Kontaminiert: {person.kontaminiert ? 'Ja' : 'Nein'}
          </Popup>
        </Marker>
      ) : null
    );

  const openPage1 = (patientId: number) => {
    navigate(`/AmbulanzprotokollPage1/${patientId}`, { state: { operationSceneId } });
  };

  const handleRowKeyDown = (event: KeyboardEvent<HTMLTableRowElement>, patientId: number) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      openPage1(patientId);
    }
  };

  if (isLoading) return <p>Lädt...</p>;
  if (persons.length === 0) return <p>Keine Personen gefunden.</p>;

  const triageDisplays = persons.map((person) => ({
    person,
    triageDisplay: getTriageDisplay(person.triagefarbe),
  }));

  const colorCounts = TRIAGE_COLORS.reduce<Record<TriageColor, number>>((acc, color) => {
    acc[color] = triageDisplays.filter(({ triageDisplay }) => triageDisplay.kind === 'valid'
      && triageDisplay.canonical === color).length;
    return acc;
  }, { rot: 0, gelb: 0, grün: 0, blau: 0, schwarz: 0 });
  const unassigned = triageDisplays.filter(({ triageDisplay }) => triageDisplay.kind === 'unassigned').length;
  const invalid = triageDisplays.filter(({ triageDisplay }) => triageDisplay.kind === 'invalid').length;

  const badgeTextColor: Record<TriageColor, string> = {
    rot: 'white',
    gelb: 'black',
    grün: 'white',
    blau: 'white',
    schwarz: 'white',
  };

  return (
    <div
      style={{
        position: 'absolute',
        top: '20%',
        left: '0',
        width: '100%',
        textAlign: 'center',
        justifyContent: 'center',
        alignItems: 'center',
        padding: '0',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap', justifyContent: 'center', margin: '12px 0' }}>
        {TRIAGE_COLORS.map((color) =>
          colorCounts[color] > 0 ? (
            <span
              key={color}
              style={{
                backgroundColor: TRIAGE_COLOR_BACKGROUND[color],
                color: badgeTextColor[color],
                padding: '6px 14px',
                borderRadius: '4px',
                fontWeight: 'bold',
                fontSize: '1rem',
              }}
            >
              {colorCounts[color]} {color.charAt(0).toUpperCase() + color.slice(1)}
            </span>
          ) : null
        )}
        {unassigned > 0 && (
          <span
            style={{
              backgroundColor: 'white',
              color: 'black',
              padding: '6px 14px',
              borderRadius: '4px',
              fontWeight: 'bold',
              fontSize: '1rem',
              border: '1px solid #ccc',
            }}
          >
            {unassigned} Keine Farbe
          </span>
        )}
        {invalid > 0 && (
          <span
            style={{
              backgroundColor: '#ff8c00',
              color: 'black',
              padding: '6px 14px',
              borderRadius: '4px',
              fontWeight: 'bold',
              fontSize: '1rem',
              border: '2px solid #8a4b00',
            }}
          >
            {invalid} Ungültig
          </span>
        )}
      </div>
      <div>
        <table border={1} style={{ margin: 15 }}>
          <thead>
            <tr>
              <th>Nummer</th>
              <th>Atmung</th>
              <th>Blutung</th>
              <th>Triagefarbe</th>
              <th>Transport</th>
              <th>Dringend</th>
              <th>Name</th>
              <th>Position (Longitude)</th>
              <th>Position (Latitude)</th>
              <th>Erfasst</th>
              <th>Letztes Update</th>
            </tr>
          </thead>
          <tbody>
            {triageDisplays.map(({ person, triageDisplay }) => (
              <tr
                key={person.idpatient}
                role="button"
                tabIndex={0}
                aria-label={`Ambulanzprotokoll für Patient ${person.idpatient} öffnen`}
                onClick={() => openPage1(person.idpatient)}
                onKeyDown={(event) => handleRowKeyDown(event, person.idpatient)}
                style={{ cursor: 'pointer' }}
              >
                <td>{person.idpatient}</td>
                <td>{person.atmung ? 'Ja' : 'Nein'}</td>
                <td>{person.blutung ? 'Ja' : 'Nein'}</td>
                <td>
                  <div
                    style={{
                      backgroundColor: triageDisplay.backgroundColor,
                      color: triageDisplay.textColor,
                      border: triageDisplay.border,
                    }}
                  >
                    {triageDisplay.label}
                  </div>
                </td>
                <td>{person.transport ? 'Ja' : 'Nein'}</td>
                <td>{person.dringend ? 'Ja' : 'Nein'}</td>
                <td>{person.name || 'N/A'}</td>
                <td>{person.longitude_patient || 'N/A'}</td>
                <td>{person.latitude_patient || 'N/A'}</td>
                <td>{person.created_at}</td>
                <td>{person.updated_at}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div style={{ height: '400px', width: '100%' }}>
        <MapContainer
          center={mapCenter}
          zoom={13}
          style={{ flex: 1, height: '300px', width: '100%' }}
        >
          <TileLayer
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          />
          {renderMarkers()}
        </MapContainer>
      </div>
    </div>
  );
}

export default SituationRoomTable;
