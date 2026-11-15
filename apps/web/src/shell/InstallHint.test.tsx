import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { canInstallToHomeScreen, InstallHint } from './InstallHint';

const IPHONE_SAFARI =
  'Mozilla/5.0 (iPhone; CPU iPhone OS 26_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Mobile/15E148 Safari/604.1';
const IPAD_SAFARI =
  'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15';
const IPHONE_CHROME =
  'Mozilla/5.0 (iPhone; CPU iPhone OS 26_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/140.0 Mobile/15E148 Safari/604.1';

function pretendToBe(userAgent: string, standalone: boolean | undefined, maxTouchPoints = 5) {
  vi.stubGlobal('navigator', { userAgent, standalone, maxTouchPoints });
}

afterEach(() => vi.unstubAllGlobals());

describe('canInstallToHomeScreen', () => {
  it.each([
    ['Safari on an iPhone, not installed', IPHONE_SAFARI, 5, false, true],
    ['Safari on an iPad, not installed', IPAD_SAFARI, 5, false, true],
    ['the installed app', IPHONE_SAFARI, 5, true, false],
    ['Chrome on an iPhone', IPHONE_CHROME, 5, false, false],
    ['Safari on a Mac', IPAD_SAFARI, 0, undefined, false],
  ])('%s', (_name, userAgent, maxTouchPoints, standalone, expected) => {
    expect(canInstallToHomeScreen({ userAgent, maxTouchPoints, standalone })).toBe(expected);
  });
});

describe('InstallHint', () => {
  it('explains Add to Home Screen, and goes away when dismissed', async () => {
    pretendToBe(IPHONE_SAFARI, false);
    render(<InstallHint />);
    expect(screen.getByText('Add to Home Screen')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'OK' }));
    expect(screen.queryByText('Add to Home Screen')).not.toBeInTheDocument();
  });

  it('shows once: a second visit stays quiet', () => {
    pretendToBe(IPHONE_SAFARI, false);
    render(<InstallHint />).unmount();
    render(<InstallHint />);
    expect(screen.queryByText('Add to Home Screen')).not.toBeInTheDocument();
  });

  it('never shows in the installed app', () => {
    pretendToBe(IPHONE_SAFARI, true);
    render(<InstallHint />);
    expect(screen.queryByText('Add to Home Screen')).not.toBeInTheDocument();
  });
});
