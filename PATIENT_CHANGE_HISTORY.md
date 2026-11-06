# Änderungen an Patientendaten

Wenn mehrere Geräte denselben Patienten bearbeiten, entscheidet die ursprüngliche
Bearbeitungszeit je Feld, welcher Wert gilt. Das betrifft Triage, Atmung, Position,
Körperregionen und Seite 1 des Ambulanzprotokolls.

## Regeln beim Speichern

- Eine neuere Bearbeitungszeit gewinnt. Änderungen an anderen Feldern bleiben erhalten.
- Bei gleicher Zeit und unterschiedlichem Wert bleibt der gespeicherte Wert bestehen.
- Identische Wiederholungen erzeugen keinen zusätzlichen Historieneintrag.
  Ist ihr früher akzeptierter Wert inzwischen überholt, erhält das Gerät dennoch
  eine Konfliktmeldung mit dem aktuell gültigen Wert.
- Nicht gesendete Felder bleiben unverändert. Ein ausdrücklich gesendetes `null`,
  `false`, `0` oder ein leerer Wert wird als Eingabe behandelt.
- Listen im Formular werden jeweils als Ganzes ersetzt. Jede Region der
  Triage-Körperkarte wird einzeln geprüft; Breiten- und Längengrad bilden ein Positionsfeld.
- Finalisierte Protokolle sind unveränderbar. Werden bei einer Finalisierung
  Feldänderungen verworfen, bleibt das noch offene Protokoll ein Entwurf.
  Auch eine während des Speicherns wartende Finalisierung wird bei einem Konflikt
  abgebrochen. Nach Prüfung des gültigen Standes muss erneut finalisiert werden.

Vergleich, Speicherung und Audit-Eintrag erfolgen innerhalb einer Transaktion
mit Patientensperre. Verworfene Änderungen werden als `rejectedChanges` mit Grund
und gültigem Wert zurückgegeben und im Frontend angezeigt.
Für ein Feld ohne akzeptierte Historie gilt die Patientenerstellungszeit als
Ausgangszeit. Ein später angelegtes Formular oder eine Körperkarte blockiert
dadurch keine unabhängige, bereits zuvor bearbeitete Eingabe.

## Bearbeitungszeiten und Teilupdates

Der Client sendet nur geänderte Felder und deren ursprüngliche Bearbeitungszeit
in UTC. Beispiel für einen Formularrequest:

```json
{
  "status": "draft",
  "changedAt": "2026-11-03T10:05:23.417Z",
  "fieldChangedAt": {
    "form.patient.vorname": "2026-11-03T10:05:23.417Z"
  },
  "formState": {
    "patient": { "vorname": "Anna" }
  }
}
```

`fieldChangedAt` gibt die Zeit einzelner Felder an; `changedAt` dient als Ersatzwert.
Ohne beide Angaben verwendet das Backend die Serverzeit. Für korrektes Verhalten
bei verspäteten Übertragungen muss der Client die ursprüngliche Zeit beibehalten.
Das Formular sichert ausstehende Änderungen samt Zeiten lokal, auch über einen Reload
hinweg. Die separate Triage-Körperkarte besitzt keine dauerhafte Offline-Warteschlange.

Die Geräteuhren müssen stimmen. Zeiten, die mehr als fünf Minuten vor der Serverzeit
liegen, werden mit HTTP 400 abgewiesen. Die vorhandenen `updated_at`-Spalten enthalten
weiterhin die serverseitige Speicherzeit und entscheiden nicht über Feldkonflikte.

## Änderungsverlauf

Aktuelle Werte stehen in den bestehenden Fachtabellen. Die gemeinsame Tabelle
`patient_changes` protokolliert übermittelte Änderungen und deren Entscheidung:

| Angaben | Bedeutung |
| --- | --- |
| `Id`, `PatientId`, `FieldId` | Eintrag, Patient und betroffenes Feld |
| `Value` | Übermittelter Wert als JSON, auch bei Ablehnung |
| `ChangedAt` | Ursprüngliche Bearbeitungszeit |
| `ReceivedAt` | Serverzeit beim Beginn der Verarbeitungssitzung |
| `Accepted`, `Reason` | Entscheidung und Begründung |
| `ActorId`, `DeviceId` | Identität aus der Anmeldung und optionale Gerätekennung |

Die festen Feldnummern stehen in `ChangeFields.cs` und dürfen nicht für andere
Felder wiederverwendet werden. Wiederholungen werden über Patient, Feld, Zeit und
inhaltlich gleichen Wert erkannt. Ein gemeinsam verwendeter QR-Login identifiziert
keinen einzelnen Sanitäter. Die aktuelle Clientanbindung sendet keine `deviceId`.

Die Historie ist mit derselben Patienten- und Einsatzberechtigung wie beim Bearbeiten
über die API abrufbar:

```text
GET /api/v2/persons/{patientId}/changes?limit=50&beforeId=1234
```

Die Antwort enthält `items` und bei weiteren Einträgen `nextBeforeId`. Es werden
höchstens 100 Einträge pro Seite ausgegeben, die zuletzt gespeicherten zuerst.
Die Historie wächst mit den gespeicherten Änderungen; automatische Löschung oder
Archivierung ist nicht vorgesehen.

## Einrichtung

Die Migration `20261103202048_AddPatientChangeHistory` erstellt die Tabelle und
übernimmt vorhandene Werte als Ausgangseinträge (`baseline`). Frühere Bearbeitungen
lassen sich daraus nicht rekonstruieren. Die Migration muss vor dem API-Start
angewendet werden; im DEV-Modus geschieht das automatisch.

