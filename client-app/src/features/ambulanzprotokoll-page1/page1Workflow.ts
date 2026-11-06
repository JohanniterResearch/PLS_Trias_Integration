import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import type { BodyMapMarker, Page1PersistedRecord, Page1RecordStatus, Page1State, PendingPage1Field } from './page1State';
import { clearPage1Draft, readPage1Draft, writePage1Draft } from './page1Storage';
import { getPage1, putPage1 } from './page1Api';
import { createPage1Template, normalizePage1State, page1FinalizeSchema, serializePage1State } from './page1State';
import { flattenFields, patchFields, pendingPayload } from './page1Changes';

export type Page1SyncState = 'loading' | 'ready' | 'saving' | 'saved-local' | 'saved-server' | 'finalized' | 'error';
export interface UsePage1WorkflowArgs {
  patientId: number;
  operationSceneId: number | null;
  token?: string | null;
}

export function useAmbulanzprotokollPage1Workflow({ patientId, operationSceneId, token }: UsePage1WorkflowArgs) {
  const form = useForm<Page1State>({ defaultValues: createPage1Template(), mode: 'onChange' });
  const { getValues, reset, setValue, watch } = form;
  const [syncState, setSyncState] = useState<Page1SyncState>('loading');
  const [status, setStatus] = useState<Page1RecordStatus>('draft');
  const [lastSavedAt, setLastSavedAt] = useState<string | null>(null);
  const [source, setSource] = useState<'local' | 'server' | 'empty'>('empty');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [validationWarnings, setValidationWarnings] = useState<string[]>([]);
  const [currentRecord, setCurrentRecord] = useState<Page1PersistedRecord | null>(null);
  const [watchedState, setWatchedState] = useState<Page1State>(createPage1Template);
  const pending = useRef<Record<string, PendingPage1Field>>({});
  const finalizeAt = useRef<string | null>(null);
  const state = useRef(createPage1Template());
  const statusRef = useRef<Page1RecordStatus>('draft');
  const hydrated = useRef(false);
  const generation = useRef(0);
  const applying = useRef(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const inFlight = useRef<Promise<void> | null>(null);

  const replaceState = useCallback((next: Page1State) => {
    applying.current = true;
    state.current = structuredClone(next);
    reset(next);
    setWatchedState(next);
    applying.current = false;
  }, [reset]);

  const persistLocal = useCallback(() => {
    const changedAt = Object.values(pending.current).map(p => p.changedAt).sort().at(-1) ?? finalizeAt.current ?? new Date().toISOString();
    const record: Page1PersistedRecord = {
      patientId, operationSceneId, formState: state.current,
      status: statusRef.current, updatedAt: changedAt, finalizedAt: finalizeAt.current,
      pendingFields: pending.current, pendingFinalizeAt: finalizeAt.current,
    };
    writePage1Draft(record);
    setCurrentRecord(record);
    setLastSavedAt(changedAt);
    setSource('local');
  }, [operationSceneId, patientId]);

  const flush = useCallback(async (): Promise<void> => {
    // Serialize requests; edits made during a request remain in the next batch.
    while (inFlight.current) await inFlight.current;
    if (!hydrated.current || (!Object.keys(pending.current).length && !finalizeAt.current)) return;
    if (!token) { setSyncState('saved-local'); return; }
    const currentGeneration = generation.current;
    const batch = { ...pending.current };
    const requestedFinalizeAt = finalizeAt.current;
    const payload = pendingPayload(batch, requestedFinalizeAt);
    const send = async () => {
      setSyncState('saving');
      try {
        const { data } = await putPage1(patientId, payload, token);
        if (currentGeneration !== generation.current) return;
        for (const [path, entry] of Object.entries(batch)) {
          if (pending.current[path] === entry) delete pending.current[path];
        }
        const hasRejectedChanges = Boolean(data?.rejectedChanges?.length);
        // A conflict also cancels finalization queued while this request was in flight.
        if (hasRejectedChanges || finalizeAt.current === requestedFinalizeAt) finalizeAt.current = null;
        const nextStatus = data?.status ?? payload.status;
        statusRef.current = finalizeAt.current ? 'finalized' : nextStatus;
        setStatus(statusRef.current);
        // The server returns the canonical merged state, including winning remote values.
        const canonical = normalizePage1State(data?.formState ?? state.current);
        replaceState(patchFields(canonical, pending.current));
        if (hasRejectedChanges) setErrorMessage('Einige Änderungen wurden verworfen. Bitte den gültigen Stand prüfen und bei Bedarf erneut finalisieren.');
        else setErrorMessage(null);
        if (nextStatus === 'finalized') {
          pending.current = {};
          clearPage1Draft(patientId);
          setCurrentRecord(data ? { ...data, operationSceneId } : null);
        } else persistLocal();
        setLastSavedAt(data?.updatedAt ?? payload.changedAt);
        setSource('server');
        setSyncState(nextStatus === 'finalized' ? 'finalized' : 'saved-server');
      } catch (error) {
        if (currentGeneration !== generation.current) return;
        // A finalized server record is immutable; do not keep retrying its rejected edits.
        const response = typeof error === 'object' && error !== null && 'response' in error
          ? error.response as { status?: number; data?: { rejectedChanges?: unknown[] } } : undefined;
        const hasRejectedChanges = Boolean(response?.data?.rejectedChanges?.length);
        if (hasRejectedChanges) {
          finalizeAt.current = null;
          statusRef.current = 'draft';
          setStatus('draft');
        }
        if (response?.status === 409) {
          try {
            const { data } = await getPage1(patientId, token);
            if (currentGeneration !== generation.current) return;
            if (data?.status === 'finalized') {
              pending.current = {};
              finalizeAt.current = null;
              statusRef.current = 'finalized';
              setStatus('finalized');
              replaceState(normalizePage1State(data.formState));
              clearPage1Draft(patientId);
              setCurrentRecord({ ...data, operationSceneId });
              setSource('server');
              setSyncState('finalized');
              setErrorMessage('Protokoll ist bereits finalisiert. Der gespeicherte Stand wurde geladen.');
              return;
            }
          } catch { /* Keep the local edits if loading the canonical state fails. */ }
        }
        persistLocal();
        setSyncState('saved-local');
        setErrorMessage(hasRejectedChanges
          ? 'Änderungen wurden verworfen. Bitte Angaben prüfen und erneut finalisieren.'
          : error instanceof Error ? error.message : 'Lokal gespeichert; Server-Sync ausstehend');
      }
    };
    inFlight.current = send();
    try { await inFlight.current; } finally { inFlight.current = null; }
  }, [operationSceneId, patientId, persistLocal, replaceState, token]);

  useEffect(() => {
    let cancelled = false;
    const currentGeneration = ++generation.current;
    hydrated.current = false;
    pending.current = {};
    finalizeAt.current = null;
    async function hydrate() {
      setSyncState('loading');
      const local = readPage1Draft(patientId);
      try {
        const { data: server } = await getPage1(patientId, token ?? undefined);
        if (cancelled) return;
        const base = normalizePage1State(server?.formState ?? createPage1Template());
        pending.current = local?.pendingFields ?? {};
        finalizeAt.current = local?.pendingFinalizeAt ?? null;
        // Upgrade old local drafts once; retain their original edit time.
        if (local && !local.pendingFields && local.updatedAt && (!server?.updatedAt || local.updatedAt > server.updatedAt)) {
          const before = flattenFields(base);
          for (const [path, value] of Object.entries(flattenFields(normalizePage1State(local.formState)))) {
            if (JSON.stringify(value) !== JSON.stringify(before[path])) pending.current[path] = { value, changedAt: local.updatedAt };
          }
          if (local.status === 'finalized') finalizeAt.current = local.finalizedAt ?? local.updatedAt;
        }
        const hasLocal = Object.keys(pending.current).length > 0 || !!finalizeAt.current;
        statusRef.current = server?.status === 'finalized' ? 'finalized' : finalizeAt.current ? 'finalized' : 'draft';
        setStatus(statusRef.current);
        // Pending edits must reach the API for audit even if another device finalized meanwhile.
        replaceState(server?.status === 'finalized' ? base : patchFields(base, pending.current));
        setCurrentRecord(hasLocal ? local : server ? { ...server, operationSceneId } : null);
        setLastSavedAt(hasLocal ? local?.updatedAt ?? null : server?.updatedAt ?? null);
        setSource(hasLocal ? 'local' : server ? 'server' : 'empty');
        setSyncState('ready');
      } catch (error) {
        if (cancelled) return;
        pending.current = local?.pendingFields ?? {};
        finalizeAt.current = local?.pendingFinalizeAt ?? null;
        replaceState(normalizePage1State(local?.formState ?? createPage1Template()));
        statusRef.current = local?.status ?? 'draft';
        setStatus(statusRef.current);
        setCurrentRecord(local);
        setSource(local ? 'local' : 'empty');
        setErrorMessage(error instanceof Error ? error.message : 'Fehler beim Laden');
        setSyncState('error');
      }
      hydrated.current = true;
    }
    void hydrate();
    return () => { cancelled = true; generation.current = currentGeneration + 1; hydrated.current = false; if (timer.current) clearTimeout(timer.current); };
  }, [operationSceneId, patientId, replaceState, token]);

  useEffect(() => {
    const subscription = watch((value) => {
      if (!hydrated.current || applying.current || statusRef.current === 'finalized') return;
      // react-hook-form may mutate nested objects; compare against an independent snapshot.
      const next = structuredClone(normalizePage1State(value as Page1State));
      const before = flattenFields(state.current);
      const changedAt = new Date().toISOString();
      for (const [path, entry] of Object.entries(flattenFields(next))) {
        if (JSON.stringify(entry) !== JSON.stringify(before[path])) pending.current[path] = { value: entry, changedAt };
      }
      state.current = next;
      setWatchedState(next);
      persistLocal();
    });
    return () => subscription.unsubscribe();
  }, [persistLocal, watch]);

  useEffect(() => {
    if (timer.current) clearTimeout(timer.current);
    if (hydrated.current && (Object.keys(pending.current).length || finalizeAt.current)) timer.current = setTimeout(() => void flush(), 800);
    return () => { if (timer.current) clearTimeout(timer.current); };
  }, [flush, watchedState]);

  useEffect(() => {
    const online = () => void flush();
    window.addEventListener('online', online);
    return () => window.removeEventListener('online', online);
  }, [flush]);

  const finalize = async () => {
    if (statusRef.current === 'finalized' && !finalizeAt.current) return;
    const validState = normalizePage1State(getValues());
    const validation = page1FinalizeSchema.safeParse(validState);
    setValidationWarnings(validation.success ? [] : validation.error.issues.map(issue => `${issue.path.join('.')}: ${issue.message}`));
    finalizeAt.current ??= new Date().toISOString();
    statusRef.current = 'finalized';
    setStatus('finalized');
    persistLocal();
    await flush();
  };

  const setBodyMap = useCallback((value: BodyMapMarker[]) => {
    setValue('assessment_secondary.bodymap', value, { shouldDirty: true, shouldTouch: true });
  }, [setValue]);
  const setSignature = useCallback((value: string | null) => {
    setValue('signatures.entlass_san_na', value, { shouldDirty: true, shouldTouch: true });
  }, [setValue]);
  const exportJson = useMemo(() => serializePage1State(watchedState), [watchedState]);
  return { form, finalize, exportJson, syncState, status, source, lastSavedAt, errorMessage, validationWarnings, currentRecord, setBodyMap, setSignature };
}
