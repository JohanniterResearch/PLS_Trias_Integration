import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import CameraFeedback from './CameraFeedback';
import { cameraErrorMessages } from '../hooks/cameraError';

afterEach(cleanup);

describe('CameraFeedback', () => {
  it('announces pending permission without offering duplicate starts', () => {
    render(<CameraFeedback error={null} isStarting onRetry={vi.fn()} />);
    expect(screen.getByRole('status')).toHaveTextContent('Kamera wird gestartet');
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it.each(['permission', 'not-found', 'unavailable', 'unknown'] as const)('offers retry for %s', (error) => {
    const retry = vi.fn();
    render(<CameraFeedback error={error} isStarting={false} onRetry={retry} />);
    expect(screen.getByRole('alert')).toHaveTextContent(cameraErrorMessages[error]);
    fireEvent.click(screen.getByRole('button', { name: 'Erneut versuchen' }));
    expect(retry).toHaveBeenCalledOnce();
  });

  it.each(['insecure', 'unsupported'] as const)('offers guidance without an ineffective retry for %s', (error) => {
    render(<CameraFeedback error={error} isStarting={false} onRetry={vi.fn()} />);
    expect(screen.getByRole('alert')).toHaveTextContent(cameraErrorMessages[error]);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('shows no error while the camera is working', () => {
    const { container } = render(<CameraFeedback error={null} isStarting={false} onRetry={vi.fn()} />);
    expect(container).toBeEmptyDOMElement();
  });
});
