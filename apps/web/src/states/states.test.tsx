import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';

import { EmptyState } from './EmptyState';
import { ErrorState } from './ErrorState';
import { ScanBanner, scanMessage } from './ScanBanner';
import { Skeleton } from './Skeleton';

describe('Skeleton, the loading state', () => {
  it('shows the requested number of shapes and tells screen readers what is loading', () => {
    const { container } = render(<Skeleton shape="card" count={6} label="Loading your trips" />);
    expect(container.querySelectorAll('.skeleton--card')).toHaveLength(6);
    expect(screen.getByRole('status')).toHaveTextContent('Loading your trips');
    expect(screen.getByRole('status')).toHaveAttribute('aria-busy', 'true');
  });
});

describe('ScanBanner, the scanning state', () => {
  it('reads like the design document', () => {
    expect(scanMessage(12400, 48000)).toBe('Scanning your library: 12,400 of 48,000 photos.');
  });

  it('says how far it is before the total is known', () => {
    expect(scanMessage(250, null)).toBe('Scanning your library: 250 photos so far.');
  });

  it('shows progress as a status, so it is announced without taking focus', () => {
    render(<ScanBanner done={12400} total={48000} />);
    expect(screen.getByRole('status')).toHaveTextContent('12,400 of 48,000');
    const bar = screen.getByRole('progressbar', { name: 'Scan progress' });
    expect(bar).toHaveAttribute('value', '12400');
    expect(bar).toHaveAttribute('max', '48000');
  });
});

describe('EmptyState', () => {
  it('shows a message and one next step', () => {
    render(
      <EmptyState
        title="No trips yet"
        message="Trips you add appear here."
        action={<a href="/inbox">Open the inbox</a>}
      />,
    );
    expect(screen.getByRole('heading', { name: 'No trips yet' })).toBeInTheDocument();
    expect(screen.getByText('Trips you add appear here.')).toBeInTheDocument();
    expect(screen.getAllByRole('link')).toHaveLength(1);
  });
});

describe('ErrorState', () => {
  it('explains the problem as an alert and retries on request', async () => {
    const retry = vi.fn();
    render(
      <MemoryRouter>
        <ErrorState
          message="Can't reach Immich. Check the server address in Settings."
          onRetry={retry}
        />
      </MemoryRouter>,
    );
    expect(screen.getByRole('alert')).toHaveTextContent("Can't reach Immich.");
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(retry).toHaveBeenCalledOnce();
    expect(screen.queryByRole('link', { name: 'Open Settings' })).not.toBeInTheDocument();
  });

  it('offers Settings when the fix is there', () => {
    render(
      <MemoryRouter>
        <ErrorState message="Can't reach Immich." showSettings />
      </MemoryRouter>,
    );
    expect(screen.getByRole('link', { name: 'Open Settings' })).toHaveAttribute(
      'href',
      '/settings',
    );
  });
});
